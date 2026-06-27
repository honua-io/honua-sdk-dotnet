// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Decides whether a GeoServices request is safe to retry on a transient failure.
/// </summary>
/// <remarks>
/// The standard resilience handler's <c>DisableForUnsafeHttpMethods</c> retries only idempotent
/// HTTP methods. That rule alone silently drops transient-retry protection from the FeatureServer
/// <c>/query</c> read whenever its URL exceeds the POST fallback threshold (long <c>where</c>
/// filters), because the read is then issued as a POST. This policy restores that protection:
/// it retries the idempotent methods <em>plus</em> the idempotent <c>/query</c> POST, while still
/// excluding genuine mutations such as <c>applyEdits</c>. Retry behaviour therefore no longer
/// changes with filter length.
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
