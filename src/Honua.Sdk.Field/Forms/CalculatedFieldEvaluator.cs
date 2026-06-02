// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
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

            values[field.FieldId] = EvaluateExpression(field.CalculatedExpression!, values);
        }
    }

    private static object? EvaluateExpression(string expression, Dictionary<string, object?> values)
    {
        var openParen = expression.IndexOf('(', StringComparison.Ordinal);
        var closeParen = expression.LastIndexOf(')');

        if (openParen <= 0 || closeParen <= openParen)
        {
            return expression;
        }

        var function = expression[..openParen].Trim();
        var args = expression[(openParen + 1)..closeParen]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(arg => ResolveArg(arg, values))
            .ToArray();

        if (string.Equals(function, "concat", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(args.Select(arg => arg?.ToString() ?? string.Empty));
        }

        if (string.Equals(function, "sum", StringComparison.OrdinalIgnoreCase))
        {
            return args.Sum(ParseDouble);
        }

        return expression;
    }

    private static object? ResolveArg(string arg, Dictionary<string, object?> values)
    {
        if (arg.StartsWith('$'))
        {
            var key = arg[1..];
            return values.TryGetValue(key, out var value) ? value : null;
        }

        if (arg.Length >= 2 && arg[0] == '\'' && arg[^1] == '\'')
        {
            return arg[1..^1];
        }

        return arg;
    }

    private static double ParseDouble(object? value)
    {
        if (value is null)
        {
            return 0;
        }

        return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }
}
