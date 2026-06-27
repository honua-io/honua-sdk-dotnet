// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Http;

/// <summary>
/// Shared open-redirect guard for hypermedia pagination. Paginating clients (OGC API Features,
/// STAC, OGC Records, …) follow a server-supplied <c>next</c> link; a malicious or compromised
/// server could point that link at a different origin to exfiltrate credentials/headers. This is
/// the single home for that security control so a hardening change reaches every client at once.
/// </summary>
public static class NextLinkOriginValidator
{
    /// <summary>
    /// Determines whether a <c>next</c> link is safe to follow: relative links (which resolve
    /// against the same base) are always safe, and absolute links must share the base address'
    /// scheme and authority.
    /// </summary>
    /// <param name="nextLink">The server-supplied next-page link.</param>
    /// <param name="baseAddress">The client's configured base address, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the link is same-origin (or cannot be cross-origin).</returns>
    public static bool IsSameOrigin(string? nextLink, Uri? baseAddress)
    {
        if (string.IsNullOrEmpty(nextLink) || !Uri.TryCreate(nextLink, UriKind.Absolute, out var nextUri))
        {
            // Relative URLs are safe — they resolve against the same base.
            return true;
        }

        if (baseAddress is null)
        {
            return true;
        }

        return string.Equals(nextUri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(nextUri.Authority, baseAddress.Authority, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the canonical cross-origin rejection message for a rejected <c>next</c> link.
    /// </summary>
    /// <param name="nextLink">The rejected next-page link.</param>
    /// <returns>A human-readable message naming the offending authority.</returns>
    public static string CrossOriginMessage(string nextLink)
    {
        var authority = Uri.TryCreate(nextLink, UriKind.Absolute, out var nextUri) ? nextUri.Authority : nextLink;
        return $"Server returned a next-page link to a different origin ({authority}), " +
            "which may indicate an open-redirect attack. Paging stopped.";
    }
}
