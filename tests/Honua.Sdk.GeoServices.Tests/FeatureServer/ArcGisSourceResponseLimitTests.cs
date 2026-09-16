// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

/// <summary>
/// honua-sdk-dotnet#373: source JSON and error bodies are read under a caller-configurable byte ceiling
/// (default equal to honua-server's <c>MigrationHttpContentReader.DefaultMaxResponseBytes</c>), an oversized
/// body fails with a typed exception and is never fully buffered, and <c>queryAttachments</c> lists
/// attachments for a batch of features in one request.
/// </summary>
public sealed class ArcGisSourceResponseLimitTests
{
    private const long Limit = 4096;

    [Fact]
    public void DefaultCeiling_MatchesTheServerImporter()
    {
        Assert.Equal(64L * 1024 * 1024, HonuaFeatureServerClientOptions.DefaultMaxResponseBytes);
        Assert.Equal(HonuaFeatureServerClientOptions.DefaultMaxResponseBytes, new HonuaFeatureServerClientOptions().MaxResponseBytes);
    }

    [Fact]
    public async Task DeclaredOversizedBody_IsRefusedBeforeAnyByteIsRead()
    {
        var body = new CountingStream(length: Limit + 1);
        var client = CreateClient(_ => Respond(HttpStatusCode.OK, body, declareLength: true));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.GetLayerInfoAsync("Hydrants", 0));

