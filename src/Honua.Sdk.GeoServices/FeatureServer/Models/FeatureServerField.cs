// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// A field definition from a FeatureServer layer.
/// </summary>
[JsonConverter(typeof(FeatureServerFieldConverter))]
public sealed class FeatureServerField
{
    /// <summary>The field name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The field type (e.g., "esriFieldTypeOID", "esriFieldTypeString").</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>The field alias (display name).</summary>
    public string? Alias { get; init; }

    /// <summary>
    /// Whether the field is nullable. An omitted source member deserializes to <see langword="false"/>,
    /// the same value as an explicit <c>"nullable": false</c>. Use <see cref="IsNullable"/> to tell the
    /// two apart.
    /// </summary>
    public bool Nullable { get; init; }

    /// <summary>
    /// Whether the field is nullable, preserving "not advertised" (<see langword="null"/>) as distinct
    /// from an explicit <c>"nullable": false</c>.
    /// </summary>
    public bool? IsNullable { get; init; }

    /// <summary>Maximum length for string fields.</summary>
    public int? Length { get; init; }

    /// <summary>
    /// Whether the field is editable. An omitted source member deserializes to <see langword="false"/>,
    /// the same value as an explicit <c>"editable": false</c>. Use <see cref="IsEditable"/> to tell the
    /// two apart.
    /// </summary>
    public bool Editable { get; init; }

    /// <summary>
    /// Whether the field is editable, preserving "not advertised" (<see langword="null"/>) as distinct
    /// from an explicit <c>"editable": false</c>.
    /// </summary>
    public bool? IsEditable { get; init; }

    /// <summary>The default value for the field.</summary>
    public JsonElement? DefaultValue { get; init; }

    /// <summary>The provider-native domain metadata for the field.</summary>
    public JsonElement? Domain { get; init; }

    /// <summary>
    /// Every source member not modelled by a typed property, preserved verbatim so metadata
    /// survives import without loss.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Populated by FeatureServerFieldConverter.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// Hand-written converter for <see cref="FeatureServerField"/>. <see cref="FeatureServerField.Nullable"/>
/// and <see cref="FeatureServerField.Editable"/> keep their original non-nullable wire behaviour for
/// binary compatibility with published 1.x releases, so <see cref="FeatureServerField.IsNullable"/> and
/// <see cref="FeatureServerField.IsEditable"/> cannot bind to the same <c>"nullable"</c>/<c>"editable"</c>
/// JSON members via attributes (System.Text.Json rejects two properties mapped to one member name). This
/// converter reads each member once and populates both the legacy and tri-state properties from it.
/// Adding a new <see cref="FeatureServerField"/> member requires updating both <see cref="Read"/> and
/// <see cref="Write"/> here.
/// </summary>
internal sealed class FeatureServerFieldConverter : JsonConverter<FeatureServerField>
{
    private static readonly HashSet<string> KnownMemberNames =
    [
        "name", "type", "alias", "nullable", "length", "editable", "defaultValue", "domain"
    ];

    public override FeatureServerField? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        var nullable = ReadNullableBool(root, "nullable");
        var editable = ReadNullableBool(root, "editable");

        Dictionary<string, JsonElement>? additionalProperties = null;
        foreach (var member in root.EnumerateObject())
        {
            if (KnownMemberNames.Contains(member.Name))
            {
                continue;
            }

            additionalProperties ??= [];
            additionalProperties[member.Name] = member.Value.Clone();
        }

        return new FeatureServerField
        {
            Name = root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString() ?? string.Empty
                : string.Empty,
            Type = root.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
                ? type.GetString() ?? string.Empty
                : string.Empty,
            Alias = ReadNullableString(root, "alias"),
            Nullable = nullable ?? false,
            IsNullable = nullable,
            Length = ReadNullableInt(root, "length"),
            Editable = editable ?? false,
            IsEditable = editable,
            DefaultValue = ReadRaw(root, "defaultValue"),
            Domain = ReadRaw(root, "domain"),
            AdditionalProperties = additionalProperties,
        };
    }

    public override void Write(Utf8JsonWriter writer, FeatureServerField value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("type", value.Type);
        WriteStringOrNull(writer, "alias", value.Alias);
        writer.WriteBoolean("nullable", value.Nullable);
        WriteIntOrNull(writer, "length", value.Length);
        writer.WriteBoolean("editable", value.Editable);
        WriteElementOrNull(writer, "defaultValue", value.DefaultValue);
        WriteElementOrNull(writer, "domain", value.Domain);

        if (value.AdditionalProperties is not null)
        {
            foreach (var (memberName, memberValue) in value.AdditionalProperties)
            {
                writer.WritePropertyName(memberName);
                memberValue.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteStringOrNull(Utf8JsonWriter writer, string propertyName, string? value)
    {
        writer.WritePropertyName(propertyName);
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteStringValue(value);
        }
    }

    private static void WriteIntOrNull(Utf8JsonWriter writer, string propertyName, int? value)
    {
        writer.WritePropertyName(propertyName);
        if (value is int actual)
        {
            writer.WriteNumberValue(actual);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private static void WriteElementOrNull(Utf8JsonWriter writer, string propertyName, JsonElement? value)
    {
        writer.WritePropertyName(propertyName);
        if (value is JsonElement actual)
        {
            actual.WriteTo(writer);
        }
        else
        {
            writer.WriteNullValue();
        }
    }

    private static bool? ReadNullableBool(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : null;

    private static string? ReadNullableString(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static int? ReadNullableInt(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Number
            ? element.GetInt32()
            : null;

    private static JsonElement? ReadRaw(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var element) && element.ValueKind != JsonValueKind.Null
            ? element.Clone()
            : null;
}
