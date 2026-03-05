// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin;

/// <summary>
/// Configuration options for the Honua Admin client.
/// </summary>
public sealed class HonuaAdminClientOptions
{
    /// <summary>
    /// Base address of the Honua server.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://localhost:5001");

    /// <summary>
    /// API key for admin authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Bearer token for authentication.
    /// </summary>
    public string? BearerToken { get; set; }

    internal static void ValidateBaseAddress(Uri? baseAddress)
    {
        if (baseAddress is null)
        {
            throw new InvalidOperationException("Honua admin base address must be configured.");
        }

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Honua admin base address must be an absolute URI.");
        }

        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Honua admin base address must use HTTP or HTTPS.");
        }
    }

    internal static bool RequiresHttpsForAuthentication(Uri? uri)
    {
        if (uri is null)
        {
            return true;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsLocalDevelopmentHttp(uri);
    }

    internal static bool IsLocalDevelopmentHttp(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.IsLoopback ||
               string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }
}
