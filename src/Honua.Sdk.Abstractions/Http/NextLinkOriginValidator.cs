// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Http;

/// <summary>
/// Shared open-redirect guard for hypermedia pagination. Paginating clients (OGC API Features,
/// STAC, OGC Records, …) follow a server-supplied <c>next</c> link; a malicious or compromised
/// server could point that link at a different origin to exfiltrate credentials/headers. This is
/// the single home for that security control so a hardening change reaches every client at once.
/// </summary>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public static class NextLinkOriginValidator
{
    /// <summary>
    /// Determines whether a <c>next</c> link is safe to follow: path-relative links (which resolve
    /// against the same authority) are always safe, and any link that resolves to a different
    /// scheme or authority than the base address is rejected.
    /// </summary>
    /// <param name="nextLink">The server-supplied next-page link.</param>
    /// <param name="baseAddress">The client's configured base address, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the link is same-origin (or cannot be cross-origin).</returns>
    public static bool IsSameOrigin(string? nextLink, Uri? baseAddress)
    {
        if (string.IsNullOrEmpty(nextLink))
        {
            return true;
        }

        // Network-path (protocol-relative) references such as "//evil.example/steal" do NOT parse
        // as UriKind.Absolute, yet per RFC 3986 they resolve against the base by inheriting only
        // the scheme and REPLACING the authority — i.e. they are cross-origin-capable. Resolving
        // the link against the base address (the same resolution HttpClient performs) is the only
        // reliable way to classify it; a bare scheme/authority string-compare on the raw link
        // misclassifies "//host" as relative and a path-absolute "/items" as a Unix file: URI.
        if (baseAddress is null)
        {
            // Without a base we cannot compare origins; only a self-describing absolute HTTP(S)
            // authority could be cross-origin, and there is nothing to compare it against.
            return true;
        }

        if (!Uri.TryCreate(nextLink, UriKind.RelativeOrAbsolute, out var candidate)
            || !Uri.TryCreate(baseAddress, candidate, out var resolved))
        {
            // Unparseable links are conservatively rejected so paging stops rather than
            // following an ambiguous reference.
            return false;
        }

        // Only HTTP(S) origins are meaningful for this guard; a resolved non-HTTP(S) scheme
        // (e.g. a path-relative reference resolved against an http base stays http) cannot widen
        // the authority. If resolution somehow produced a non-HTTP(S) scheme, treat it as unsafe.
        if (!string.Equals(resolved.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolved.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(resolved.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(resolved.Authority, baseAddress.Authority, StringComparison.OrdinalIgnoreCase);
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
