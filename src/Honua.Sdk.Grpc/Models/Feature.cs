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

    /// <summary>Feature geometry as an Esri JSON (GeoServices) dictionary.</summary>
    public IReadOnlyDictionary<string, object?>? Geometry { get; init; }

    /// <summary>
    /// NetTopologySuite geometry carried alongside the feature when it was produced from a gRPC
    /// proto response or constructed via <see cref="WithGeometry"/>. When set, this is the source
    /// of truth used by <see cref="ToGeometry"/> and avoids re-parsing the Esri JSON dictionary.
    /// </summary>
    public Nts.Geometry? NtsGeometry { get; init; }

    /// <summary>
    /// Returns the feature geometry as a NetTopologySuite <see cref="Nts.Geometry"/>.
    /// </summary>
    /// <remarks>
    /// Prefers the geometry carried in <see cref="NtsGeometry"/> (the gRPC proto-native shape).
    /// When only the Esri JSON dictionary is available it is parsed through the GeoServices
    /// converter, preserving Z/M ordinates and Esri ring orientation.
    /// </remarks>
    /// <returns>The NTS geometry, or <see langword="null"/> when the feature has no geometry.</returns>
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

    /// <summary>
    /// Creates a feature from a NetTopologySuite geometry, carrying the NTS shape natively so the
    /// write path converts straight to the gRPC proto without a dictionary round-trip.
    /// </summary>
    /// <param name="geometry">The NTS geometry.</param>
    /// <param name="attributes">Optional feature attributes.</param>
    /// <param name="id">Optional feature identifier (required for updates).</param>
    /// <param name="spatialReference">
    /// Optional spatial reference emitted on the Esri JSON projection of the geometry.
    /// </param>
    /// <returns>A feature whose <see cref="NtsGeometry"/> and <see cref="Geometry"/> describe the shape.</returns>
    public static Feature WithGeometry(
        Nts.Geometry geometry,
        IReadOnlyDictionary<string, object?>? attributes = null,
        long id = 0,
        HonuaSpatialReference? spatialReference = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var esriJson = GeoServicesGeometryConverter.WriteGeometry(geometry, spatialReference);
        return new Feature
        {
            Id = id,
            Attributes = attributes ?? new Dictionary<string, object?>(),
            NtsGeometry = geometry,
            Geometry = UnwrapGeometryDictionary(esriJson),
        };
    }

    private static IReadOnlyDictionary<string, object?>? UnwrapGeometryDictionary(System.Text.Json.JsonElement element)
    {
        return Conversion.ProtoAdapter.UnwrapGeometryJsonValue(element) as IReadOnlyDictionary<string, object?>;
    }
}
