// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Buffers;
using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Shared GeoServices HTTP helpers: form-POST, GET, and the GeoServices
/// <c>{ error: { code, message, details[] } }</c> envelope handling used by all
/// GeoServices clients (FeatureServer, NAServer routing, ImageServer, GeometryServer).
/// </summary>
internal static class GeoServicesHttp
{
    internal static async Task<string> GetStringAsync(
        HttpClient http, string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    internal static async Task<string> PostFormAsync(
        HttpClient http,
        string path,
        IEnumerable<(string Key, string? Value)> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!)));

        using var response = await http.PostAsync(CreateRequestUri(path), content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    internal static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    /// <summary>
    /// Reads a response body as a string without buffering more than <paramref name="maxBytes"/> bytes.
    /// A declared <c>Content-Length</c> over the ceiling is refused before the body is read; a body that
    /// streams past it is abandoned at the first chunk that would cross it. Mirrors honua-server's
    /// <c>MigrationHttpContentReader.ReadStringWithLimitAsync</c>, including charset and BOM handling.
    /// </summary>
    internal static async Task<string> ReadStringWithLimitAsync(
        HttpResponseMessage response, long maxBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes)
        {
            throw new HonuaFeatureServerResponseTooLargeException(
                response.StatusCode, response.RequestMessage?.RequestUri, maxBytes, declared);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffered = new MemoryStream();
            var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            try
            {
                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (buffered.Length + read > maxBytes)
                    {
                        throw new HonuaFeatureServerResponseTooLargeException(
                            response.StatusCode, response.RequestMessage?.RequestUri, maxBytes, declaredContentLength: null);
                    }

                    await buffered.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return DecodeString(buffered, response.Content.Headers.ContentType?.CharSet);
        }
    }

    private static string DecodeString(MemoryStream buffered, string? charSet)
    {
        var encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(charSet))
        {
            try
            {
                encoding = Encoding.GetEncoding(charSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                // Unknown charset advertised by the source; fall back to UTF-8.
            }
        }

        var text = encoding.GetString(buffered.GetBuffer(), 0, (int)buffered.Length);

        // Strip a byte-order mark, matching HttpContent.ReadAsStringAsync semantics.
        return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
    }

    internal static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            if (!string.IsNullOrWhiteSpace(body) && TryExtractGeoServicesError(body, response) is { } error)
            {
                throw error;
            }

            return;
        }

        var errorMessage = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "GeoServices request failed";
        throw new HonuaFeatureServerException(
            response.StatusCode,
            errorMessage,
            body,
            null,
            null,
            failureReceipt: Honua.Sdk.Abstractions.HonuaFailureReceiptFactory.FromHttpResponse(response, body))
        {
            RetryAfter = TryGetRetryAfter(response)
        };
    }

    private static TimeSpan? TryGetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is TimeSpan delta)
        {
            return delta;
        }

        if (retryAfter.Date is DateTimeOffset date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    internal static HonuaFeatureServerException? TryExtractGeoServicesError(string body, HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var errorElement) ||
                errorElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var message = "GeoServices returned an error.";
            int? geoServicesCode = null;
            IReadOnlyList<string>? details = null;
            var httpCode = response.StatusCode;
            if (errorElement.TryGetProperty("message", out var msgProp) &&
                msgProp.ValueKind == JsonValueKind.String)
            {
                message = msgProp.GetString() ?? message;
            }

            if (errorElement.TryGetProperty("code", out var codeProp) &&
                codeProp.TryGetInt32(out var errorCode))
            {
                geoServicesCode = errorCode;
                httpCode = MapErrorCodeToStatus(errorCode, response.StatusCode);
            }

            if (errorElement.TryGetProperty("details", out var detailsProp) &&
                detailsProp.ValueKind == JsonValueKind.Array)
            {
                var detailList = new List<string>();
                foreach (var item in detailsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        detailList.Add(item.GetString()!);
                    }
                }

                details = detailList;
            }

            return new HonuaFeatureServerException(
                httpCode,
                message,
                body,
                geoServicesCode,
                details,
                Honua.Sdk.Abstractions.HonuaFailureReceiptFactory.FromHttpResponse(response, body, geoServicesCode))
            {
                RetryAfter = TryGetRetryAfter(response)
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Maps a GeoServices <c>error.code</c> to an <see cref="HttpStatusCode"/> only when it is a
    /// valid HTTP status (100-599). GeoServices codes are an independent code space (e.g. 1000,
    /// 4001) and must not be blindly cast — doing so produces nonsensical <see cref="HttpStatusCode"/>.
    /// Out-of-range codes keep the transport status; the Esri code is still exposed via <c>GeoServicesErrorCode</c>.
    /// </summary>
    internal static HttpStatusCode MapErrorCodeToStatus(int errorCode, HttpStatusCode transportStatus)
        => errorCode is >= 100 and <= 599 ? (HttpStatusCode)errorCode : transportStatus;

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }

            if (doc.RootElement.TryGetProperty("message", out var topMsg) &&
                topMsg.ValueKind == JsonValueKind.String)
            {
                return topMsg.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
