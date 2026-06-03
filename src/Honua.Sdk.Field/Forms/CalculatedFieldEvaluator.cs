// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Field.Forms.Expressions;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Forms;

/// <summary>
/// Evaluates portable calculated field expressions.
/// </summary>
public static class CalculatedFieldEvaluator
{
    /// <summary>
    /// Evaluates calculated fields and writes results into the record values
    /// dictionary, including each captured row of a repeatable section (where
    /// expressions resolve against the row's own values).
    /// </summary>
    /// <param name="form">Form with calculated field definitions.</param>
    /// <param name="record">Record containing input values and receiving calculated output values.</param>
    public static void ApplyCalculatedFields(FormDefinition form, FieldRecord record)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(record);

        foreach (var section in form.Sections)
        {
            if (section.Repeatable)
            {
                if (record.Repeats.TryGetValue(section.SectionId, out var instances) && instances is not null)
                {
                    foreach (var instance in instances)
                    {
                        ApplyToScope(section.Fields, instance.Values);
                    }
                }

                continue;
            }

            ApplyToScope(section.Fields, record.Values);
        }
    }

    private static void ApplyToScope(IEnumerable<FormField> fields, Dictionary<string, object?> values)
    {
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.CalculatedExpression))
            {
                continue;
            }

            var result = ExpressionEvaluator.EvaluateDetailed(field.CalculatedExpression, values);

            // A malformed or unresolvable expression must not clobber an existing
            // value with null; only write through results that actually evaluated.
            if (result.Succeeded)
            {
                values[field.FieldId] = result.Value;
            }
        }
    }
}
