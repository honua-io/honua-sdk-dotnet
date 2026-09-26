// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// The ArcGIS REST service type a <see cref="HonuaFeatureServerClient"/> addresses.
/// </summary>
public enum ArcGisServiceType
{
    /// <summary>A <c>FeatureServer</c> service (the default).</summary>
    FeatureServer,

    /// <summary>A <c>MapServer</c> service; layer metadata, query and attachment reads use the same layer paths.</summary>
    MapServer,
}

/// <summary>
/// Addressing and response-size options for <see cref="HonuaFeatureServerClient"/>. The defaults address a
/// Honua server (<c>{base}/rest/services/{serviceId}/FeatureServer</c>); set these to read an arbitrary
/// ArcGIS source, or build them from a service root URL with <see cref="ArcGisServiceRoot.ToClientOptions"/>.
/// </summary>
public sealed class HonuaFeatureServerClientOptions
{
    /// <summary>
    /// Default response body ceiling in bytes (64 MiB). Matches honua-server's
    /// <c>MigrationHttpContentReader.DefaultMaxResponseBytes</c> for operator-supplied ArcGIS sources.
    /// </summary>
    public const long DefaultMaxResponseBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Absolute root every request resolves under, including any path prefix (for example
    /// <c>https://services.arcgis.com/Org123/arcgis/</c>). When null, the <see cref="HttpClient.BaseAddress"/>
    /// is used. In both cases the path prefix is preserved whether or not it ends with <c>/</c>.
    /// </summary>
    public Uri? RootAddress { get; set; }

    /// <summary>
    /// Path between the root and the service id. Defaults to <c>rest/services</c>; set to an empty string when
    /// the root already ends at the services directory.
    /// </summary>
    public string ServicesPath { get; set; } = "rest/services";

    /// <summary>The service type segment appended after the service id. Defaults to <see cref="ArcGisServiceType.FeatureServer"/>.</summary>
    public ArcGisServiceType ServiceType { get; set; } = ArcGisServiceType.FeatureServer;

    /// <summary>
    /// Maximum number of bytes read from a JSON or error response body. A larger body fails with
    /// <see cref="Exceptions.HonuaFeatureServerResponseTooLargeException"/> without being fully buffered.
    /// Defaults to <see cref="DefaultMaxResponseBytes"/>.
    /// </summary>
    public long MaxResponseBytes { get; set; } = DefaultMaxResponseBytes;
}
