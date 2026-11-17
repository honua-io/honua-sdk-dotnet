// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>Distinguishes an omitted string from an explicitly supplied string or null.</summary>
[JsonConverter(typeof(OptionalStringJsonConverter))]
public readonly record struct OptionalString
{
    /// <summary>Creates an explicit string value; null remains an explicit null on the wire.</summary>
    public OptionalString(string? value)
    {
        Value = value;
        IsSpecified = true;
    }

    /// <summary>The supplied string or explicit null.</summary>
    public string? Value { get; }

    /// <summary>Whether a value was explicitly supplied.</summary>
    public bool IsSpecified { get; }
}

internal sealed class OptionalStringJsonConverter : JsonConverter<OptionalString>
{
    public override bool HandleNull => true;

    public override OptionalString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType is JsonTokenType.String or JsonTokenType.Null
            ? new(reader.GetString()) : throw new JsonException("Expected a string or null.");

    public override void Write(Utf8JsonWriter writer, OptionalString value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
