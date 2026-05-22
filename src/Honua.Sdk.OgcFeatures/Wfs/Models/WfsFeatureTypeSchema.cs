// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.OgcFeatures.Wfs.Models;

/// <summary>
/// Schema definition for a WFS feature type from DescribeFeatureType.
/// </summary>
public sealed class WfsFeatureTypeSchema
{
    /// <summary>Target namespace of the schema.</summary>
    public string? TargetNamespace { get; init; }

    /// <summary>Root element name.</summary>
    public string? ElementName { get; init; }

    /// <summary>Properties (fields) defined in the schema.</summary>
    public IReadOnlyList<WfsSchemaProperty> Properties { get; init; } = [];
}

/// <summary>
/// A single property within a WFS feature type schema.
/// </summary>
public sealed class WfsSchemaProperty
{
    /// <summary>Property name.</summary>
    public string Name { get; init; } = "";

    /// <summary>XSD type (e.g. "xsd:string", "xsd:int", "gml:PointPropertyType").</summary>
    public string Type { get; init; } = "";

    /// <summary>Minimum occurrences (0 = optional).</summary>
    public int MinOccurs { get; init; }

    /// <summary>Maximum occurrences (-1 = unbounded).</summary>
    public int MaxOccurs { get; init; } = 1;

    /// <summary>Whether the property is nillable.</summary>
    public bool Nillable { get; init; }
}
