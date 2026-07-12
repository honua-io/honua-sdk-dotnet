// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text;

namespace Honua.Sdk.Field.Forms.Expressions;

internal enum TokenType
{
    Number,
    String,
    Identifier,
    FieldRef,
    Operator,
    LeftParen,
    RightParen,
    Comma,
    End,
}

internal readonly struct Token
{
    public Token(TokenType type, string text, object? literal = null)
    {
        Type = type;
        Text = text;
        Literal = literal;
    }

    public TokenType Type { get; }

    public string Text { get; }

    public object? Literal { get; }
}

/// <summary>
/// Converts expression source text into a flat token list.
/// </summary>
internal static class Tokenizer
{
    /// <summary>
    /// Context key that a bare <c>.</c> token resolves to. Constraint evaluation
    /// populates this with the value of the field under validation.
    /// </summary>
    public const string SelfReferenceKey = ".";

    public static IReadOnlyList<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var i = 0;
        var length = source.Length;

        while (i < length)
        {
            var c = source[i];

            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            switch (c)
            {
                case '(':
                    tokens.Add(new Token(TokenType.LeftParen, "("));
                    i++;
                    continue;
                case ')':
                    tokens.Add(new Token(TokenType.RightParen, ")"));
                    i++;
                    continue;
                case ',':
                    tokens.Add(new Token(TokenType.Comma, ","));
                    i++;
                    continue;
                case '\'':
                    tokens.Add(ReadString(source, ref i));
                    continue;
                case '$':
                    tokens.Add(ReadFieldRef(source, ref i));
                    continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < length && char.IsDigit(source[i + 1])))
            {
                tokens.Add(ReadNumber(source, ref i));
                continue;
            }

            // A standalone '.' references the "current" value (the field under constraint).
            if (c == '.')
            {
                i++;
                tokens.Add(new Token(TokenType.FieldRef, SelfReferenceKey, SelfReferenceKey));
                continue;
            }

            if (IsIdentifierStart(c))
            {
                tokens.Add(ReadIdentifier(source, ref i));
                continue;
            }

            var op = ReadOperator(source, ref i);
            if (op is not null)
            {
                tokens.Add(op.Value);
                continue;
            }

            throw new ExpressionEvaluationException($"Unexpected character '{c}' at position {i}.");
        }

        tokens.Add(new Token(TokenType.End, string.Empty));
        return tokens;
    }

    private static Token ReadString(string source, ref int i)
    {
        // i points at the opening quote.
        var sb = new StringBuilder();
        i++;
        while (i < source.Length)
        {
            var c = source[i];
            if (c == '\'')
            {
                // Doubled single-quote is an escaped quote.
                if (i + 1 < source.Length && source[i + 1] == '\'')
                {
                    sb.Append('\'');
                    i += 2;
                    continue;
                }

                i++;
                return new Token(TokenType.String, sb.ToString(), sb.ToString());
            }

            sb.Append(c);
            i++;
        }

        throw new ExpressionEvaluationException("Unterminated string literal.");
    }

    private static Token ReadFieldRef(string source, ref int i)
    {
        // i points at '$'.
        i++;
        if (i < source.Length && source[i] == '{')
        {
            i++;
            var sb = new StringBuilder();
            while (i < source.Length && source[i] != '}')
            {
                sb.Append(source[i]);
                i++;
            }

            if (i >= source.Length)
            {
                throw new ExpressionEvaluationException("Unterminated field reference; missing '}'.");
            }

            i++; // consume '}'
            var name = sb.ToString().Trim();
            if (name.Length == 0)
            {
                throw new ExpressionEvaluationException("Empty field reference.");
            }

            return new Token(TokenType.FieldRef, name, name);
        }
        else
        {
            var start = i;
            while (i < source.Length && IsIdentifierPart(source[i]))
            {
                i++;
            }

            if (i == start)
            {
                throw new ExpressionEvaluationException("Empty field reference.");
            }

            var name = source[start..i];
            return new Token(TokenType.FieldRef, name, name);
        }
    }

    private static Token ReadNumber(string source, ref int i)
    {
        var start = i;
        var seenDot = false;
        var seenExp = false;
        while (i < source.Length)
        {
            var c = source[i];
            if (char.IsDigit(c))
            {
                i++;
            }
            else if (c == '.' && !seenDot && !seenExp)
            {
                seenDot = true;
                i++;
            }
            else if ((c == 'e' || c == 'E') && !seenExp)
            {
                seenExp = true;
                i++;
                if (i < source.Length && (source[i] == '+' || source[i] == '-'))
                {
                    i++;
                }
            }
            else
            {
                break;
            }
        }

        var text = source[start..i];
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            throw new ExpressionEvaluationException($"Invalid number literal '{text}'.");
        }

        return new Token(TokenType.Number, text, number);
    }

    private static Token ReadIdentifier(string source, ref int i)
    {
        var start = i;
        while (i < source.Length && IsIdentifierPart(source[i]))
        {
            i++;
        }

        var text = source[start..i];
        return new Token(TokenType.Identifier, text);
    }

    private static Token? ReadOperator(string source, ref int i)
    {
        var c = source[i];
        var next = i + 1 < source.Length ? source[i + 1] : '\0';

        switch (c)
        {
            case '=':
                if (next == '=')
                {
                    i += 2;
                    return new Token(TokenType.Operator, "==");
                }

                i++;
                return new Token(TokenType.Operator, "=");
            case '!':
                if (next == '=')
                {
                    i += 2;
                    return new Token(TokenType.Operator, "!=");
                }

                i++;
                return new Token(TokenType.Operator, "!");
            case '<':
                if (next == '=')
                {
                    i += 2;
                    return new Token(TokenType.Operator, "<=");
                }

                i++;
                return new Token(TokenType.Operator, "<");
            case '>':
                if (next == '=')
                {
                    i += 2;
                    return new Token(TokenType.Operator, ">=");
                }

                i++;
                return new Token(TokenType.Operator, ">");
            case '&':
                if (next == '&')
                {
                    i += 2;
                    return new Token(TokenType.Operator, "&&");
                }

                break;
            case '|':
                if (next == '|')
                {
                    i += 2;
                    return new Token(TokenType.Operator, "||");
                }

                break;
            case '+':
            case '-':
            case '*':
            case '/':
            case '%':
                i++;
                return new Token(TokenType.Operator, c.ToString());
        }

        return null;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.';
}
