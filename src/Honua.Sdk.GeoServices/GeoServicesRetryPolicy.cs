// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions.Http;
using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Honua.Sdk.GeoServices;

// Retries idempotent HTTP methods plus the idempotent /query POST fallback, honors
// Honua-Retryable: false on 503 responses, and retries HTTP 200 GeoServices error
// envelopes carrying 429/503 or Honua-Retryable: true.
internal static class GeoServicesRetryPolicy
{
    public static bool IsRetryableRequest(HttpRequestMessage? request)
    {
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

        return await ShouldRetryGeoServicesEnvelopeAsync(response, cancellationToken).ConfigureAwait(false);
    }

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

        if (response.StatusCode != HttpStatusCode.OK || !HonuaGeoServicesEnvelopeInspector.IsJsonResponse(response))
        {
            return null;
        }

        var envelope = await HonuaGeoServicesEnvelopeInspector.TryInspectAsync(response, cancellationToken)
            .ConfigureAwait(false);
        return envelope?.RetryAfter;
    }

    private static bool IsTransientOutcome(Outcome<HttpResponseMessage> outcome)
        => HttpClientResiliencePredicates.IsTransient(outcome);

    private static async ValueTask<bool> ShouldRetryGeoServicesEnvelopeAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var headerAllowsRetry = HonuaRestHttpClientRegistration.TryParseRetryableHeader(response) == true;
        if (!headerAllowsRetry && !HonuaGeoServicesEnvelopeInspector.IsJsonResponse(response))
        {
            return false;
        }

        var envelope = await HonuaGeoServicesEnvelopeInspector.TryInspectAsync(response, cancellationToken)
            .ConfigureAwait(false);
        if (envelope is null)
        {
            return false;
        }

        if (envelope.Retryable is { } bodyRetryable)
        {
            return bodyRetryable;
        }

        return headerAllowsRetry || envelope.Code is 429 or 503;
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
