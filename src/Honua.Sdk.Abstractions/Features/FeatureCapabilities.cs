// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Canonical feature source capability identifiers shared across Honua SDKs.
/// </summary>
public static class FeatureCapabilities
{
    /// <summary>Feature query support.</summary>
    public const string Query = "query";

    /// <summary>Aggregate query support.</summary>
    public const string QueryAggregate = "queryAggregate";

    /// <summary>Extent-only query support.</summary>
    public const string QueryExtent = "queryExtent";

    /// <summary>Object or feature ID query support.</summary>
    public const string QueryObjectIds = "queryObjectIds";

    /// <summary>Related-record query support.</summary>
    public const string QueryRelated = "queryRelated";

    /// <summary>Add, update, or delete feature edit support.</summary>
    public const string ApplyEdits = "applyEdits";

    /// <summary>Attachment query or edit support.</summary>
    public const string Attachments = "attachments";

    /// <summary>Map rendering support.</summary>
    public const string Render = "render";

    /// <summary>Tile access support.</summary>
    public const string Tiles = "tiles";

    /// <summary>SQL expression support.</summary>
    public const string Sql = "sql";

    /// <summary>Streaming or page iteration support.</summary>
    public const string Stream = "stream";

    /// <summary>Protocol buffer response support.</summary>
    public const string Pbf = "pbf";

    /// <summary>Connectivity or health probe support.</summary>
    public const string Connect = "connect";

    /// <summary>Image service support.</summary>
    public const string Image = "image";

    /// <summary>Geometry service support.</summary>
    public const string Geometry = "geometry";

    /// <summary>Geoprocessing service support.</summary>
    public const string Geoprocess = "geoprocess";

    /// <summary>OGC API Processes support.</summary>
    public const string Processes = "processes";

    /// <summary>All canonical capability identifiers supported by the shared vocabulary.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Query,
        QueryAggregate,
        QueryExtent,
        QueryObjectIds,
        QueryRelated,
        ApplyEdits,
        Attachments,
        Render,
        Tiles,
        Sql,
        Stream,
        Pbf,
        Connect,
        Image,
        Geometry,
        Geoprocess,
        Processes
    ];

    /// <summary>
    /// Returns whether a capability collection contains the requested canonical capability.
    /// </summary>
    /// <param name="capabilities">Capability identifiers to inspect.</param>
    /// <param name="capability">Capability identifier to find.</param>
    /// <returns><see langword="true"/> when the collection includes the capability.</returns>
    public static bool Contains(
        IEnumerable<string> capabilities,
        string capability)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);

        return capabilities.Any(value => string.Equals(value, capability, StringComparison.Ordinal));
    }
}
