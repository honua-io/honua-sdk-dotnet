// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms.Expressions;

/// <summary>
/// Recursive-descent / precedence-climbing parser that turns a token list into
/// an evaluable <see cref="ExpressionNode"/> tree.
/// </summary>
internal sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _position;
    private int _depth;

    public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public ExpressionNode ParseExpression() => ParseBinary(0);

    public void ExpectEnd()
    {
        if (Current.Type != TokenType.End)
        {
            throw new ExpressionEvaluationException($"Unexpected token '{Current.Text}' after expression.");
        }
    }

    private Token Current => _tokens[_position];

    private Token Advance() => _tokens[_position++];

    private ExpressionNode ParseBinary(int minPrecedence)
    {
        EnterDepth();
        try
        {
            var left = ParseUnary();

            while (true)
            {
                var op = CurrentBinaryOperator();
                if (op is null)
                {
                    break;
                }

                var precedence = PrecedenceOf(op);
                if (precedence < minPrecedence)
                {
                    break;
                }

                Advance();

                // All supported binary operators are left-associative.
                var right = ParseBinary(precedence + 1);
                left = new BinaryNode(op, left, right);
            }

            return left;
        }
        finally
        {
            ExitDepth();
        }
    }

    private ExpressionNode ParseUnary()
    {
        // Guard unary recursion with the same depth limit used by ParseBinary. A long
        // chain of prefix operators (e.g. "!!!!...x") recurses through here directly, so
        // without this guard it overflows the stack at parse time — before the
        // evaluation-time depth limit can apply — crashing the process uncatchably.
        EnterDepth();
        try
        {
            if (Current.Type == TokenType.Operator && (Current.Text == "-" || Current.Text == "!"))
            {
                var op = Advance().Text;
                return new UnaryNode(op, ParseUnary());
            }

            if (Current.Type == TokenType.Identifier
                && string.Equals(Current.Text, "not", StringComparison.OrdinalIgnoreCase))
            {
                Advance();
                return new UnaryNode("not", ParseUnary());
            }

            return ParsePrimary();
        }
        finally
        {
            ExitDepth();
        }
    }

    private ExpressionNode ParsePrimary()
    {
        var token = Current;
        switch (token.Type)
        {
            case TokenType.Number:
            case TokenType.String:
                Advance();
                return new LiteralNode(token.Literal);

            case TokenType.FieldRef:
                Advance();
                return new FieldRefNode((string)token.Literal!);

            case TokenType.LeftParen:
                Advance();
                var inner = ParseExpression();
                Expect(TokenType.RightParen, ")");
                return inner;

            case TokenType.Identifier:
                return ParseIdentifier(token);

            default:
                throw new ExpressionEvaluationException($"Unexpected token '{token.Text}'.");
        }
    }

    private ExpressionNode ParseIdentifier(Token token)
    {
        // Keyword literals.
        if (string.Equals(token.Text, "true", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            return new LiteralNode(true);
        }

        if (string.Equals(token.Text, "false", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            return new LiteralNode(false);
        }

        if (string.Equals(token.Text, "null", StringComparison.OrdinalIgnoreCase))
        {
            Advance();
            return new LiteralNode(null);
        }

        // Function call: identifier immediately followed by '('.
        if (_position + 1 < _tokens.Count && _tokens[_position + 1].Type == TokenType.LeftParen)
        {
            var name = Advance().Text;
            Advance(); // '('
            var args = ParseArguments();
            Expect(TokenType.RightParen, ")");
            return new FunctionNode(name, args);
        }

        // Bare identifier resolves as a field reference (lenient fallback).
        Advance();
        return new FieldRefNode(token.Text);
    }

    private List<ExpressionNode> ParseArguments()
    {
        var args = new List<ExpressionNode>();
        if (Current.Type == TokenType.RightParen)
        {
            return args;
        }

        args.Add(ParseExpression());
        while (Current.Type == TokenType.Comma)
        {
            Advance();
            args.Add(ParseExpression());
        }

        return args;
    }

    private string? CurrentBinaryOperator()
    {
        if (Current.Type == TokenType.Operator)
        {
            return Current.Text switch
            {
                "+" or "-" or "*" or "/" or "%"
                    or "=" or "==" or "!=" or "<" or "<=" or ">" or ">="
                    or "&&" or "||" => Current.Text,
                _ => null,
            };
        }

        if (Current.Type == TokenType.Identifier)
        {
            if (string.Equals(Current.Text, "and", StringComparison.OrdinalIgnoreCase))
            {
                return "and";
            }

            if (string.Equals(Current.Text, "or", StringComparison.OrdinalIgnoreCase))
            {
                return "or";
            }
        }

        return null;
    }

    private static int PrecedenceOf(string op) => op switch
    {
        "or" or "||" => 1,
        "and" or "&&" => 2,
        "=" or "==" or "!=" => 3,
        "<" or "<=" or ">" or ">=" => 4,
        "+" or "-" => 5,
        "*" or "/" or "%" => 6,
        _ => 0,
    };

    private void Expect(TokenType type, string text)
    {
        if (Current.Type != type)
        {
            throw new ExpressionEvaluationException($"Expected '{text}' but found '{Current.Text}'.");
        }

        Advance();
    }

    private void EnterDepth()
    {
        if (++_depth > ExpressionEvaluator.MaxDepth)
        {
            throw new ExpressionEvaluationException("Expression nesting is too deep.");
        }
    }

    private void ExitDepth() => _depth--;
}
