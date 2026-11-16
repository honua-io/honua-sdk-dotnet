// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Honua.Sdk.Field.Forms.Authoring;

internal static class XlsFormXml
{
    public static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
    public static readonly XNamespace DocumentRelationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const int MaxRows = 10_000;
    public const int MaxColumns = 64;
    public const int MaxCellLength = 32_767;

    public static void CheckArchive(ZipArchive zip)
    {
        if (zip.Entries.Count > 128 || zip.Entries.Sum(e => e.Length) > 32 * 1024 * 1024)
        {
            throw new InvalidDataException("The workbook exceeds 128 entries or 32 MiB uncompressed.");
        }

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            if (!paths.Add(entry.FullName))
            {
                throw new InvalidDataException("Duplicate archive entry.");
            }
        }
    }

    public static XDocument Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException("A required workbook part is missing.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024
        });
        return XDocument.Load(reader);
    }

    public static int Number(string? text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    public static int Column(string reference, int rowNumber)
    {
        var index = 0;
        var letters = 0;
        foreach (var c in reference.TakeWhile(char.IsAsciiLetterUpper))
        {
            if (++letters > 2)
            {
                return -1;
            }

            index = index * 26 + c - 'A' + 1;
        }

        return letters > 0 && index <= MaxColumns && Number(reference[letters..]) == rowNumber ? index - 1 : -1;
    }

    public static string Reference(int column, int row)
    {
        var prefix = column < 26 ? string.Empty : ((char)('A' + column / 26 - 1)).ToString();
        return prefix + (char)('A' + column % 26) + row.ToString(CultureInfo.InvariantCulture);
    }

    public static string RichText(XElement? element)
    {
        var text = string.Concat(element?.Elements().SelectMany(child => child.Name == Spreadsheet + "t"
            ? [child] : child.Name == Spreadsheet + "r" ? child.Elements(Spreadsheet + "t") : [])
            .Select(t => XlsFormString.Decode(t.Value)) ?? []);
        if (text.Length > MaxCellLength)
        {
            throw new InvalidDataException("Cell text exceeds 32,767 characters.");
        }

        return text;
    }
}
