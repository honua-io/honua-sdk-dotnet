// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// Maps authoring value types and artifact kinds onto OGC API Processes JSON Schema
/// fragments, matching the shapes the Honua Server emits in its process descriptions.
/// </summary>
internal static class HonuaProcessSchemaFactory
{
    private const string WkbMediaType = "application/wkb";

    /// <summary>
    /// Builds the JSON Schema fragment for an input parameter value type.
    /// </summary>
    /// <param name="valueType">The parameter value type.</param>
    /// <returns>The schema fragment.</returns>
    public static HonuaProcessIoSchema ForValueType(HonuaProcessParameterValueType valueType) => valueType switch
    {
        HonuaProcessParameterValueType.Text => new HonuaProcessIoSchema { Type = "string" },
        HonuaProcessParameterValueType.WholeNumber => new HonuaProcessIoSchema { Type = "integer" },
        HonuaProcessParameterValueType.FloatingPoint => new HonuaProcessIoSchema { Type = "number" },
        HonuaProcessParameterValueType.Flag => new HonuaProcessIoSchema { Type = "boolean" },
        HonuaProcessParameterValueType.Wkb => new HonuaProcessIoSchema { Type = "string", ContentMediaType = WkbMediaType },
        HonuaProcessParameterValueType.WkbArray => new HonuaProcessIoSchema { Type = "array", ContentMediaType = WkbMediaType },
        HonuaProcessParameterValueType.Srid => new HonuaProcessIoSchema { Type = "integer" },
        HonuaProcessParameterValueType.LayerId => new HonuaProcessIoSchema { Type = "string" },
        _ => new HonuaProcessIoSchema { Type = "string" }
    };

    /// <summary>
    /// Builds the JSON Schema fragment for an output artifact kind.
    /// </summary>
    /// <param name="artifactKind">The artifact kind.</param>
    /// <returns>The schema fragment.</returns>
    public static HonuaProcessIoSchema ForArtifactKind(HonuaProcessArtifactKind artifactKind) => artifactKind switch
    {
        HonuaProcessArtifactKind.Scalar => new HonuaProcessIoSchema { Type = "string" },
        _ => new HonuaProcessIoSchema { Type = "object" }
    };
}
