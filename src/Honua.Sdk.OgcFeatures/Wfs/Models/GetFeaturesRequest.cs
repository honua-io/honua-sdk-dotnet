// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.OgcFeatures.Wfs.Models;

/// <summary>
/// Parameters for a WFS GetFeature request.
/// </summary>
public sealed record GetFeaturesRequest
{
    /// <summary>Qualified feature type name(s).</summary>
    public required string TypeNames { get; init; }

    /// <summary>Maximum number of features to return per page.</summary>
    public int? Count { get; init; }

    /// <summary>Zero-based start index for paging.</summary>
    public int? StartIndex { get; init; }

    /// <summary>Sort clause (e.g. "name ASC").</summary>
    public string? SortBy { get; init; }

    /// <summary>FES 2.0 XML filter expression (e.g. <c>&lt;fes:Filter&gt;...&lt;/fes:Filter&gt;</c>).</summary>
    public string? Filter { get; init; }

    /// <summary>Bounding box spatial filter.</summary>
    public WfsBoundingBox? Bbox { get; init; }

    /// <summary>Specific resource identifier.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Comma-separated list of property names to return.</summary>
    public string? PropertyName { get; init; }

    /// <summary>Coordinate reference system URI for the response geometry.</summary>
    public string? SrsName { get; init; }
}
