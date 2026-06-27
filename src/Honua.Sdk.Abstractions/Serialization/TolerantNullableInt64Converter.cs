// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Serialization;

/// <summary>
/// A tolerant <see cref="long"/>? converter for fields such as <c>numberMatched</c>
/// that some OGC drafts and servers emit as the string <c>"unknown"</c> (or another
/// non-numeric token) instead of a number. Numeric values parse to a <see cref="long"/>;
/// numeric strings parse to a <see cref="long"/>; <c>null</c>, <c>"unknown"</c>, and any
/// other non-numeric value deserialize to <c>null</c> rather than throwing.
/// </summary>
public sealed class TolerantNullableInt64Converter : JsonConverter<long?>
{
    /// <inheritdoc />
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var number) ? number : null;
            case JsonTokenType.String:
                var text = reader.GetString();
                return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : null;
            default:
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.HasValue)
        {
            writer.WriteNumberValue(value.Value);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
