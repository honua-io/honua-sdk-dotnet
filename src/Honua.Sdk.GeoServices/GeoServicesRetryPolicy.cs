// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Decides whether a GeoServices request and outcome are eligible for retry and extracts any
/// server-supplied retry delay.
/// </summary>
/// <remarks>
/// The standard resilience handler's <c>DisableForUnsafeHttpMethods</c> retries only idempotent
/// HTTP methods. That rule alone silently drops transient-retry protection from the FeatureServer
/// <c>/query</c> read whenever its URL exceeds the POST fallback threshold (long <c>where</c>
/// filters), because the read is then issued as a POST. This policy restores that protection:
/// it retries the idempotent methods <em>plus</em> the idempotent <c>/query</c> POST, honors
/// explicit <c>Honua-Retryable: false</c> headers on 503 responses, and retries HTTP 200
/// GeoServices error envelopes carrying <c>429</c>/<c>503</c> or <c>Honua-Retryable: true</c>.
/// </remarks>
internal static class GeoServicesRetryPolicy
{
    /// <summary>
    /// Returns <see langword="true"/> when a transient failure for <paramref name="request"/> may
    /// be safely retried.
    /// </summary>
    /// <param name="request">The outgoing request, or <see langword="null"/> when unavailable.</param>
    public static bool IsRetryableRequest(HttpRequestMessage? request)
    {
        // When the method cannot be determined, defer to the default transient policy (retry).
        if (request is null)
        {
            return true;
        }

        var method = request.Method;
        if (method == HttpMethod.Get
            || method == HttpMethod.Head
            || method == HttpMethod.Options
            || method == HttpMethod.Trace
            || method == HttpMethod.Put
            || method == HttpMethod.Delete)
        {
            return true;
        }

        // The GeoServices /query read falls back to POST for long filter strings; it is
        // idempotent and must remain retry-eligible. Mutations (applyEdits, attachments) do not
        // target /query and are correctly excluded.
        return method == HttpMethod.Post && IsQueryRequest(request);
    }

    public static async ValueTask<bool> ShouldRetryAsync(
        Outcome<HttpResponseMessage> outcome,
        HttpRequestMessage? request,
        CancellationToken cancellationToken = default)
    {
        if (!IsRetryableRequest(request))
        {
            return false;
        }

        if (outcome.Exception is not null)
        {
            return IsTransientOutcome(outcome);
        }

        if (outcome.Result is not { } response)
        {
            return false;
        }

        var headerRetryable = HonuaRestHttpClientRegistration.TryParseRetryableHeader(response);
        if (headerRetryable == false)
        {
            return false;
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return IsTransientOutcome(outcome);
        }

        return await ShouldRetryGeoServicesEnvelopeAsync(response, headerRetryable, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsTransientOutcome(Outcome<HttpResponseMessage> outcome)
        => HttpClientResiliencePredicates.IsTransient(outcome);

    public static async ValueTask<TimeSpan?> TryGetRetryDelayAsync(
        HttpResponseMessage? response,
        CancellationToken cancellationToken = default)
    {
        if (response is null)
        {
            return null;
        }

        var headerDelay = TryGetRetryAfterHeader(response);
        if (headerDelay.HasValue)
        {
            return headerDelay.Value;
        }

        if (response.StatusCode != HttpStatusCode.OK || !IsJsonResponse(response))
        {
            return null;
        }

        await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return TryGetEnvelopeRetryAfterSeconds(body);
    }

    private static async ValueTask<bool> ShouldRetryGeoServicesEnvelopeAsync(
        HttpResponseMessage response,
        bool? headerRetryable,
        CancellationToken cancellationToken)
    {
        if (headerRetryable != true && !IsJsonResponse(response))
        {
            return false;
        }

        await response.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return IsRetryableGeoServicesEnvelopeBody(body, headerRetryable);
    }

    private static bool IsRetryableGeoServicesEnvelopeBody(string body, bool? headerRetryable)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!TryGetErrorEnvelope(document.RootElement, out var error, out var code))
            {
                return false;
            }

            if (TryGetBoolProperty(error, "retryable", out var bodyRetryable))
            {
                return bodyRetryable;
            }

            return headerRetryable == true || code is 429 or 503;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetErrorEnvelope(JsonElement root, out JsonElement error, out int code)
    {
        error = default;
        code = 0;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("error", out var errorElement) ||
            errorElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!errorElement.TryGetProperty("code", out var codeProp) || !codeProp.TryGetInt32(out var parsedCode))
        {
            return false;
        }

        error = errorElement;
        code = parsedCode;
        return true;
    }

    private static bool TryGetBoolProperty(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var prop) ||
            prop.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = prop.GetBoolean();
        return true;
    }

    private static TimeSpan? TryGetRetryAfterHeader(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is TimeSpan delta)
        {
            return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
        }

        if (retryAfter.Date is DateTimeOffset date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    private static TimeSpan? TryGetEnvelopeRetryAfterSeconds(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!TryGetErrorEnvelope(document.RootElement, out var error, out _))
            {
                return null;
            }

            if (error.TryGetProperty("retryAfterSeconds", out var secondsProp) &&
                secondsProp.ValueKind == JsonValueKind.Number &&
                secondsProp.TryGetDouble(out var seconds) &&
                double.IsFinite(seconds) &&
                seconds >= 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsJsonResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQueryRequest(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri is null)
        {
            return false;
        }

        var path = uri.IsAbsoluteUri ? uri.AbsolutePath : uri.OriginalString;
        var queryStart = path.IndexOf('?', StringComparison.Ordinal);
        if (queryStart >= 0)
        {
            path = path[..queryStart];
        }

        path = path.TrimEnd('/');
        return path.EndsWith("/query", StringComparison.OrdinalIgnoreCase);
    }
}
