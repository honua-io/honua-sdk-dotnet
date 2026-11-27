// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Field.Forms;

/// <summary>Submits saved, versioned published-form records to Honua Server.</summary>
public sealed class PublishedFormSubmissionClient
{
    private readonly HttpClient _httpClient;

    /// <summary>Uses a caller-configured Honua Server HTTP client.</summary>
    public PublishedFormSubmissionClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>Submits the original JSON and opens fresh streams for each evidence part.</summary>
    public async Task<PublishedFormSubmissionReceipt> SubmitAsync(
        PublishedFormSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.FormId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.RequestJson);
        ArgumentNullException.ThrowIfNull(submission.Files);
        var formVersion = ReadSavedVersion(submission.RequestJson);
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri($"api/v1/forms/{Uri.EscapeDataString(submission.FormId)}/submissions", UriKind.Relative));
        SetRequestContent(request, submission);

        using var deadline = submission.Files.Count > 0 && submission.CreateUploadDeadline is not null
            ? submission.CreateUploadDeadline(request.Content!.Headers.ContentLength
                ?? throw new ArgumentException("Multipart length is required to create an upload deadline.", nameof(submission)))
            : null;
        using var linkedCancellation = deadline is null
            ? null
            : CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        var requestToken = linkedCancellation?.Token ?? cancellationToken;
        using var response = await _httpClient.SendAsync(request, requestToken).ConfigureAwait(false);
        var receipt = await ReadReceiptAsync(response, requestToken).ConfigureAwait(false);
        if (receipt.FormId != submission.FormId || receipt.FormVersion != formVersion)
        {
            throw new JsonException("The response is not a receipt for the submitted form version.");
        }

        return receipt;
    }

    private static async Task<PublishedFormSubmissionReceipt> ReadReceiptAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            response.EnsureSuccessStatusCode();
        }

        var receipt = await response.Content.ReadFromJsonAsync(
            PublishedFormReceiptJsonContext.Default.PublishedFormSubmissionReceipt, cancellationToken).ConfigureAwait(false);
        if (receipt is null || receipt.Status is not ("accepted" or "rejected" or "failed")
            || (response.StatusCode == HttpStatusCode.BadRequest && receipt.Status == "accepted"))
        {
            throw new JsonException("The response is not a receipt for the submitted form version.");
        }

        receipt.HttpStatusCode = response.StatusCode;
        return receipt;
    }

    private static int ReadSavedVersion(string requestJson)
    {
        using var envelope = JsonDocument.Parse(requestJson);
        var body = envelope.RootElement;
        if (body.ValueKind != JsonValueKind.Object
            || !body.TryGetProperty("formVersion", out var version) || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out var formVersion) || formVersion <= 0
            || !body.TryGetProperty("idempotencyKey", out var key) || key.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(key.GetString()))
        {
            throw new JsonException("The saved form submission must specify its form version and idempotency key.");
        }

        return formVersion;
    }

    private static void SetRequestContent(HttpRequestMessage request, PublishedFormSubmission submission)
    {
        var json = new StringContent(submission.RequestJson, Encoding.UTF8, "application/json");
        request.Content = json;
        if (submission.Files.Count > 0)
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(json, "submission");
            request.Content = multipart;
            foreach (var file in submission.Files)
            {
                ArgumentNullException.ThrowIfNull(file);
                var contentType = MediaTypeHeaderValue.Parse(file.ContentType);
                // MultipartFormDataContent owns the content after Add succeeds.
#pragma warning disable CA2000
                var content = new StreamContent(file.OpenRead());
#pragma warning restore CA2000
                try
                {
                    content.Headers.ContentType = contentType;
                    multipart.Add(content, file.PartName, file.FileName);
                }
                catch
                {
                    content.Dispose();
                    throw;
                }
            }
        }
    }
}

/// <summary>A saved submission and its replayable evidence.</summary>
public sealed class PublishedFormSubmission
{
    /// <summary>The published form identifier.</summary>
    public required string FormId { get; init; }

    /// <summary>The unchanged saved JSON envelope with its positive version and stable idempotency key.</summary>
    public required string RequestJson { get; init; }

    /// <summary>Named evidence parts whose bytes must remain unchanged across retries.</summary>
    public IReadOnlyList<PublishedFormPart> Files { get; init; } = [];

    /// <summary>Creates an optional upload deadline from the complete multipart length, requiring seekable streams.</summary>
    public Func<long, CancellationTokenSource>? CreateUploadDeadline { get; init; }
}

/// <summary>An evidence part whose opener returns a fresh stream owned and disposed by the client.</summary>
public sealed record PublishedFormPart(string PartName, string FileName, string ContentType, Func<Stream> OpenRead);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PublishedFormSubmissionReceipt))]
internal sealed partial class PublishedFormReceiptJsonContext : JsonSerializerContext;
