// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Single source of truth for the base-address and timeout validation shared by
/// every Honua SDK client options class. Each package routes its own
/// <c>ValidateBaseAddress</c>/<c>ValidateTimeout</c> check here with a product
/// label so the rules can never drift between packages.
/// </summary>
public static class HonuaClientOptionsValidation
{
    private static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromHours(24);

    /// <summary>
    /// Validates that a configured base address is present, absolute, and uses
    /// HTTP or HTTPS, throwing <see cref="HonuaConfigurationException"/> otherwise.
    /// </summary>
    /// <param name="baseAddress">The configured base address.</param>
    /// <param name="productLabel">Human-readable product prefix used in messages (e.g. <c>"Honua STAC"</c>).</param>
    public static void ValidateBaseAddress(Uri? baseAddress, string productLabel)
    {
        if (baseAddress is null)
        {
            throw new HonuaConfigurationException($"{productLabel} base address must be configured.");
        }

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new HonuaConfigurationException($"{productLabel} base address must be an absolute URI.");
        }

        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new HonuaConfigurationException($"{productLabel} base address must use HTTP or HTTPS.");
        }
    }

    /// <summary>
    /// Validates that a configured timeout is greater than 10 milliseconds and
    /// less than 24 hours, throwing <see cref="HonuaConfigurationException"/> otherwise.
    /// </summary>
    /// <param name="timeout">The configured timeout.</param>
    /// <param name="productLabel">Human-readable product prefix used in messages (e.g. <c>"Honua STAC"</c>).</param>
    public static void ValidateTimeout(TimeSpan timeout, string productLabel)
    {
        if (timeout <= MinTimeout || timeout >= MaxTimeout)
        {
            throw new HonuaConfigurationException($"{productLabel} timeout must be greater than 10 milliseconds and less than 24 hours.");
        }
    }
}
