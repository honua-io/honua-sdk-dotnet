// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Canonical feature source protocol identifiers and aliases shared across Honua SDKs.
/// </summary>
public static class FeatureProtocolIds
{
    /// <summary>Canonical gRPC FeatureService protocol identifier.</summary>
    public const string Grpc = "grpc";

    /// <summary>Canonical GeoServices Feature Service protocol identifier.</summary>
    public const string GeoServicesFeatureService = "geoservices-feature-service";

    /// <summary>Canonical GeoServices Map Service protocol identifier.</summary>
    public const string GeoServicesMapService = "geoservices-map-service";

    /// <summary>Canonical GeoServices Image Service protocol identifier.</summary>
    public const string GeoServicesImageService = "geoservices-image-service";

    /// <summary>Canonical GeoServices Geometry Service protocol identifier.</summary>
    public const string GeoServicesGeometryService = "geoservices-geometry-service";

    /// <summary>Canonical GeoServices geoprocessing service protocol identifier.</summary>
    public const string GeoServicesGpService = "geoservices-gp-service";

    /// <summary>Existing .NET GeoServices FeatureServer provider name alias.</summary>
    public const string GeoServicesFeatureServer = "geoservices-featureserver";

    /// <summary>OGC API Features protocol identifier.</summary>
    public const string OgcFeatures = "ogc-features";

    /// <summary>OGC API Tiles protocol identifier.</summary>
    public const string OgcTiles = "ogc-tiles";

    /// <summary>OGC API Maps protocol identifier.</summary>
    public const string OgcMaps = "ogc-maps";

    /// <summary>STAC API protocol identifier.</summary>
    public const string Stac = "stac";

    /// <summary>WFS protocol identifier.</summary>
    public const string Wfs = "wfs";

    /// <summary>WMS protocol identifier.</summary>
    public const string Wms = "wms";

    /// <summary>WMTS protocol identifier.</summary>
    public const string Wmts = "wmts";

    /// <summary>OData protocol identifier.</summary>
    public const string OData = "odata";

    /// <summary>MapLibre vector source protocol identifier.</summary>
    public const string MapLibreVector = "maplibre-vector";

    /// <summary>MapLibre raster source protocol identifier.</summary>
    public const string MapLibreRaster = "maplibre-raster";

    /// <summary>MapLibre GeoJSON source protocol identifier.</summary>
    public const string MapLibreGeoJson = "maplibre-geojson";

    private static readonly Dictionary<string, string> CanonicalIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Grpc] = Grpc,
            [GeoServicesFeatureService] = GeoServicesFeatureService,
            [GeoServicesFeatureServer] = GeoServicesFeatureService,
            ["featureserver"] = GeoServicesFeatureService,
            ["feature-server"] = GeoServicesFeatureService,
            ["feature-service"] = GeoServicesFeatureService,
            ["geoservices-feature-server"] = GeoServicesFeatureService,
            [GeoServicesMapService] = GeoServicesMapService,
            ["map-server"] = GeoServicesMapService,
            ["mapserver"] = GeoServicesMapService,
            [GeoServicesImageService] = GeoServicesImageService,
            ["image-server"] = GeoServicesImageService,
            ["imageserver"] = GeoServicesImageService,
            [GeoServicesGeometryService] = GeoServicesGeometryService,
            ["geometry-server"] = GeoServicesGeometryService,
            ["geometryserver"] = GeoServicesGeometryService,
            [GeoServicesGpService] = GeoServicesGpService,
            ["gp-server"] = GeoServicesGpService,
            ["gpserver"] = GeoServicesGpService,
            [OgcFeatures] = OgcFeatures,
            ["ogcapi-features"] = OgcFeatures,
            ["ogc-api-features"] = OgcFeatures,
            ["ogc_features"] = OgcFeatures,
            ["OgcFeatures"] = OgcFeatures,
            [OgcTiles] = OgcTiles,
            [OgcMaps] = OgcMaps,
            [Stac] = Stac,
            [Wfs] = Wfs,
            ["wfs-2.0"] = Wfs,
            [Wms] = Wms,
            ["wms-1.3.0"] = Wms,
            [Wmts] = Wmts,
            ["wmts-1.0.0"] = Wmts,
            [OData] = OData,
            ["odata-v4"] = OData,
            [MapLibreVector] = MapLibreVector,
            [MapLibreRaster] = MapLibreRaster,
            [MapLibreGeoJson] = MapLibreGeoJson
        };

    private static readonly Dictionary<string, IReadOnlyList<string>> AliasMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Grpc] = [Grpc],
            [GeoServicesFeatureService] =
            [
                GeoServicesFeatureService,
                GeoServicesFeatureServer,
                "featureserver",
                "feature-server",
                "feature-service",
                "FeatureServer",
                "geoservices-feature-server"
            ],
            [GeoServicesMapService] = [GeoServicesMapService, "map-server", "mapserver"],
            [GeoServicesImageService] = [GeoServicesImageService, "image-server", "imageserver"],
            [GeoServicesGeometryService] =
            [
                GeoServicesGeometryService,
                "geometry-server",
                "geometryserver"
            ],
            [GeoServicesGpService] = [GeoServicesGpService, "gp-server", "gpserver"],
            [OgcFeatures] =
            [
                OgcFeatures,
                "ogcapi-features",
                "ogc-api-features",
                "ogc_features",
                "OgcFeatures"
            ],
            [OgcTiles] = [OgcTiles],
            [OgcMaps] = [OgcMaps],
            [Stac] = [Stac],
            [Wfs] = [Wfs, "wfs-2.0"],
            [Wms] = [Wms, "wms-1.3.0"],
            [Wmts] = [Wmts, "wmts-1.0.0"],
            [OData] = [OData, "odata-v4"],
            [MapLibreVector] = [MapLibreVector],
            [MapLibreRaster] = [MapLibreRaster],
            [MapLibreGeoJson] = [MapLibreGeoJson]
        };

    /// <summary>Canonical protocol identifiers shared across Honua SDKs.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Grpc,
        GeoServicesFeatureService,
        GeoServicesMapService,
        GeoServicesImageService,
        GeoServicesGeometryService,
        GeoServicesGpService,
        OgcFeatures,
        OgcTiles,
        OgcMaps,
        Stac,
        Wfs,
        Wms,
        Wmts,
        OData,
        MapLibreVector,
        MapLibreRaster,
        MapLibreGeoJson
    ];

    /// <summary>
    /// Normalizes a protocol identifier or provider name alias to its canonical protocol identifier.
    /// </summary>
    /// <param name="protocolId">Protocol identifier or alias.</param>
    /// <returns>The canonical protocol identifier.</returns>
    public static string Normalize(string protocolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolId);

        return CanonicalIds.TryGetValue(protocolId, out var canonical)
            ? canonical
            : protocolId;
    }

    /// <summary>
    /// Returns whether two protocol identifiers refer to the same canonical protocol.
    /// </summary>
    /// <param name="left">First protocol identifier or alias.</param>
    /// <param name="right">Second protocol identifier or alias.</param>
    /// <returns><see langword="true"/> when both identifiers normalize to the same protocol.</returns>
    public static bool Matches(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    /// <summary>
    /// Returns the known aliases for a canonical protocol identifier.
    /// </summary>
    /// <param name="protocolId">Canonical protocol identifier or alias.</param>
    /// <returns>Known aliases, including the canonical identifier.</returns>
    public static IReadOnlyList<string> AliasesFor(string protocolId)
    {
        var canonical = Normalize(protocolId);
        return AliasMap.TryGetValue(canonical, out var aliases)
            ? aliases
            : [canonical];
    }
}
