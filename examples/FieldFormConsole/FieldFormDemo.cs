// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

namespace FieldFormConsole;

/// <summary>
/// Demonstrates Honua SDK field form validation and calculated-field evaluation end to end
/// using <see cref="FormValidator"/> and <see cref="CalculatedFieldEvaluator"/> against a
/// portable <see cref="FormDefinition"/>. No UI framework is involved.
/// </summary>
public static class FieldFormDemo
{
    /// <summary>
    /// Runs the form demonstration and writes a deterministic transcript.
    /// </summary>
    /// <param name="output">Transcript writer.</param>
    /// <returns>A summary of the invalid-then-valid validation passes.</returns>
    public static FieldFormDemoSummary Run(TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var form = CreateInspectionForm();

        output.WriteLine($"Honua field form demo: \"{form.Name}\" ({form.FormId})");
        output.WriteLine(
            $"Fields: {form.Sections.SelectMany(s => s.Fields).Count()} across {form.Sections.Count} section(s)");
        output.WriteLine();

        // Pass 1: a record that violates several constraints. Calculated fields run first so
        // the validator sees the computed values just as a host would before submission.
        var invalid = CreateInvalidRecord();
        CalculatedFieldEvaluator.ApplyCalculatedFields(form, invalid);
        var invalidResult = FormValidator.Validate(form, invalid);

        output.WriteLine("== Pass 1: incomplete record ==");
        WriteCalculated(output, invalid);
        WriteValidation(output, invalidResult);
        output.WriteLine();

        // Pass 2: a complete, well-formed record. Calculated fields produce the display name
        // and a numeric total, and validation passes cleanly.
        var valid = CreateValidRecord();
        CalculatedFieldEvaluator.ApplyCalculatedFields(form, valid);
        var validResult = FormValidator.Validate(form, valid);

        output.WriteLine("== Pass 2: complete record ==");
        WriteCalculated(output, valid);
        WriteValidation(output, validResult);

        return new FieldFormDemoSummary(
            InvalidErrorCount: invalidResult.Errors.Count,
            InvalidIsValid: invalidResult.IsValid,
            ValidIsValid: validResult.IsValid,
            CalculatedInspectorName: valid.Values.TryGetValue("inspectorName", out var name) ? name?.ToString() : null,
            CalculatedSampleTotal: valid.Values.TryGetValue("sampleTotal", out var total) ? total?.ToString() : null);
    }

    private static void WriteCalculated(TextWriter output, FieldRecord record)
    {
        var inspectorName = record.Values.TryGetValue("inspectorName", out var name) ? name : "(none)";
        var sampleTotal = record.Values.TryGetValue("sampleTotal", out var total) ? total : "(none)";
        output.WriteLine($"  calculated inspectorName = \"{inspectorName}\"");
        output.WriteLine($"  calculated sampleTotal   = {sampleTotal}");
    }

    private static void WriteValidation(TextWriter output, FormValidationResult result)
    {
        if (result.IsValid)
        {
            output.WriteLine("  validation: VALID (0 errors)");
            return;
        }

        output.WriteLine($"  validation: INVALID ({result.Errors.Count} error(s))");
        foreach (var error in result.Errors)
        {
            output.WriteLine($"    - [{error.FieldId}] {error.Message}");
        }
    }

