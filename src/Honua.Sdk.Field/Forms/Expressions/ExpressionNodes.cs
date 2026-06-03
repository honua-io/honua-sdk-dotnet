// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms.Expressions;

/// <summary>
/// Base class for evaluable expression-tree nodes.
/// </summary>
internal abstract class ExpressionNode
{
    public abstract object? Evaluate(IReadOnlyDictionary<string, object?> context, int depth);

    protected static void GuardDepth(int depth)
    {
        if (depth > ExpressionEvaluator.MaxDepth)
        {
            throw new ExpressionException("Expression nesting is too deep.");
        }
    }
}

internal sealed class LiteralNode : ExpressionNode
{
    private readonly object? _value;

    public LiteralNode(object? value) => _value = value;

    public override object? Evaluate(IReadOnlyDictionary<string, object?> context, int depth) => _value;
}

internal sealed class FieldRefNode : ExpressionNode
{
    private readonly string _name;

    public FieldRefNode(string name) => _name = name;

    public override object? Evaluate(IReadOnlyDictionary<string, object?> context, int depth)
        => context.TryGetValue(_name, out var value) ? value : null;
}

internal sealed class UnaryNode : ExpressionNode
{
    private readonly string _op;
    private readonly ExpressionNode _operand;

    public UnaryNode(string op, ExpressionNode operand)
    {
        _op = op;
        _operand = operand;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> context, int depth)
    {
        GuardDepth(depth);
        var value = _operand.Evaluate(context, depth + 1);
        switch (_op)
        {
            case "-":
                return Coercion.TryToDouble(value, out var number) ? -number : null;
            case "!":
            case "not":
                return !Coercion.ToBoolean(value);
            default:
                throw new ExpressionException($"Unknown unary operator '{_op}'.");
        }
    }
}

internal sealed class BinaryNode : ExpressionNode
{
    private readonly string _op;
    private readonly ExpressionNode _left;
    private readonly ExpressionNode _right;

    public BinaryNode(string op, ExpressionNode left, ExpressionNode right)
    {
        _op = op;
        _left = left;
        _right = right;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> context, int depth)
    {
        GuardDepth(depth);

        // Short-circuit logical operators.
        if (_op is "and" or "&&")
        {
            return Coercion.ToBoolean(_left.Evaluate(context, depth + 1))
                && Coercion.ToBoolean(_right.Evaluate(context, depth + 1));
        }

        if (_op is "or" or "||")
        {
            return Coercion.ToBoolean(_left.Evaluate(context, depth + 1))
                || Coercion.ToBoolean(_right.Evaluate(context, depth + 1));
        }

        var left = _left.Evaluate(context, depth + 1);
        var right = _right.Evaluate(context, depth + 1);

        switch (_op)
        {
            case "+":
                return Add(left, right);
            case "-":
                return Arithmetic(left, right, static (a, b) => a - b);
            case "*":
                return Arithmetic(left, right, static (a, b) => a * b);
            case "/":
                return Divide(left, right);
            case "%":
                return Modulo(left, right);
            case "=":
            case "==":
                return AreEqual(left, right);
            case "!=":
                return !AreEqual(left, right);
            case "<":
                return Compare(left, right) is { } lt && lt < 0;
            case "<=":
                return Compare(left, right) is { } le && le <= 0;
            case ">":
                return Compare(left, right) is { } gt && gt > 0;
            case ">=":
                return Compare(left, right) is { } ge && ge >= 0;
            default:
                throw new ExpressionException($"Unknown operator '{_op}'.");
        }
    }

    private static object? Add(object? left, object? right)
    {
        // Null propagates rather than silently coercing to an empty string;
        // callers wanting null-safe joining should use concat().
        if (left is null || right is null)
        {
            return null;
        }

        // Numeric addition when both sides are numbers; otherwise string concatenation.
        if (Coercion.TryToDouble(left, out var l) && Coercion.TryToDouble(right, out var r)
            && left is not string && right is not string)
        {
            return l + r;
        }

        return Coercion.ToStringValue(left) + Coercion.ToStringValue(right);
    }

    private static double? Arithmetic(object? left, object? right, Func<double, double, double> op)
    {
        if (Coercion.TryToDouble(left, out var l) && Coercion.TryToDouble(right, out var r))
        {
            return op(l, r);
        }

        return null;
    }

    private static object? Divide(object? left, object? right)
    {
        if (Coercion.TryToDouble(left, out var l) && Coercion.TryToDouble(right, out var r))
        {
            return r == 0 ? null : l / r;
        }

        return null;
    }

    private static object? Modulo(object? left, object? right)
    {
        if (Coercion.TryToDouble(left, out var l) && Coercion.TryToDouble(right, out var r))
        {
            return r == 0 ? null : l % r;
        }

        return null;
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is bool || right is bool)
        {
            return Coercion.ToBoolean(left) == Coercion.ToBoolean(right);
        }

        if (Coercion.TryToDouble(left, out var l) && Coercion.TryToDouble(right, out var r)
            && left is not string && right is not string)
        {
            return Math.Abs(l - r) < 1e-9;
        }

        // If both numeric-looking strings, compare numerically; otherwise string compare.
        if (Coercion.TryToDouble(left, out var ls) && Coercion.TryToDouble(right, out var rs))
        {
            return Math.Abs(ls - rs) < 1e-9;
        }

        return string.Equals(
            Coercion.ToStringValue(left),
            Coercion.ToStringValue(right),
            StringComparison.Ordinal);
    }

    private static int? Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return null;
        }

        if (Coercion.TryToDouble(left, out var l) && Coercion.TryToDouble(right, out var r))
        {
            return l.CompareTo(r);
        }

        return string.Compare(
            Coercion.ToStringValue(left),
            Coercion.ToStringValue(right),
            StringComparison.Ordinal);
    }
}

internal sealed class FunctionNode : ExpressionNode
{
    private readonly string _name;
    private readonly IReadOnlyList<ExpressionNode> _args;

    public FunctionNode(string name, IReadOnlyList<ExpressionNode> args)
    {
        _name = name;
        _args = args;
    }

    public override object? Evaluate(IReadOnlyDictionary<string, object?> context, int depth)
    {
        GuardDepth(depth);
        return ExpressionFunctions.Invoke(_name, _args, context, depth + 1);
    }
}
