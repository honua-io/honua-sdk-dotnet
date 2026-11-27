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

    /// <summary>Submits the original JSON and opens fresh streams for each supplied evidence part.</summary>
    /// <param name="formId">The published form identifier.</param>
    /// <param name="requestJson">The saved JSON envelope with a positive form version and stable idempotency key.</param>
    /// <param name="files">Named evidence parts. Replays must reference the same bytes.</param>
    /// <param name="createUploadDeadline">Optional multipart deadline factory receiving the complete length in bytes.
    /// Link the caller cancellation token when creating the deadline. Streams must be seekable when supplied.</param>
    /// <param name="cancellationToken">Cancellation for sending and reading the receipt.</param>
    public async Task<PublishedFormSubmissionReceipt> SubmitAsync(
        string formId,
        string requestJson,
        IReadOnlyList<PublishedFormPart> files,
        Func<long, CancellationTokenSource>? createUploadDeadline = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        ArgumentNullException.ThrowIfNull(files);
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

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/forms/{Uri.EscapeDataString(formId)}/submissions");
        var json = new StringContent(requestJson, Encoding.UTF8, "application/json");
        request.Content = json;
        if (files.Count > 0)
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(json, "submission");
            request.Content = multipart;
            foreach (var file in files)
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

        using var deadline = files.Count > 0 && createUploadDeadline is not null
            ? createUploadDeadline(request.Content.Headers.ContentLength
                ?? throw new ArgumentException("Multipart length is required to create an upload deadline.", nameof(files)))
            : null;
        var requestToken = deadline?.Token ?? cancellationToken;
        using var response = await _httpClient.SendAsync(request, requestToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            response.EnsureSuccessStatusCode();
        }

        var receipt = await response.Content.ReadFromJsonAsync(
            PublishedFormReceiptJsonContext.Default.PublishedFormSubmissionReceipt, requestToken).ConfigureAwait(false);
        if (receipt is null || receipt.FormId != formId || receipt.FormVersion != formVersion
            || receipt.Status is not ("accepted" or "rejected" or "failed")
            || (response.StatusCode == HttpStatusCode.BadRequest && receipt.Status == "accepted"))
        {
            throw new JsonException("The response is not a receipt for the submitted form version.");
        }

        receipt.HttpStatusCode = response.StatusCode;
        return receipt;
    }
}

/// <summary>An evidence part with an opener that returns a new readable stream for every request.</summary>
/// <param name="PartName">The multipart name referenced by the saved submission envelope.</param>
/// <param name="FileName">The filename sent with this part.</param>
/// <param name="ContentType">The media type sent with this part.</param>
/// <param name="OpenRead">Opens a fresh stream that the submission client disposes.</param>
public sealed record PublishedFormPart(string PartName, string FileName, string ContentType, Func<Stream> OpenRead);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PublishedFormSubmissionReceipt))]
internal sealed partial class PublishedFormReceiptJsonContext : JsonSerializerContext;