        Assert.Equal(0, body.BytesRead);
        Assert.Equal(Limit, ex.MaxResponseBytes);
        Assert.Equal(Limit + 1, ex.DeclaredContentLength);
        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Equal(new Uri("https://gis.example.com/arcgis/rest/services/Hydrants/FeatureServer/0?f=json"), ex.RequestUri);
        Assert.IsAssignableFrom<HonuaException>(ex);
        Assert.True(body.Disposed);
    }

    [Fact]
    public async Task UndeclaredOversizedBody_IsAbandonedAtTheCeiling()
    {
        // No Content-Length (chunked) and an effectively endless body: the reader must stop within one
        // read chunk of the ceiling rather than buffering the whole stream.
        var body = new CountingStream(length: long.MaxValue);
        var client = CreateClient(_ => Respond(HttpStatusCode.OK, body, declareLength: false));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.QueryAsync("Hydrants", 0, new FeatureServerQueryParams { Where = "1=1" }));

        Assert.Null(ex.DeclaredContentLength);
        Assert.InRange(body.BytesRead, Limit + 1, Limit + (64 * 1024));
        Assert.True(body.Disposed);
    }

    [Fact]
    public async Task BodyAtTheCeiling_IsReadNormally()
    {
        var json = """{"id":0,"name":"Hydrants"}""";
        var padded = json + new string(' ', (int)Limit - Encoding.UTF8.GetByteCount(json));
        var client = CreateClient(_ => Respond(HttpStatusCode.OK, new MemoryStream(Encoding.UTF8.GetBytes(padded)), declareLength: false));

        var layer = await client.GetLayerInfoAsync("Hydrants", 0);

        Assert.Equal("Hydrants", layer.Name);
    }

    [Fact]
    public async Task BoundedRead_DecodesTheDeclaredCharsetAndStripsTheBom()
    {
        var latin1 = CreateClient(_ =>
        {
            var response = Respond(HttpStatusCode.OK, new MemoryStream(Encoding.Latin1.GetBytes("""{"id":0,"name":"Café"}""")), declareLength: true);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "iso-8859-1" };
            return response;
        });
        var utf8Bom = CreateClient(_ => Respond(
            HttpStatusCode.OK,
            new MemoryStream([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("""{"id":0,"name":"Café"}""")]),
            declareLength: true));

        Assert.Equal("Café", (await latin1.GetLayerInfoAsync("Hydrants", 0)).Name);
        Assert.Equal("Café", (await utf8Bom.GetLayerInfoAsync("Hydrants", 0)).Name);
    }

    [Fact]
    public async Task OversizedErrorBody_FailsWithTheTypedExceptionAndStatus()
    {
        var body = new CountingStream(length: long.MaxValue);
        var client = CreateClient(_ => Respond(HttpStatusCode.InternalServerError, body, declareLength: false));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.GetServiceInfoAsync("Hydrants"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal(500, ex.HttpStatus);
        Assert.InRange(body.BytesRead, 0, Limit + (64 * 1024));
    }

    [Fact]
    public async Task OversizedPostBody_FailsWithTheTypedException()
    {
        var bodies = new List<CountingStream>();
        var client = CreateClient(_ =>
        {
            var body = new CountingStream(length: long.MaxValue);
            bodies.Add(body);
            return Respond(HttpStatusCode.OK, body, declareLength: false);
        });

        await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.DeleteFeaturesAsync("Hydrants", 0, [1]));
        await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.ValidateSqlAsync("Hydrants", 0, "1=1"));
        await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.QueryAsync("Hydrants", 0, new FeatureServerQueryParams { Where = new string('x', 2100) }));

        Assert.Equal(3, bodies.Count);
        Assert.All(bodies, body => Assert.InRange(body.BytesRead, Limit + 1, Limit + (64 * 1024)));
    }

    [Fact]
    public async Task OversizedAttachmentErrorBodies_FailWithTheTypedException()
    {
        var client = CreateClient(_ => Respond(HttpStatusCode.BadGateway, new CountingStream(long.MaxValue), declareLength: false));
        var source = new FeatureSource { ServiceId = "Hydrants", LayerId = 0 };

        await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.DownloadAttachmentAsync(new FeatureAttachmentDownloadRequest { Source = source, ObjectId = 1, AttachmentId = 2 }));
        using var upload = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.AddAttachmentAsync(new FeatureAttachmentAddRequest { Source = source, ObjectId = 1, Content = upload, Name = "a.bin", ContentType = "application/octet-stream" }));
        await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(
            () => client.QueryVectorAsync("Hydrants", 0, new FeatureServerQueryParams(), Honua.Sdk.Geometry.Vector.VectorPayloadFormat.GeoJson));
    }

    [Fact]
    public async Task CeilingIsCallerConfigurable()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"id":0,"name":"Hydrants"}""" + new string(' ', 10_000));
        var generous = CreateClient(_ => Respond(HttpStatusCode.OK, new MemoryStream(bytes), declareLength: true), maxResponseBytes: 20_000);
        var strict = CreateClient(_ => Respond(HttpStatusCode.OK, new MemoryStream(bytes), declareLength: true), maxResponseBytes: 1_000);

        Assert.Equal("Hydrants", (await generous.GetLayerInfoAsync("Hydrants", 0)).Name);
        var ex = await Assert.ThrowsAsync<HonuaFeatureServerResponseTooLargeException>(() => strict.GetLayerInfoAsync("Hydrants", 0));
        Assert.Equal(1_000, ex.MaxResponseBytes);
    }

    // ── queryAttachments ────────────────────────────────────────────

    [Fact]
    public async Task QueryAttachments_ListsABatchInOneRequest()
    {
        var sent = new List<string>();
        var client = CreateClient(request =>
        {
            sent.Add(request.RequestUri!.AbsoluteUri);
            return TestHelpers.CreateRawJsonResponse("""
            {
              "attachmentGroups": [
                {
                  "parentObjectId": 1,
                  "parentGlobalId": "{5E1B4E4B-0000-0000-0000-000000000001}",
                  "attachmentInfos": [
                    { "id": 11, "globalId": "{A}", "name": "front.jpg", "contentType": "image/jpeg", "size": 2048, "keywords": "inspection" },
                    { "id": 12, "name": "rear.jpg", "contentType": "image/jpeg", "size": 1024 }
                  ]
                },
                { "parentObjectId": 3, "attachmentInfos": [ { "id": 31, "name": "valve.pdf", "contentType": "application/pdf", "size": 99 } ] }
              ]
            }
            """);
        });

        var response = await client.QueryAttachmentsAsync("Utilities/Water", 0, [1, 2, 3]);

        Assert.Equal(["https://gis.example.com/arcgis/rest/services/Utilities/Water/FeatureServer/0/queryAttachments?objectIds=1%2C2%2C3&returnUrl=false&f=json"], sent);
        Assert.Equal(2, response.AttachmentGroups.Count);
        var first = response.AttachmentGroups[0];
        Assert.Equal(1, first.ParentObjectId);
        Assert.Equal("{5E1B4E4B-0000-0000-0000-000000000001}", first.ParentGlobalId);
        Assert.Equal([11L, 12L], first.AttachmentInfos.Select(info => info.Id!.Value));
        Assert.Equal("front.jpg", first.AttachmentInfos[0].Name);
        Assert.Equal("image/jpeg", first.AttachmentInfos[0].ContentType);
        Assert.Equal(2048, first.AttachmentInfos[0].Size);
        Assert.Equal("inspection", first.AttachmentInfos[0].Keywords);
        Assert.Equal(3, response.AttachmentGroups[1].ParentObjectId);
        Assert.Null(response.AttachmentGroups[1].ParentGlobalId);
        Assert.Equal("valve.pdf", Assert.Single(response.AttachmentGroups[1].AttachmentInfos).Name);
    }

    [Fact]
    public async Task QueryAttachments_EmptyBatchSendsNoRequest()
    {
        var sent = 0;
        var client = CreateClient(_ =>
        {
            sent++;
            return TestHelpers.CreateRawJsonResponse("{}");
        });

        var response = await client.QueryAttachmentsAsync("Hydrants", 0, []);

        Assert.Empty(response.AttachmentGroups);
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task QueryAttachments_LargeBatchFallsBackToFormPost()
    {
        string? method = null;
        string? url = null;
        string? form = null;
        var client = CreateClient(request =>
        {
            method = request.Method.Method;
            url = request.RequestUri!.AbsoluteUri;
            form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return TestHelpers.CreateRawJsonResponse("""{"attachmentGroups":[]}""");
        });
        var objectIds = Enumerable.Range(100_000, 1000).Select(id => (long)id).ToArray();

        await client.QueryAttachmentsAsync("Hydrants", 0, objectIds);

        Assert.Equal("POST", method);
        Assert.Equal("https://gis.example.com/arcgis/rest/services/Hydrants/FeatureServer/0/queryAttachments", url);
        Assert.StartsWith("objectIds=100000%2C100001%2C", form, StringComparison.Ordinal);
        Assert.EndsWith("&returnUrl=false&f=json", form, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAttachments_ReportsGeoServicesErrors()
    {
        var client = CreateClient(_ => TestHelpers.CreateRawJsonResponse("""{"error":{"code":400,"message":"Layer does not support attachments."}}"""));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(() => client.QueryAttachmentsAsync("Hydrants", 0, [1]));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    // ── helpers ─────────────────────────────────────────────────────

    private static HonuaFeatureServerClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> respond, long maxResponseBytes = Limit)
    {
        var handler = new MockHttpHandler(request => Task.FromResult(WithRequest(respond(request), request)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://gis.example.com/arcgis/") };
        return new HonuaFeatureServerClient(http, new HonuaFeatureServerClientOptions { MaxResponseBytes = maxResponseBytes });
    }

    private static HttpResponseMessage WithRequest(HttpResponseMessage response, HttpRequestMessage request)
    {
        response.RequestMessage ??= request;
        return response;
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, Stream body, bool declareLength)
    {
        var content = new StreamContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        if (declareLength)
        {
            content.Headers.ContentLength = body.Length;
        }

        return new HttpResponseMessage(status) { Content = content };
    }

    /// <summary>A read-only stream of spaces that records how many bytes were read and whether it was disposed.</summary>
    private sealed class CountingStream(long length) : Stream
    {
        public long BytesRead { get; private set; }

        public bool Disposed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => BytesRead;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = (int)Math.Min(count, length - BytesRead);
            buffer.AsSpan(offset, read).Fill((byte)' ');
            BytesRead += read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => Task.FromResult(Read(buffer, offset, count));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = (int)Math.Min(buffer.Length, length - BytesRead);
            buffer.Span[..read].Fill((byte)' ');
            BytesRead += read;
            return ValueTask.FromResult(read);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
