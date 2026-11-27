// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Field.Forms;

namespace Honua.Sdk.Field.Tests;

public sealed class PublishedFormSubmissionClientTests
{
    private const string Frozen = """
        { "formVersion": 3, "operation": "create", "idempotencyKey": "record-7",
          "values": { "name": "Legacy gate" }, "attachments": [
            { "clientAttachmentId": "evidence-1", "fieldId": "photo", "partName": "photo-file" }
          ] }
        """;

    [Fact]
    public async Task SubmitAsync_ReplaysUnchangedEnvelopeAndEvidenceWithFreshStreams()
    {
        // Matches the server's FormPackageEndpoints multipart submission and replay receipts.
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        var opened = new List<MemoryStream>();
        var requests = 0;
        var deadlineLength = 0L;
        using var http = CreateHttp(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/forms/bridge%20check/submissions", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            Assert.Equal(multipart.Headers.ContentLength, deadlineLength);
            Assert.True(deadlineLength > bytes.Length + Encoding.UTF8.GetByteCount(Frozen));
            var parts = multipart.ToArray();
            Assert.Equal(2, parts.Length);
            Assert.Equal("submission", parts[0].Headers.ContentDisposition?.Name?.Trim('"'));
            Assert.Equal("application/json", parts[0].Headers.ContentType?.MediaType);
            Assert.Equal(Frozen, await parts[0].ReadAsStringAsync(ct));
            Assert.Equal("photo-file", parts[1].Headers.ContentDisposition?.Name?.Trim('"'));
            Assert.Equal("photo.png", parts[1].Headers.ContentDisposition?.FileName?.Trim('"'));
            Assert.Equal("image/png", parts[1].Headers.ContentType?.MediaType);
            Assert.Equal(bytes, await parts[1].ReadAsByteArrayAsync(ct));
            var replay = ++requests == 2 ? "true" : "false";
            return Response(HttpStatusCode.OK, $$"""
                {"submissionId":"85afc68d-b063-4236-b24c-daed78a6ce2a","formId":"bridge check","formVersion":3,
                 "status":"accepted","operation":"create","targetFeatureId":19,"editOutcome":{"succeeded":true,"created":1},
                 "attachmentOutcomes":[{"clientAttachmentId":"evidence-1","fieldId":"photo","status":"accepted","attachmentId":42}],
                 "validationIssues":[],"idempotentReplay":{{replay}}}
                """);
        });
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
        var client = new PublishedFormSubmissionClient(http);
        PublishedFormPart[] evidence = [new("photo-file", "photo.png", "image/png", () =>
        {
            var stream = new MemoryStream(bytes);
            opened.Add(stream);
            return stream;
        })];

        CancellationTokenSource Deadline(long length)
        {
            deadlineLength = length;
            return new CancellationTokenSource();
        }

        var submission = new PublishedFormSubmission
        {
            FormId = "bridge check",
            RequestJson = Frozen,
            Files = evidence,
            CreateUploadDeadline = Deadline
        };
        var first = await client.SubmitAsync(submission);
        var replay = await client.SubmitAsync(submission);

        Assert.Equal(2, requests);
        Assert.Equal(2, opened.Count);
        Assert.All(opened, stream => Assert.False(stream.CanRead));
        Assert.False(first.IdempotentReplay);
        Assert.True(replay.IdempotentReplay);
        Assert.Equal(first.SubmissionId, replay.SubmissionId);
        Assert.Equal(19L, replay.TargetFeatureId);
        Assert.True(replay.EditOutcome?.Succeeded);
        Assert.Equal(42L, Assert.Single(replay.AttachmentOutcomes!).AttachmentId);
    }

    [Fact]
    public async Task SubmitAsync_ReadsServerValidationReceiptFromJsonRequest()
    {
        using var http = CreateHttp(async (request, ct) =>
        {
            var content = Assert.IsType<StringContent>(request.Content);
            Assert.Equal("application/json", content.Headers.ContentType?.MediaType);
            Assert.Equal(Frozen, await content.ReadAsStringAsync(ct));
            return Response(HttpStatusCode.BadRequest, """
                {"formId":"bridge","formVersion":3,"status":"rejected","attachmentOutcomes":[],
                 "validationIssues":[{"fieldId":"name","code":"required","message":"Name is required."}],"retry":{"retryable":false}}
                """);
        });

        var result = await new PublishedFormSubmissionClient(http).SubmitAsync(new() { FormId = "bridge", RequestJson = Frozen });

        Assert.Equal(HttpStatusCode.BadRequest, result.HttpStatusCode);
        Assert.Equal("rejected", result.Status);
        Assert.False(result.Retry?.Retryable);
        Assert.Equal("Name is required.", Assert.Single(result.ValidationIssues!).Message);
        Assert.Null(result.TargetFeatureId);
        Assert.Null(result.EditOutcome);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("broken")]
    [InlineData("""{"formId":"bridge","formVersion":4,"status":"accepted"}""")]
    public async Task SubmitAsync_UnconfirmedReceiptFails(string body)
    {
        using var http = CreateHttp((_, _) => Task.FromResult(Response(HttpStatusCode.OK, body)));
        await Assert.ThrowsAsync<JsonException>(() => new PublishedFormSubmissionClient(http).SubmitAsync(new() { FormId = "bridge", RequestJson = Frozen }));
    }

    [Fact]
    public async Task SubmitAsync_HttpFailurePreservesStatus()
    {
        using var http = CreateHttp((_, _) => Task.FromResult(Response(HttpStatusCode.Conflict, "{}")));
        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => new PublishedFormSubmissionClient(http).SubmitAsync(new() { FormId = "bridge", RequestJson = Frozen }));
        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
    }

    [Fact]
    public async Task SubmitAsync_RequiresSavedKeyBeforeSending()
    {
        using var http = CreateHttp((_, _) => throw new InvalidOperationException("Unexpected send."));
        await Assert.ThrowsAsync<JsonException>(() =>
            new PublishedFormSubmissionClient(http).SubmitAsync(new() { FormId = "bridge", RequestJson = """{"formVersion":3}""" }));
    }

    private static HttpClient CreateHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        => new(new Handler(send)) { BaseAddress = new Uri("https://test.invalid/") };

    private static HttpResponseMessage Response(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
