// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms.Authoring;

/// <summary>A worksheet row with its original one-based spreadsheet row number.</summary>
public sealed record XlsFormRow
{
    /// <summary>The one-based spreadsheet row number, including the header row.</summary>
    public required int RowNumber { get; init; }

    /// <summary>Cell text indexed by the case-sensitive XLSForm column name.</summary>
    public required IReadOnlyDictionary<string, string> Cells { get; init; }
}

/// <summary>The survey, choices and settings rows of an XLSForm workbook.</summary>
public sealed record XlsFormWorkbook
{
    /// <summary>Ordered survey rows.</summary>
    public IReadOnlyList<XlsFormRow> Survey { get; init; } = [];

    /// <summary>Ordered choice-list rows.</summary>
    public IReadOnlyList<XlsFormRow> Choices { get; init; } = [];

    /// <summary>Form settings rows, with at most one nonempty row.</summary>
    public IReadOnlyList<XlsFormRow> Settings { get; init; } = [];
}

/// <summary>A diagnostic identifying an invalid or unsupported spreadsheet cell.</summary>
public sealed record XlsFormDiagnostic
{
    /// <summary>The worksheet name.</summary>
    public required string Sheet { get; init; }

    /// <summary>The one-based spreadsheet row number.</summary>
    public required int RowNumber { get; init; }

    /// <summary>The XLSForm column name.</summary>
    public required string Column { get; init; }

    /// <summary>The reason the cell cannot be converted.</summary>
    public required string Message { get; init; }
}

/// <summary>A conversion result that exposes a value only when no diagnostics exist.</summary>
public sealed record XlsFormResult<T> where T : class
{
    /// <summary>The complete converted value, or null when conversion failed.</summary>
    public T? Value { get; init; }

    /// <summary>All detected invalid or unsupported cells.</summary>
    public IReadOnlyList<XlsFormDiagnostic> Diagnostics { get; init; } = [];
}
