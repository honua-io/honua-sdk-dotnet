// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// Value types a Honua geoprocessing process parameter can declare. These mirror the
/// server-side <c>ProcessParameterValueType</c> contract and determine how each parameter
/// is projected into an OGC API Processes JSON Schema fragment.
/// </summary>
public enum HonuaProcessParameterValueType
{
    /// <summary>Free-form text value.</summary>
    Text,

    /// <summary>Integral whole number.</summary>
    WholeNumber,

    /// <summary>Floating-point number.</summary>
    FloatingPoint,

    /// <summary>Boolean flag.</summary>
    Flag,

    /// <summary>Well-known binary geometry encoded as a string.</summary>
    Wkb,

    /// <summary>Array of well-known binary geometries.</summary>
    WkbArray,

    /// <summary>Spatial reference identifier.</summary>
    Srid,

    /// <summary>Honua layer identifier.</summary>
    LayerId
}

/// <summary>
/// Output artifact kinds a process can declare it produces. Mirrors the server-side
/// <c>ArtifactKind</c> contract.
/// </summary>
public enum HonuaProcessArtifactKind
{
    /// <summary>A single scalar value.</summary>
    Scalar,

    /// <summary>A feature layer.</summary>
    FeatureLayer,

    /// <summary>A tabular result.</summary>
    Table,

    /// <summary>A raster dataset.</summary>
    Raster,

    /// <summary>An opaque file.</summary>
    File,

    /// <summary>A rendered report.</summary>
    Report,

    /// <summary>A rendered map.</summary>
    Map,

    /// <summary>A packaged application bundle.</summary>
    AppBundle
}
