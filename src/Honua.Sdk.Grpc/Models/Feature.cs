// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using Nts = NetTopologySuite.Geometries;

namespace Honua.Sdk.Grpc.Models;

/// <summary>
/// A single geographic feature.
/// </summary>
public sealed class Feature
{
    /// <summary>Feature ID.</summary>
    public long Id { get; init; }

    /// <summary>Feature attributes as key-value pairs.</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    /// <summary>Feature geometry as a GeoServices JSON dictionary.</summary>
    public IReadOnlyDictionary<string, object?>? Geometry { get; init; }

    /// <summary>NetTopologySuite geometry carried with the feature; preferred over parsing the GeoServices JSON dictionary.</summary>
    public Nts.Geometry? NtsGeometry { get; init; }

    /// <summary>Returns the feature geometry as a NetTopologySuite geometry, or null when the feature has none.</summary>
    public Nts.Geometry? ToGeometry()
    {
        if (NtsGeometry is not null)
        {
            return NtsGeometry;
        }

        if (Geometry is null)
        {
            return null;
        }

        var json = System.Text.Json.JsonSerializer.SerializeToElement(Geometry);
        return GeoServicesGeometryConverter.ReadGeometry(json);
    }

    /// <summary>Creates a feature from a NetTopologySuite geometry so the write path skips the dictionary round-trip.</summary>
    public static Feature WithGeometry(
        Nts.Geometry geometry,
        IReadOnlyDictionary<string, object?>? attributes = null,
        long id = 0,
        HonuaSpatialReference? spatialReference = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var geoServicesJson = GeoServicesGeometryConverter.WriteGeometry(geometry, spatialReference);
        return new Feature
        {
            Id = id,
            Attributes = attributes ?? new Dictionary<string, object?>(),
            NtsGeometry = geometry,
            Geometry = UnwrapGeometryDictionary(geoServicesJson),
        };
    }

    private static IReadOnlyDictionary<string, object?>? UnwrapGeometryDictionary(System.Text.Json.JsonElement element)
    {
        return Conversion.ProtoAdapter.UnwrapGeometryJsonValue(element) as IReadOnlyDictionary<string, object?>;
    }
}
