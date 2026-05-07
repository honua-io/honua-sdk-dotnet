// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Default feature source capability sets for protocol clients implemented by this SDK.
/// </summary>
public static class FeatureProtocolCapabilities
{
    private static readonly Dictionary<string, IReadOnlyList<string>> Defaults =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [FeatureProtocolIds.Grpc] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryAggregate,
                FeatureCapabilities.QueryExtent,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.ApplyEdits,
                FeatureCapabilities.Stream
            ],
            [FeatureProtocolIds.GeoServicesFeatureService] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryAggregate,
                FeatureCapabilities.QueryExtent,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.QueryRelated,
                FeatureCapabilities.ApplyEdits,
                FeatureCapabilities.Attachments,
                FeatureCapabilities.Sql,
                FeatureCapabilities.Stream,
                FeatureCapabilities.Pbf,
                FeatureCapabilities.Connect
            ],
            [FeatureProtocolIds.GeoServicesMapService] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryAggregate,
                FeatureCapabilities.QueryExtent,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.QueryRelated,
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles,
                FeatureCapabilities.Sql,
                FeatureCapabilities.Stream
            ],
            [FeatureProtocolIds.GeoServicesImageService] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryExtent,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.Image,
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles,
                FeatureCapabilities.Connect
            ],
            [FeatureProtocolIds.GeoServicesGeometryService] =
            [
                FeatureCapabilities.Geometry,
                FeatureCapabilities.Connect
            ],
            [FeatureProtocolIds.GeoServicesGpService] =
            [
                FeatureCapabilities.Geoprocess,
                FeatureCapabilities.Connect
            ],
            [FeatureProtocolIds.OgcFeatures] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.ApplyEdits,
                FeatureCapabilities.Stream
            ],
            [FeatureProtocolIds.OgcTiles] =
            [
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles
            ],
            [FeatureProtocolIds.OgcMaps] =
            [
                FeatureCapabilities.Render
            ],
            [FeatureProtocolIds.Stac] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.Stream
            ],
            [FeatureProtocolIds.Wfs] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryExtent,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.ApplyEdits,
                FeatureCapabilities.Stream
            ],
            [FeatureProtocolIds.Wms] =
            [
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles,
                FeatureCapabilities.Query
            ],
            [FeatureProtocolIds.Wmts] =
            [
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles
            ],
            [FeatureProtocolIds.OData] =
            [
                FeatureCapabilities.Query,
                FeatureCapabilities.QueryObjectIds,
                FeatureCapabilities.Stream,
                FeatureCapabilities.ApplyEdits
            ],
            [FeatureProtocolIds.MapLibreVector] =
            [
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles
            ],
            [FeatureProtocolIds.MapLibreRaster] =
            [
                FeatureCapabilities.Render,
                FeatureCapabilities.Tiles
            ],
            [FeatureProtocolIds.MapLibreGeoJson] =
            [
                FeatureCapabilities.Render
            ]
        };

    /// <summary>
    /// Gets the default capabilities for a protocol identifier or alias.
    /// </summary>
    /// <param name="protocolId">Protocol identifier or alias.</param>
    /// <returns>Default capability identifiers. Unknown protocols return an empty list.</returns>
    public static IReadOnlyList<string> DefaultsFor(string protocolId)
    {
        var canonical = FeatureProtocolIds.Normalize(protocolId);
        return Defaults.TryGetValue(canonical, out var capabilities)
            ? capabilities
            : [];
    }

    /// <summary>
    /// Intersects capability collections with canonical string comparison.
    /// </summary>
    /// <param name="participants">Capability collections to intersect.</param>
    /// <returns>Capabilities supported by every participant.</returns>
    public static IReadOnlyList<string> Intersect(params IEnumerable<string>[] participants)
    {
        ArgumentNullException.ThrowIfNull(participants);
        if (participants.Length == 0)
        {
            return [];
        }

        var result = new HashSet<string>(
            participants[0].Select(FeatureCapabilities.Normalize),
            StringComparer.Ordinal);
        foreach (var participant in participants.Skip(1))
        {
            result.IntersectWith(participant.Select(FeatureCapabilities.Normalize));
        }

        return FeatureCapabilities.All.Where(result.Contains).ToList();
    }

    /// <summary>
    /// Unions capability collections with canonical declaration order.
    /// </summary>
    /// <param name="participants">Capability collections to union.</param>
    /// <returns>Capabilities supported by any participant.</returns>
    public static IReadOnlyList<string> Union(params IEnumerable<string>[] participants)
    {
        ArgumentNullException.ThrowIfNull(participants);

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var participant in participants)
        {
            result.UnionWith(participant.Select(FeatureCapabilities.Normalize));
        }

        return FeatureCapabilities.All.Where(result.Contains).ToList();
    }
}
