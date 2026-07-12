// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;

namespace Honua.Sdk.Field.Forms.Expressions;

/// <summary>
/// Built-in expression function library.
/// </summary>
internal static class ExpressionFunctions
{
    public static object? Invoke(
        string name,
        IReadOnlyList<ExpressionNode> args,
        IReadOnlyDictionary<string, object?> context,
        int depth)
    {
        // if() must control argument evaluation, so it is handled before eager evaluation.
        if (NameEquals(name, "if"))
        {
            if (args.Count != 3)
            {
                throw new ExpressionEvaluationException("if() requires exactly 3 arguments.");
            }

            var condition = Coercion.ToBoolean(args[0].Evaluate(context, depth));
            return condition ? args[1].Evaluate(context, depth) : args[2].Evaluate(context, depth);
        }

        // coalesce() also benefits from lazy evaluation.
        if (NameEquals(name, "coalesce"))
        {
            foreach (var arg in args)
            {
                var value = arg.Evaluate(context, depth);
                if (value is not null && !(value is string s && s.Length == 0))
                {
                    return value;
                }
            }

            return null;
        }

        var values = new object?[args.Count];
        for (var i = 0; i < args.Count; i++)
        {
            values[i] = args[i].Evaluate(context, depth);
        }

        return name.ToUpperInvariant() switch
        {
            "CONCAT" => Concat(values),
            "SUM" => (object?)values.Sum(ToDouble),
            "MIN" => Aggregate(values, Math.Min),
            "MAX" => Aggregate(values, Math.Max),
            "ROUND" => Round(values),
            "FLOOR" => UnaryMath(values, Math.Floor),
            "CEIL" or "CEILING" => UnaryMath(values, Math.Ceiling),
            "ABS" => UnaryMath(values, Math.Abs),
            "LEN" or "STRING-LENGTH" or "LENGTH" => StringLength(values),
            "UPPER" => Transform(values, static s => s.ToUpperInvariant()),
#pragma warning disable CA1308 // lower() intentionally lower-cases its argument.
            "LOWER" => Transform(values, static s => s.ToLowerInvariant()),
#pragma warning restore CA1308
            "TRIM" => Transform(values, static s => s.Trim()),
            "CONTAINS" => Contains(values),
            "NUMBER" => ToNumberOrNull(values),
            "STRING" => values.Length >= 1 ? Coercion.ToStringValue(values[0]) : string.Empty,
            "TODAY" => Today(),
            "NOW" => DateTimeOffset.UtcNow,
            "DATE-DIFF-DAYS" or "DATEDIFFDAYS" => DateDiffDays(values),
            "ADD-DAYS" or "ADDDAYS" => AddDays(values),
            _ => throw new ExpressionEvaluationException($"Unknown function '{name}'."),
        };
    }

    private static bool NameEquals(string name, string candidate)
        => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase);

    private static double ToDouble(object? value)
        => Coercion.TryToDouble(value, out var d) ? d : 0;

    private static string Concat(object?[] values)
        => string.Concat(values.Select(Coercion.ToStringValue));

    private static double? Aggregate(object?[] values, Func<double, double, double> op)
    {
        double? acc = null;
        foreach (var value in values)
        {
            if (Coercion.TryToDouble(value, out var number))
            {
                acc = acc is null ? number : op(acc.Value, number);
            }
        }

        return acc;
    }

    private static double? Round(object?[] values)
    {
        if (values.Length == 0 || !Coercion.TryToDouble(values[0], out var x))
        {
            return null;
        }

        var digits = 0;
        if (values.Length >= 2 && Coercion.TryToDouble(values[1], out var d))
        {
            digits = Math.Clamp((int)d, 0, 15);
        }

        return Math.Round(x, digits, MidpointRounding.AwayFromZero);
    }

    private static double? UnaryMath(object?[] values, Func<double, double> op)
    {
        if (values.Length == 0 || !Coercion.TryToDouble(values[0], out var x))
        {
            return null;
        }

        return op(x);
    }

    private static double StringLength(object?[] values)
        => values.Length == 0 ? 0d : Coercion.ToStringValue(values[0]).Length;

    private static string? Transform(object?[] values, Func<string, string> op)
        => values.Length == 0 ? null : op(Coercion.ToStringValue(values[0]));

    private static bool Contains(object?[] values)
    {
        if (values.Length < 2)
        {
            return false;
        }

        return Coercion.ToStringValue(values[0])
            .Contains(Coercion.ToStringValue(values[1]), StringComparison.Ordinal);
    }

    private static double? ToNumberOrNull(object?[] values)
        => values.Length >= 1 && Coercion.TryToDouble(values[0], out var d) ? d : null;

    private static DateTimeOffset Today()
    {
        var now = DateTimeOffset.UtcNow;
        return new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private static double? DateDiffDays(object?[] values)
    {
        if (values.Length < 2
            || !Coercion.TryToDateTimeOffset(values[0], out var a)
            || !Coercion.TryToDateTimeOffset(values[1], out var b))
        {
            return null;
        }

        // Whole-day difference of a minus b.
        return Math.Truncate((a - b).TotalDays);
    }

    private static DateTimeOffset? AddDays(object?[] values)
    {
        if (values.Length < 2
            || !Coercion.TryToDateTimeOffset(values[0], out var date)
            || !Coercion.TryToDouble(values[1], out var days))
        {
            return null;
        }

        return date.AddDays(days);
    }
}
