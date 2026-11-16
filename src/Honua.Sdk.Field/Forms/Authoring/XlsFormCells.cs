// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Field.Forms.Expressions;

namespace Honua.Sdk.Field.Forms.Authoring;

internal static class XlsFormCells
{
    public static string Text(this XlsFormRow row, string column)
        => row.Cells.TryGetValue(column, out var value) ? value : string.Empty;

    public static string? Optional(this XlsFormRow row, string column)
        => string.IsNullOrWhiteSpace(row.Text(column)) ? null : row.Text(column);

    public static void Error(this List<XlsFormDiagnostic> errors, (string Sheet, XlsFormRow Row, string Column) cell, string message)
        => errors.Add(new XlsFormDiagnostic { Sheet = cell.Sheet, RowNumber = cell.Row.RowNumber, Column = cell.Column, Message = message });

    public static void CheckColumns(this List<XlsFormDiagnostic> errors, (string Sheet, XlsFormRow Row) location, params string[] supported)
    {
        foreach (var cell in location.Row.Cells.Where(c => !string.IsNullOrWhiteSpace(c.Value) && !supported.Contains(c.Key, StringComparer.Ordinal)))
        {
            errors.Error((location.Sheet, location.Row, cell.Key), "This column is not supported; its behavior would be lost.");
        }
    }

    public static bool IsName(string value) => value.Length > 0 && (char.IsAsciiLetter(value[0]) || value[0] == '_')
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');

    public static void CheckExpression(this List<XlsFormDiagnostic> errors, XlsFormRow row, string column, string? generatedExpression = null)
    {
        var expression = generatedExpression ?? row.Optional(column);
        if (expression is null)
        {
            return;
        }

        try
        {
            if (expression.Length > ExpressionEvaluator.MaxExpressionLength)
            {
                throw new ExpressionEvaluationException("Expression exceeds the maximum supported length.");
            }

            var tokens = Tokenizer.Tokenize(expression);
            for (var i = 0; i + 1 < tokens.Count; i++)
            {
                if (tokens[i].Type == TokenType.Identifier && tokens[i + 1].Type != TokenType.LeftParen
                    && tokens[i].Text is not ("true" or "false" or "null" or "and" or "or" or "not"))
                {
                    throw new ExpressionEvaluationException("Field references must use ${name} syntax.");
                }
            }

            var parser = new Parser(tokens, ValidateFunction);
            var tree = parser.ParseExpression();
            if (tree.TreeDepth > ExpressionEvaluator.MaxDepth)
            {
                throw new ExpressionEvaluationException("Expression evaluation tree is too deep.");
            }
            parser.ExpectEnd();
        }
        catch (ExpressionEvaluationException ex)
        {
            errors.Error(("survey", row, column), ex.Message);
        }
    }

    public static void CheckReferences(this List<XlsFormDiagnostic> errors, XlsFormRow row, (string Column, HashSet<string> Fields) context)
    {
        var expression = row.Optional(context.Column);
        if (expression is null)
        {
            return;
        }

        try
        {
            foreach (var token in Tokenizer.Tokenize(expression).Where(t => t.Type == TokenType.FieldRef))
            {
                if (token.Text == "." ? context.Column != "constraint" : !context.Fields.Contains(token.Text))
                {
                    errors.Error(("survey", row, context.Column), "Expression references an unknown field, crosses a repeat boundary, uses a calculated field before it is evaluated or uses '.' outside a constraint.");
                }
            }
        }
        catch (ExpressionEvaluationException)
        {
            // Syntax diagnostics were already recorded by CheckExpression.
        }
    }

    private static void ValidateFunction(string name, int argumentCount)
    {
        var valid = name switch
        {
            "concat" => argumentCount >= 1,
            "if" => argumentCount == 3,
            "floor" => argumentCount == 1,
            _ => false
        };
        if (!valid)
        {
            throw new ExpressionEvaluationException("Only concat() with one or more arguments, if() with three, and floor() with one are supported.");
        }
    }

    public static XlsFormRow Row(int number, params (string Column, string? Value)[] cells) => new()
    {
        RowNumber = number,
        Cells = cells.Where(c => c.Value is not null).ToDictionary(c => c.Column, c => c.Value!, StringComparer.Ordinal)
    };
}
