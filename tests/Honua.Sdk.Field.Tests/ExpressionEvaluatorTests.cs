using System.Globalization;
using Honua.Sdk.Field.Forms.Expressions;

namespace Honua.Sdk.Field.Tests;

public sealed class ExpressionEvaluatorTests
{
    private static Dictionary<string, object?> Ctx(params (string Key, object? Value)[] entries)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
        {
            dict[key] = value;
        }

        return dict;
    }

    private static double AsDouble(object? value)
    {
        Assert.NotNull(value);
        return Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }

    // ---- literals ----

    [Theory]
    [InlineData("1", 1d)]
    [InlineData("3.5", 3.5d)]
    [InlineData("1e3", 1000d)]
    [InlineData("-4", -4d)]
    public void Evaluate_NumberLiterals(string expr, double expected)
        => Assert.Equal(expected, AsDouble(ExpressionEvaluator.Evaluate(expr, null)));

    [Fact]
    public void Evaluate_StringLiteral()
        => Assert.Equal("hello world", ExpressionEvaluator.Evaluate("'hello world'", null));

    [Fact]
    public void Evaluate_EscapedQuoteInString()
        => Assert.Equal("it's", ExpressionEvaluator.Evaluate("'it''s'", null));

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Evaluate_BooleanLiterals(string expr, bool expected)
        => Assert.Equal(expected, ExpressionEvaluator.Evaluate(expr, null));

    [Fact]
    public void Evaluate_NullLiteral()
        => Assert.Null(ExpressionEvaluator.Evaluate("null", null));

    // ---- field references ----

    [Fact]
    public void Evaluate_DollarFieldReference()
        => Assert.Equal(42d, AsDouble(ExpressionEvaluator.Evaluate("$score", Ctx(("score", 42)))));

    [Fact]
    public void Evaluate_BracedFieldReference()
        => Assert.Equal(42d, AsDouble(ExpressionEvaluator.Evaluate("${score}", Ctx(("score", 42)))));

    [Fact]
    public void Evaluate_BracedFieldReference_WithHyphenAndDot()
        => Assert.Equal("x", ExpressionEvaluator.Evaluate("${a-b.c}", Ctx(("a-b.c", "x"))));

    [Fact]
    public void Evaluate_MissingFieldReference_IsNull()
        => Assert.Null(ExpressionEvaluator.Evaluate("$missing", Ctx()));

    [Fact]
    public void Evaluate_DollarAndBraced_Equivalent()
    {
        var ctx = Ctx(("a", 5));
        Assert.Equal(
            ExpressionEvaluator.Evaluate("$a + 1", ctx),
            ExpressionEvaluator.Evaluate("${a} + 1", ctx));
    }

    // ---- arithmetic ----

    [Theory]
    [InlineData("2 + 3", 5d)]
    [InlineData("10 - 4", 6d)]
    [InlineData("6 * 7", 42d)]
    [InlineData("20 / 5", 4d)]
    [InlineData("17 % 5", 2d)]
    [InlineData("2 + 3 * 4", 14d)]
    [InlineData("(2 + 3) * 4", 20d)]
    [InlineData("-3 + 5", 2d)]
    [InlineData("2 * -3", -6d)]
    public void Evaluate_Arithmetic(string expr, double expected)
        => Assert.Equal(expected, AsDouble(ExpressionEvaluator.Evaluate(expr, null)));

    [Fact]
    public void Evaluate_DivisionByZero_IsNull()
        => Assert.Null(ExpressionEvaluator.Evaluate("5 / 0", null));

    [Fact]
    public void Evaluate_PlusOnStrings_Concatenates()
        => Assert.Equal("ab", ExpressionEvaluator.Evaluate("'a' + 'b'", null));

    // ---- comparison ----

    [Theory]
    [InlineData("1 = 1", true)]
    [InlineData("1 == 1", true)]
    [InlineData("1 != 2", true)]
    [InlineData("2 > 1", true)]
    [InlineData("1 < 2", true)]
    [InlineData("2 >= 2", true)]
    [InlineData("2 <= 1", false)]
    [InlineData("'a' = 'a'", true)]
    [InlineData("'a' = 'b'", false)]
    public void Evaluate_Comparison(string expr, bool expected)
        => Assert.Equal(expected, ExpressionEvaluator.Evaluate(expr, null));

    [Fact]
    public void Evaluate_Comparison_NumericStringCoercion()
        => Assert.Equal(true, ExpressionEvaluator.Evaluate("$n > 3", Ctx(("n", "5"))));

    // ---- logical ----

    [Theory]
    [InlineData("true and true", true)]
    [InlineData("true and false", false)]
    [InlineData("false or true", true)]
    [InlineData("false or false", false)]
    [InlineData("not true", false)]
    [InlineData("not false", true)]
    [InlineData("true && false", false)]
    [InlineData("false || true", true)]
    [InlineData("!false", true)]
    [InlineData("1 > 0 and 2 > 1", true)]
    [InlineData("1 > 0 or 2 < 1", true)]
    public void Evaluate_Logical(string expr, bool expected)
        => Assert.Equal(expected, ExpressionEvaluator.Evaluate(expr, null));

    [Fact]
    public void Evaluate_LogicalPrecedence_OrBelowAnd()
        => Assert.Equal(true, ExpressionEvaluator.Evaluate("false and false or true", null));

    [Fact]
    public void Evaluate_CompoundFieldCondition()
    {
        var ctx = Ctx(("a", "x"), ("b", 10));
        Assert.Equal(true, ExpressionEvaluator.Evaluate("${a}='x' and ${b}>5", ctx));
        Assert.Equal(false, ExpressionEvaluator.Evaluate("${a}='x' and ${b}>50", ctx));
    }

    // ---- functions ----

    [Fact]
    public void Function_If()
    {
        Assert.Equal("yes", ExpressionEvaluator.Evaluate("if(1 > 0, 'yes', 'no')", null));
        Assert.Equal("no", ExpressionEvaluator.Evaluate("if(1 < 0, 'yes', 'no')", null));
    }

    [Fact]
    public void Function_Coalesce()
    {
        Assert.Equal("fallback", ExpressionEvaluator.Evaluate("coalesce($missing, '', 'fallback')", Ctx()));
        Assert.Equal(7d, AsDouble(ExpressionEvaluator.Evaluate("coalesce($a, 7)", Ctx(("a", null)))));
    }

    [Fact]
    public void Function_Concat()
        => Assert.Equal("a-b", ExpressionEvaluator.Evaluate("concat('a','-','b')", null));

    [Fact]
    public void Function_Sum()
        => Assert.Equal(8d, AsDouble(ExpressionEvaluator.Evaluate("sum($a,$b)", Ctx(("a", 3), ("b", 5)))));

    [Fact]
    public void Function_MinMax()
    {
        Assert.Equal(1d, AsDouble(ExpressionEvaluator.Evaluate("min(3, 1, 2)", null)));
        Assert.Equal(3d, AsDouble(ExpressionEvaluator.Evaluate("max(3, 1, 2)", null)));
    }

    [Theory]
    [InlineData("round(3.14159, 2)", 3.14d)]
    [InlineData("round(2.5)", 3d)]
    [InlineData("floor(3.9)", 3d)]
    [InlineData("ceil(3.1)", 4d)]
    [InlineData("abs(-5)", 5d)]
    public void Function_Math(string expr, double expected)
        => Assert.Equal(expected, AsDouble(ExpressionEvaluator.Evaluate(expr, null)));

    [Fact]
    public void Function_LenAndStringLength()
    {
        Assert.Equal(5d, AsDouble(ExpressionEvaluator.Evaluate("len('hello')", null)));
        Assert.Equal(5d, AsDouble(ExpressionEvaluator.Evaluate("string-length('hello')", null)));
    }

    [Fact]
    public void Function_UpperLowerTrim()
    {
        Assert.Equal("ABC", ExpressionEvaluator.Evaluate("upper('abc')", null));
        Assert.Equal("abc", ExpressionEvaluator.Evaluate("lower('ABC')", null));
        Assert.Equal("abc", ExpressionEvaluator.Evaluate("trim('  abc  ')", null));
    }

    [Fact]
    public void Function_Contains()
    {
        Assert.Equal(true, ExpressionEvaluator.Evaluate("contains('hello', 'ell')", null));
        Assert.Equal(false, ExpressionEvaluator.Evaluate("contains('hello', 'z')", null));
    }

    [Fact]
    public void Function_NumberAndString()
    {
        Assert.Equal(12d, AsDouble(ExpressionEvaluator.Evaluate("number('12')", null)));
        Assert.Equal("3.5", ExpressionEvaluator.Evaluate("string(3.5)", null));
    }

    [Fact]
    public void Function_DateDiffDays_UsesFixedFieldDates()
    {
        var ctx = Ctx(
            ("start", DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture)),
            ("end", DateTimeOffset.Parse("2026-01-11T00:00:00Z", CultureInfo.InvariantCulture)));
        Assert.Equal(10d, AsDouble(ExpressionEvaluator.Evaluate("date-diff-days($end, $start)", ctx)));
    }

    [Fact]
    public void Function_AddDays_UsesFixedFieldDate()
    {
        var ctx = Ctx(("d", DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture)));
        var result = ExpressionEvaluator.Evaluate("add-days($d, 5)", ctx);
        var expected = DateTimeOffset.Parse("2026-01-06T00:00:00Z", CultureInfo.InvariantCulture);
        Assert.Equal(expected, Assert.IsType<DateTimeOffset>(result));
    }

    [Fact]
    public void Function_DateDiffDays_AcceptsIsoStrings()
    {
        var ctx = Ctx(("a", "2026-03-10"), ("b", "2026-03-01"));
        Assert.Equal(9d, AsDouble(ExpressionEvaluator.Evaluate("date-diff-days($a, $b)", ctx)));
    }

    [Fact]
    public void Function_Today_ReturnsMidnightUtc()
    {
        var result = Assert.IsType<DateTimeOffset>(ExpressionEvaluator.Evaluate("today()", null));
        Assert.Equal(TimeSpan.Zero, result.TimeOfDay);
    }

    // ---- null / robustness ----

    [Fact]
    public void Evaluate_NullArithmetic_IsNull()
        => Assert.Null(ExpressionEvaluator.Evaluate("$missing + 1", Ctx()));

    [Fact]
    public void Evaluate_EmptyExpression_ReturnsNull()
        => Assert.Null(ExpressionEvaluator.Evaluate("   ", null));

    [Fact]
    public void Evaluate_MalformedExpression_DoesNotThrow_ReturnsNullWithDiagnostic()
    {
        var result = ExpressionEvaluator.EvaluateDetailed("1 + + )", null);
        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.NotNull(result.Diagnostic);
    }

    [Fact]
    public void Evaluate_UnknownFunction_ReturnsDiagnostic()
    {
        var result = ExpressionEvaluator.EvaluateDetailed("bogus(1)", null);
        Assert.False(result.Succeeded);
        Assert.Contains("bogus", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_OverlongInput_IsRejected()
    {
        var huge = new string('1', ExpressionEvaluator.MaxExpressionLength + 1);
        var result = ExpressionEvaluator.EvaluateDetailed(huge, null);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Evaluate_DeeplyNestedInput_DoesNotThrow()
    {
        var expr = new string('(', 500) + "1" + new string(')', 500);
        var result = ExpressionEvaluator.EvaluateDetailed(expr, null);
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Diagnostic);
    }

    [Fact]
    public void EvaluateBoolean_TruthyAndFalsy()
    {
        Assert.True(ExpressionEvaluator.EvaluateBoolean("$a > 5", Ctx(("a", 10))));
        Assert.False(ExpressionEvaluator.EvaluateBoolean("$a > 5", Ctx(("a", 1))));
        Assert.False(ExpressionEvaluator.EvaluateBoolean("$missing", Ctx()));
    }
}
