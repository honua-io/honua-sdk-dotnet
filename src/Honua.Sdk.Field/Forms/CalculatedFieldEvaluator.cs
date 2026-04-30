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
    /// Evaluates calculated fields and writes results into the record values dictionary.
    /// </summary>
    /// <param name="form">Form with calculated field definitions.</param>
    /// <param name="record">Record containing input values and receiving calculated output values.</param>
    public static void ApplyCalculatedFields(FormDefinition form, FieldRecord record)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(record);

        foreach (var field in form.Sections.SelectMany(section => section.Fields))
        {
            if (string.IsNullOrWhiteSpace(field.CalculatedExpression))
            {
                continue;
            }

            var value = EvaluateExpression(field.CalculatedExpression!, record);
            record.Values[field.FieldId] = value;
        }
    }

    private static object? EvaluateExpression(string expression, FieldRecord record)
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
            .Select(arg => ResolveArg(arg, record))
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

    private static object? ResolveArg(string arg, FieldRecord record)
    {
        if (arg.StartsWith('$'))
        {
            var key = arg[1..];
            return record.Values.TryGetValue(key, out var value) ? value : null;
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
