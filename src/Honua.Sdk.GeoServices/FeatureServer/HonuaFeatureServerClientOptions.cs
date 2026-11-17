// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>The GeoServices REST service type a <see cref="HonuaFeatureServerClient"/> addresses.</summary>
public enum GeoServicesServiceType
{
    /// <summary>A <c>FeatureServer</c> service (the default).</summary>
    FeatureServer,

    /// <summary>A <c>MapServer</c> service; layer metadata, query and attachment reads use the same layer paths.</summary>
    MapServer,
}

/// <summary>Addressing and response-size options for <see cref="HonuaFeatureServerClient"/>; the defaults address a Honua server.</summary>
public sealed class HonuaFeatureServerClientOptions
{
    /// <summary>Default response body ceiling in bytes (64 MiB).</summary>
    public const long DefaultMaxResponseBytes = 64L * 1024 * 1024;

    /// <summary>Absolute root every request resolves under, including any path prefix; null uses the HTTP client's base address.</summary>
    public Uri? RootAddress { get; set; }

    /// <summary>
    /// Path between the root and the service id. Defaults to <c>rest/services</c>; set to an empty string when
    /// the root already ends at the services directory.
    /// </summary>
    public string ServicesPath { get; set; } = "rest/services";

    /// <summary>The service type segment appended after the service id. Defaults to <see cref="GeoServicesServiceType.FeatureServer"/>.</summary>
    public GeoServicesServiceType ServiceType { get; set; } = GeoServicesServiceType.FeatureServer;

    /// <summary>
    /// Maximum number of bytes read from a JSON or error response body. A larger body fails with
    /// <see cref="Exceptions.HonuaFeatureServerResponseTooLargeException"/> without being fully buffered.
    /// Defaults to <see cref="DefaultMaxResponseBytes"/>.
    /// </summary>
    public long MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;
}
