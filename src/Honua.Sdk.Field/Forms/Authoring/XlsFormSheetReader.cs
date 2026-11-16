// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Xml.Linq;

namespace Honua.Sdk.Field.Forms.Authoring;

internal sealed class XlsFormSheetReader(string sheet, string[] strings, List<XlsFormDiagnostic> errors)
{
    private static XNamespace S => XlsFormXml.Spreadsheet;
    private readonly Dictionary<int, string> _headers = [];
    private readonly HashSet<int> _rows = [];
    private int _cellCount;

    public IReadOnlyList<XlsFormRow> Read(XDocument document)
    {
        if (document.Descendants(S + "mergeCell").Any())
        {
            throw new InvalidDataException("Merged cells are not supported.");
        }

        var result = new List<XlsFormRow>();
        foreach (var row in document.Descendants(S + "sheetData").Elements(S + "row"))
        {
            var number = XlsFormXml.Number((string?)row.Attribute("r"));
            if (number < 1 || number > XlsFormXml.MaxRows || !_rows.Add(number))
            {
                throw new InvalidDataException("Row references must be unique and between 1 and 10,000.");
            }

            var cells = ReadCells(row, number);
            if (number == 1)
            {
                ReadHeaders(cells);
            }
            else
            {
                result.Add(ReadRow(cells, number));
            }
        }

        if (result.Any(r => r.Cells.Count > 0) && _headers.Count == 0)
        {
            throw new InvalidDataException("A worksheet with data requires headers in row 1.");
        }

        return result.OrderBy(r => r.RowNumber).ToArray();
    }

    private Dictionary<int, string> ReadCells(XElement row, int number)
    {
        var cells = new Dictionary<int, string>();
        foreach (var cell in row.Elements(S + "c"))
        {
            if (++_cellCount > 100_000)
            {
                throw new InvalidDataException("The worksheet exceeds 100,000 cells.");
            }

            var column = XlsFormXml.Column((string?)cell.Attribute("r") ?? string.Empty, number);
            if (column < 0 || !cells.TryAdd(column, string.Empty))
            {
                throw new InvalidDataException("Invalid or duplicate cell reference; at most 64 columns are supported.");
            }

            if (cell.Element(S + "f") is not null)
            {
                Error(number, column, "Spreadsheet formulas are not supported; use literal XLSForm expressions.");
                continue;
            }

            cells[column] = CellText(cell);
        }

        return cells;
    }

    private string CellText(XElement cell)
    {
        var type = (string?)cell.Attribute("t");
        var value = cell.Element(S + "v")?.Value ?? string.Empty;
        if (value.Length > XlsFormXml.MaxCellLength)
        {
            throw new InvalidDataException("Cell text exceeds 32,767 characters.");
        }

        return type switch
        {
            "inlineStr" => XlsFormXml.RichText(cell.Element(S + "is")),
            "s" => SharedString(value),
            null or "n" or "b" or "str" => value,
            _ => throw new InvalidDataException("Unsupported cell value type.")
        };
    }

    private string SharedString(string value)
    {
        if (!int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
            || index < 0 || index >= strings.Length)
        {
            throw new InvalidDataException("Invalid shared string reference.");
        }

        return strings[index];
    }

    private void ReadHeaders(Dictionary<int, string> cells)
    {
        foreach (var (column, text) in cells.Where(c => !string.IsNullOrWhiteSpace(c.Value)))
        {
            if (_headers.Values.Contains(text, StringComparer.Ordinal))
            {
                Error(1, column, "Duplicate column header.");
            }

            _headers[column] = text;
        }
    }

    private XlsFormRow ReadRow(Dictionary<int, string> cells, int number)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (column, value) in cells.Where(c => c.Value.Length > 0))
        {
            if (!_headers.TryGetValue(column, out var header))
            {
                Error(number, column, "Nonempty cells require a column header in row 1.");
            }
            else
            {
                values[header] = value;
            }
        }

        return new XlsFormRow { RowNumber = number, Cells = values };
    }

    private void Error(int row, int column, string message)
        => errors.Add(new XlsFormDiagnostic
        {
            Sheet = sheet, RowNumber = row,
            Column = _headers.GetValueOrDefault(column) ?? XlsFormXml.Reference(column, 1).TrimEnd('1'), Message = message
        });
}
