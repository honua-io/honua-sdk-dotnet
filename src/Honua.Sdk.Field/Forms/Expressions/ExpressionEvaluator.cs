// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;

namespace Honua.Sdk.Field.Forms.Expressions;

/// <summary>
/// Evaluates portable form expressions against a context of field values.
/// </summary>
/// <remarks>
/// <para>
/// The engine supports literals (numbers, single-quoted strings, <c>true</c>,
/// <c>false</c>, <c>null</c>), field references (<c>$fieldId</c> and
/// <c>${fieldId}</c>), arithmetic, comparison and logical operators, and a small
/// library of functions. It is deliberately tolerant: a malformed expression,
/// an unknown function, or a type mismatch never throws — the evaluator returns
/// <see langword="null"/> and records a diagnostic on the
/// <see cref="ExpressionResult"/>.
/// </para>
/// <para>
/// Date helpers operate on <see cref="DateTimeOffset"/>. <c>today()</c> and
/// <c>now()</c> read the current clock; tests that need determinism should pass
/// fixed dates as field values rather than relying on those functions.
/// </para>
/// </remarks>
public static class ExpressionEvaluator
{
    /// <summary>Maximum supported expression length, guarding against runaway input.</summary>
    public const int MaxExpressionLength = 8_000;

    /// <summary>Maximum parser/evaluator recursion depth, guarding against runaway nesting.</summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// Evaluates an expression against the supplied field-value context and
    /// returns the raw object result, or <see langword="null"/> when the
    /// expression is empty, malformed, or fails to evaluate.
    /// </summary>
    /// <param name="expression">Expression source text.</param>
    /// <param name="context">Field values keyed by field id. May be <see langword="null"/>.</param>
    /// <returns>The evaluated value, or <see langword="null"/>.</returns>
    public static object? Evaluate(string? expression, IReadOnlyDictionary<string, object?>? context)
        => EvaluateDetailed(expression, context).Value;

    /// <summary>
    /// Evaluates an expression and returns the value together with any diagnostic
    /// produced while tokenizing, parsing, or evaluating it.
    /// </summary>
    /// <param name="expression">Expression source text.</param>
    /// <param name="context">Field values keyed by field id. May be <see langword="null"/>.</param>
    /// <returns>An <see cref="ExpressionResult"/> describing the outcome.</returns>
    public static ExpressionResult EvaluateDetailed(string? expression, IReadOnlyDictionary<string, object?>? context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return ExpressionResult.Failure("Expression is empty.");
        }

        if (expression!.Length > MaxExpressionLength)
        {
            return ExpressionResult.Failure("Expression exceeds the maximum supported length.");
        }

        try
        {
            var tokens = Tokenizer.Tokenize(expression);
            var parser = new Parser(tokens);
            var node = parser.ParseExpression();
            parser.ExpectEnd();
            var value = node.Evaluate(context ?? EmptyContext, 0);
            return ExpressionResult.Success(value);
        }
        catch (ExpressionException ex)
        {
            return ExpressionResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Evaluates an expression and coerces the result to a boolean using truthiness
    /// rules. A failed or null evaluation yields <see langword="false"/>.
    /// </summary>
    /// <param name="expression">Expression source text.</param>
    /// <param name="context">Field values keyed by field id.</param>
    /// <returns>The boolean interpretation of the result.</returns>
    public static bool EvaluateBoolean(string? expression, IReadOnlyDictionary<string, object?>? context)
        => Coercion.ToBoolean(Evaluate(expression, context));

    private static readonly IReadOnlyDictionary<string, object?> EmptyContext =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Outcome of an expression evaluation.
/// </summary>
public sealed class ExpressionResult
{
    private ExpressionResult(bool succeeded, object? value, string? diagnostic)
    {
        Succeeded = succeeded;
        Value = value;
        Diagnostic = diagnostic;
    }

    /// <summary>Whether the expression evaluated without a diagnostic.</summary>
    public bool Succeeded { get; }

    /// <summary>Evaluated value, or <see langword="null"/>.</summary>
    public object? Value { get; }

    /// <summary>Diagnostic message when evaluation failed, otherwise <see langword="null"/>.</summary>
    public string? Diagnostic { get; }

    internal static ExpressionResult Success(object? value) => new(true, value, null);

    internal static ExpressionResult Failure(string diagnostic) => new(false, null, diagnostic);
}

/// <summary>
/// Exception used to unwind tokenizer/parser/evaluator errors into a diagnostic.
/// </summary>
public sealed class ExpressionException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="ExpressionException"/> class.</summary>
    public ExpressionException()
    {
    }

    /// <summary>Initializes a new instance with a diagnostic message.</summary>
    /// <param name="message">Diagnostic message.</param>
    public ExpressionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with a diagnostic message and inner exception.</summary>
    /// <param name="message">Diagnostic message.</param>
    /// <param name="innerException">Underlying cause.</param>
    public ExpressionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Shared null/numeric/boolean/string coercion helpers.
/// </summary>
internal static class Coercion
{
    public static bool TryToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = default;
                return false;
            case bool b:
                result = b ? 1 : 0;
                return true;
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case short s:
                result = s;
                return true;
            case byte by:
                result = by;
                return true;
            case DateTimeOffset dto:
                result = dto.ToUnixTimeMilliseconds();
                return true;
            case DateTime dt:
                result = new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeMilliseconds();
                return true;
            case string str:
                return double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
            default:
                return double.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out result);
        }
    }

    public static bool ToBoolean(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            case bool b:
                return b;
            case string str:
                if (bool.TryParse(str, out var parsed))
                {
                    return parsed;
                }

                return !string.IsNullOrEmpty(str);
            default:
                return TryToDouble(value, out var number) ? number != 0 : true;
        }
    }

    public static string ToStringValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string str => str,
            bool b => b ? "true" : "false",
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("o", CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("o", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    public static bool TryToDateTimeOffset(object? value, out DateTimeOffset result)
    {
        switch (value)
        {
            case DateTimeOffset dto:
                result = dto;
                return true;
            case DateTime dt:
                result = new DateTimeOffset(dt.ToUniversalTime(), TimeSpan.Zero);
                return true;
            case string str when DateTimeOffset.TryParse(
                str,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed):
                result = parsed;
                return true;
            case DateOnly dateOnly:
                result = new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
                return true;
            default:
                result = default;
                return false;
        }
    }
}
