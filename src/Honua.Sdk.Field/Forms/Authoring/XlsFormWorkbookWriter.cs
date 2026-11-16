// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Honua.Sdk.Field.Forms.Authoring;

internal static class XlsFormWorkbookWriter
{
    private static XNamespace S => XlsFormXml.Spreadsheet;
    private static XNamespace R => XlsFormXml.Relationships;
    private static XNamespace D => XlsFormXml.DocumentRelationships;
    private static readonly string[] SheetNames = ["survey", "choices", "settings"];

    public static void Write(Stream output, XlsFormWorkbook workbook)
    {
        IReadOnlyList<XlsFormRow>[] sheets = [workbook.Survey, workbook.Choices, workbook.Settings];
        foreach (var rows in sheets)
        {
            ValidateRows(rows);
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            WriteManifest(zip);
            for (var i = 0; i < sheets.Length; i++)
            {
                Save(zip, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(sheets[i]));
            }
        }

        buffer.Position = 0;
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Read, true))
        {
            XlsFormXml.CheckArchive(zip);
            if (zip.Entries.Any(e => e.Length > 16 * 1024 * 1024))
            {
                throw new InvalidDataException("An XML workbook part exceeds 16 MiB.");
            }
        }

        if (buffer.Length > 8 * 1024 * 1024)
        {
            throw new InvalidDataException("The compressed workbook exceeds 8 MiB.");
        }

        buffer.Position = 0;
        buffer.CopyTo(output);
    }

    private static void ValidateRows(IReadOnlyList<XlsFormRow> rows)
    {
        var numbers = new HashSet<int>();
        long characters = 0;
        var cellCount = 0;
        foreach (var row in rows)
        {
            if (row.RowNumber < 2 || row.RowNumber > XlsFormXml.MaxRows || !numbers.Add(row.RowNumber))
            {
                throw new InvalidDataException("Data rows must have unique numbers between 2 and 10,000.");
            }

            foreach (var (header, value) in row.Cells)
            {
                if (string.IsNullOrWhiteSpace(header) || header.Length > XlsFormXml.MaxCellLength
                    || value.Length > XlsFormXml.MaxCellLength || ++cellCount > 100_000)
                {
                    throw new InvalidDataException("Invalid header, oversized cell or more than 100,000 cells.");
                }

                characters += header.Length + value.Length;
            }
        }

        var headerCount = Headers(rows).Length;
        if (characters > 4 * 1024 * 1024 || headerCount > XlsFormXml.MaxColumns
            || (long)(rows.Count + 1) * headerCount > 100_000)
        {
            throw new InvalidDataException("The worksheet exceeds 4 MiB of text, 64 columns or 100,000 output cells.");
        }
    }

    private static string[] Headers(IReadOnlyList<XlsFormRow> rows)
        => rows.SelectMany(r => r.Cells.Keys).Distinct(StringComparer.Ordinal).ToArray();

    private static XElement Worksheet(IReadOnlyList<XlsFormRow> rows)
    {
        var headers = Headers(rows);
        var data = new XElement(S + "sheetData");
        data.Add(Row(1, headers));
        foreach (var row in rows.OrderBy(r => r.RowNumber))
        {
            data.Add(Row(row.RowNumber, headers.Select(h => row.Cells.GetValueOrDefault(h) ?? string.Empty)));
        }

        return new XElement(S + "worksheet", data);
    }

    private static XElement Row(int number, IEnumerable<string> values)
        => new(S + "row", new XAttribute("r", number), values.Select((value, column) =>
            new XElement(S + "c", new XAttribute("r", XlsFormXml.Reference(column, number)), new XAttribute("t", "inlineStr"),
                new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value)))));

    private static void WriteManifest(ZipArchive zip)
    {
        var sheets = SheetNames.Select((name, index) => new XElement(S + "sheet", new XAttribute("name", name),
            new XAttribute("sheetId", index + 1), new XAttribute(D + "id", Id(index))));
        Save(zip, "xl/workbook.xml", new XElement(S + "workbook", new XAttribute(XNamespace.Xmlns + "r", D), new XElement(S + "sheets", sheets)));
        Save(zip, "xl/_rels/workbook.xml.rels", new XElement(R + "Relationships", SheetNames.Select((_, index) =>
            Relationship(Id(index), "worksheet", $"worksheets/sheet{index + 1}.xml"))));
        Save(zip, "_rels/.rels", new XElement(R + "Relationships", Relationship("rId1", "officeDocument", "xl/workbook.xml")));
        WriteContentTypes(zip);
    }

    private static string Id(int index) => "rId" + (index + 1).ToString(CultureInfo.InvariantCulture);

    private static XElement Relationship(string id, string type, string target)
        => new(R + "Relationship", new XAttribute("Id", id), new XAttribute("Type", D.NamespaceName + "/" + type), new XAttribute("Target", target));

    private static void WriteContentTypes(ZipArchive zip)
    {
        XNamespace content = "http://schemas.openxmlformats.org/package/2006/content-types";
        var types = new XElement(content + "Types",
            new XElement(content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            new XElement(content + "Override", new XAttribute("PartName", "/xl/workbook.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
            SheetNames.Select((_, index) => new XElement(content + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{index + 1}.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
        Save(zip, "[Content_Types].xml", types);
    }

    private static void Save(ZipArchive zip, string path, XElement content)
    {
        using var stream = zip.CreateEntry(path).Open();
        new XDocument(new XDeclaration("1.0", "utf-8", null), content).Save(stream);
    }
}
