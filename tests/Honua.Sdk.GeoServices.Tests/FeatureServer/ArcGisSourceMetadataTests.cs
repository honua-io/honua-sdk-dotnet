// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.Tests.Fixtures;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

public sealed class ArcGisSourceMetadataTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Metadata_PreservesSourcePresenceAndExtensions_InCallerOwnedDocument(bool layer)
    {
        const string source = """
            {
              "id": 3, "name": "Caf\u00e9", "hasAttachments": null,
              "spatialReference": { "wkt": "custom WKT", "latestWkt": "latest custom WKT", "vertical": { "unit": "meters" } },
              "fields": [
                { "name": "omitted", "type": "esriFieldTypeString" },
                { "name": "required", "nullable": false, "editable": false },
                { "name": "optional", "nullable": true, "editable": true },
                { "name": "explicit-null", "nullable": null, "editable": null }
              ],
              "vendor": { "largeId": 9007199254740993, "values": [null, false, 1.25] }
            }
            """;
        var content = new TrackingContent(source);
        var client = TestHelpers.CreateFeatureServerClient(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        using var document = await ReadMetadataAsync(client, layer);

        Assert.True(content.Disposed);
        Assert.Equal(source, document.RootElement.GetRawText());
        Assert.Equal("Caf\u00e9", document.RootElement.GetProperty("name").GetString());
        var fields = document.RootElement.GetProperty("fields");
        Assert.False(fields[0].TryGetProperty("nullable", out _));
        Assert.False(fields[0].TryGetProperty("editable", out _));
        Assert.False(fields[1].GetProperty("nullable").GetBoolean());
        Assert.True(fields[2].GetProperty("nullable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, fields[3].GetProperty("nullable").ValueKind);
        Assert.False(document.RootElement.GetProperty("spatialReference").TryGetProperty("wkid", out _));
        Assert.Equal(9007199254740993, document.RootElement.GetProperty("vendor").GetProperty("largeId").GetInt64());

        document.Dispose();
        Assert.Throws<ObjectDisposedException>(() => document.RootElement.GetProperty("name"));
    }

    [Theory]
    [InlineData(false, HttpStatusCode.OK)]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, HttpStatusCode.TooManyRequests)]
    public async Task Metadata_ReportsEnvelopeOrTransportError_AndDisposesResponse(bool layer, HttpStatusCode status)
    {
        var content = new TrackingContent(status == HttpStatusCode.OK
            ? """{"error":{"code":403,"message":"Denied","details":["Read access required"]}}"""
            : """{"message":"Rate limit"}""");
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = content };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));
            return Task.FromResult(response);
        });

        var exception = await Assert.ThrowsAsync<HonuaFeatureServerException>(() => ReadMetadataAsync(client, layer));

        Assert.True(content.Disposed);
        Assert.Equal(TimeSpan.FromSeconds(3), exception.RetryAfter);
        Assert.Equal(status == HttpStatusCode.OK ? HttpStatusCode.Forbidden : status, exception.StatusCode);
        Assert.Equal(status == HttpStatusCode.OK ? 403 : (int?)null, exception.GeoServicesErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Metadata_InvalidJson_DisposesResponse(bool layer)
    {
        var content = new TrackingContent("{ invalid");
        var client = TestHelpers.CreateFeatureServerClient(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        await Assert.ThrowsAsync<JsonException>(() => ReadMetadataAsync(client, layer));

        Assert.True(content.Disposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Metadata_CancellationDuringBodyRead_PropagatesAndDisposesStream(bool layer)
    {
        var cancellation = new CancellationTokenSource();
        try
        {
            var body = new WaitingStream();
            var client = TestHelpers.CreateFeatureServerClient(_ => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) }));

            var read = ReadMetadataAsync(client, layer, cancellation.Token);
            await body.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
            Assert.True(body.Disposed);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static Task<JsonDocument> ReadMetadataAsync(
        HonuaFeatureServerClient client, bool layer, CancellationToken cancellationToken = default)
        => layer
            ? client.GetLayerMetadataAsync("Utilities/Water", 3, cancellationToken)
            : client.GetServiceMetadataAsync("Utilities/Water", cancellationToken);

    private sealed class TrackingContent(string json) : StringContent(json, Encoding.UTF8, "application/json")
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class WaitingStream : MemoryStream
    {
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Disposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