    /// <summary>
    /// Builds a portable inspection form exercising required fields, numeric/range validation,
    /// regex validation, choice validation, conditional visibility, and two calculated fields.
    /// </summary>
    public static FormDefinition CreateInspectionForm()
        => new()
        {
            FormId = "tree-inspection",
            Name = "Street Tree Inspection",
            Version = "1.0",
            Sections =
            [
                new FormSection
                {
                    SectionId = "inspector",
                    Label = "Inspector",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "firstName",
                            Label = "First name",
                            Type = FormFieldType.Text,
                            Required = true,
                            Validation = new FieldValidationRule { MinLength = 2, MaxLength = 40 },
                        },
                        new FormField
                        {
                            FieldId = "lastName",
                            Label = "Last name",
                            Type = FormFieldType.Text,
                            Required = true,
                            Validation = new FieldValidationRule { MinLength = 2, MaxLength = 40 },
                        },
                        new FormField
                        {
                            FieldId = "inspectorName",
                            Label = "Inspector (computed)",
                            Type = FormFieldType.Calculated,
                            CalculatedExpression = "concat($firstName, ' ', $lastName)",
                        },
                        new FormField
                        {
                            FieldId = "contactEmail",
                            Label = "Contact email",
                            Type = FormFieldType.Text,
                            Required = true,
                            Validation = new FieldValidationRule
                            {
                                RegexPattern = "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$",
                            },
                        },
                    ],
                },
                new FormSection
                {
                    SectionId = "condition",
                    Label = "Condition",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "healthRating",
                            Label = "Health rating (1-5)",
                            Type = FormFieldType.Numeric,
                            Required = true,
                            Validation = new FieldValidationRule { MinNumericValue = 1, MaxNumericValue = 5 },
                        },
                        new FormField
                        {
                            FieldId = "species",
                            Label = "Species",
                            Type = FormFieldType.SingleChoice,
                            Required = true,
                            Choices =
                            [
                                new FieldChoice { Value = "monkeypod", Label = "Monkeypod" },
                                new FieldChoice { Value = "kukui", Label = "Kukui" },
                                new FieldChoice { Value = "plumeria", Label = "Plumeria" },
                            ],
                        },
                        new FormField
                        {
                            FieldId = "removalReason",
                            Label = "Reason for removal",
                            Type = FormFieldType.Text,
                            Required = true,
                            // Only shown (and required) when the tree is in poor health.
                            VisibilityRule = new FieldVisibilityRule
                            {
                                DependsOnFieldId = "healthRating",
                                Operator = ComparisonOperator.LessThan,
                                MatchValue = 2,
                            },
                        },
                    ],
                },
                new FormSection
                {
                    SectionId = "samples",
                    Label = "Samples",
                    Fields =
                    [
                        new FormField
                        {
                            FieldId = "leafSamples",
                            Label = "Leaf samples",
                            Type = FormFieldType.Numeric,
                            Validation = new FieldValidationRule { MinNumericValue = 0 },
                        },
                        new FormField
                        {
                            FieldId = "soilSamples",
                            Label = "Soil samples",
                            Type = FormFieldType.Numeric,
                            Validation = new FieldValidationRule { MinNumericValue = 0 },
                        },
                        new FormField
                        {
                            FieldId = "sampleTotal",
                            Label = "Total samples (computed)",
                            Type = FormFieldType.Calculated,
                            CalculatedExpression = "sum($leafSamples, $soilSamples)",
                        },
                    ],
                },
            ],
        };

    private static FieldRecord CreateInvalidRecord()
        => new()
        {
            RecordId = "rec-invalid",
            FormId = "tree-inspection",
            Values =
            {
                ["firstName"] = "A",                 // too short (MinLength 2)
                ["lastName"] = "Kealoha",
                ["contactEmail"] = "not-an-email",   // fails regex
                ["healthRating"] = 1,                // valid range, but triggers removalReason visibility
                ["species"] = "banyan",              // not an allowed choice
                // removalReason intentionally missing while visible+required
                ["leafSamples"] = 3,
                ["soilSamples"] = 2,
            },
        };

    private static FieldRecord CreateValidRecord()
        => new()
        {
            RecordId = "rec-valid",
            FormId = "tree-inspection",
            Values =
            {
                ["firstName"] = "Leilani",
                ["lastName"] = "Kealoha",
                ["contactEmail"] = "leilani@honua.io",
                ["healthRating"] = 4,                // removalReason stays hidden (>= 2)
                ["species"] = "monkeypod",
                ["leafSamples"] = 6,
                ["soilSamples"] = 4,
            },
        };
}

/// <summary>Summary of the field form demonstration.</summary>
/// <param name="InvalidErrorCount">Validation errors found on the incomplete record.</param>
/// <param name="InvalidIsValid">Whether the incomplete record passed validation.</param>
/// <param name="ValidIsValid">Whether the complete record passed validation.</param>
/// <param name="CalculatedInspectorName">Computed inspector name on the complete record.</param>
/// <param name="CalculatedSampleTotal">Computed sample total on the complete record.</param>
public sealed record FieldFormDemoSummary(
    int InvalidErrorCount,
    bool InvalidIsValid,
    bool ValidIsValid,
    string? CalculatedInspectorName,
    string? CalculatedSampleTotal);
