// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Field.Forms;

/// <summary>
/// Creates form contracts from SDK source schema metadata.
/// </summary>
public static class FieldFormSchemaMapper
{
    /// <summary>
    /// Creates a single-section form from a source descriptor.
    /// </summary>
    /// <param name="source">Source descriptor with optional schema.</param>
    /// <param name="options">Optional mapping settings.</param>
    /// <returns>A form definition aligned with the source schema.</returns>
    public static FormDefinition CreateForm(SourceDescriptor source, FieldFormSchemaMapperOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var resolvedOptions = options ?? new FieldFormSchemaMapperOptions();
        var fields = (source.Schema?.Fields ?? [])
            .Where(field => IncludeField(source.Schema, field, resolvedOptions))
            .Select(MapField)
            .ToArray();

        return new FormDefinition
        {
            FormId = resolvedOptions.FormId ?? source.Id,
            Name = resolvedOptions.Name ?? source.Id,
            Version = resolvedOptions.Version,
            Description = resolvedOptions.Description,
            Target = new FormTarget
            {
                SourceId = source.Id,
                ServiceId = source.Locator.ServiceId,
                LayerId = source.Locator.LayerId,
                CollectionId = source.Locator.CollectionId,
                TypeName = source.Locator.TypeName,
            },
            Sections =
            [
                new FormSection
                {
                    SectionId = resolvedOptions.SectionId,
                    Label = resolvedOptions.SectionLabel,
                    Fields = fields,
                },
            ],
        };
    }

    private static bool IncludeField(SourceSchema? schema, SourceField field, FieldFormSchemaMapperOptions options)
    {
        if (field.Editable == false && !options.IncludeReadOnlyFields)
        {
            return false;
        }

        if (!options.IncludeSystemFields &&
            (string.Equals(field.Name, schema?.ObjectIdField, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(field.Name, schema?.GlobalIdField, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private static FormField MapField(SourceField sourceField)
    {
        var choices = ReadChoices(sourceField.Domain);

        return new FormField
        {
            FieldId = sourceField.Name,
            SourceFieldName = sourceField.Name,
            Label = sourceField.Alias ?? sourceField.Name,
            Type = choices.Count > 0 ? FormFieldType.SingleChoice : MapFieldType(sourceField.Type),
            Required = sourceField.Required == true || sourceField.Nullable == false,
            Choices = choices,
            Validation = new FieldValidationRule
            {
                MaxLength = sourceField.Length,
            },
        };
    }

    private static FormFieldType MapFieldType(string? sourceType)
    {
        var normalized = (sourceType ?? string.Empty)
            .Replace("esriFieldType", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Edm.", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToUpperInvariant();

        return normalized switch
        {
            "INTEGER" or "SMALLINTEGER" or "OID" or "SINGLE" or "DOUBLE" or "DECIMAL" or "INT16" or "INT32" or "INT64" => FormFieldType.Numeric,
            "DATE" => FormFieldType.DateTime,
            "BOOLEAN" or "BOOL" => FormFieldType.YesNo,
            "BLOB" or "BINARY" => FormFieldType.File,
            _ => FormFieldType.Text,
        };
    }

    private static List<FieldChoice> ReadChoices(JsonElement? domain)
    {
        if (domain is not { ValueKind: JsonValueKind.Object } domainElement ||
            !domainElement.TryGetProperty("codedValues", out var codedValues) ||
            codedValues.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var choices = new List<FieldChoice>();
        foreach (var codedValue in codedValues.EnumerateArray())
        {
            if (codedValue.ValueKind != JsonValueKind.Object ||
                !codedValue.TryGetProperty("code", out var code))
            {
                continue;
            }

            var label = codedValue.TryGetProperty("name", out var name)
                ? ConvertJsonElementToString(name)
                : null;

            choices.Add(new FieldChoice
            {
                Value = ConvertJsonElementToString(code),
                Label = label,
            });
        }

        return choices;
    }

    private static string ConvertJsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            _ => element.GetRawText(),
        };
    }
}

/// <summary>
/// Options for creating forms from source schemas.
/// </summary>
public sealed record FieldFormSchemaMapperOptions
{
    /// <summary>Override form identifier.</summary>
    public string? FormId { get; init; }

    /// <summary>Override form name.</summary>
    public string? Name { get; init; }

    /// <summary>Optional form version.</summary>
    public string? Version { get; init; }

    /// <summary>Optional form description.</summary>
    public string? Description { get; init; }

    /// <summary>Identifier for the generated section.</summary>
    public string SectionId { get; init; } = "main";

    /// <summary>Label for the generated section.</summary>
    public string SectionLabel { get; init; } = "Main";

    /// <summary>Whether read-only source fields should be included.</summary>
    public bool IncludeReadOnlyFields { get; init; }

    /// <summary>Whether object ID and global ID fields should be included.</summary>
    public bool IncludeSystemFields { get; init; }
}
