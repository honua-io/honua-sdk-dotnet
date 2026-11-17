// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.IO.Compression;
using System.Xml.Linq;

namespace Honua.Sdk.Field.Forms.Authoring;

internal sealed class XlsFormWorkbookReader(ZipArchive zip)
{
    private readonly List<XlsFormDiagnostic> _errors = [];
    private static XNamespace S => XlsFormXml.Spreadsheet;

    public XlsFormResult<XlsFormWorkbook> Read()
    {
        var relationships = ReadRelationships();
        var strings = ReadSharedStrings();
        var sheets = new Dictionary<string, IReadOnlyList<XlsFormRow>>(StringComparer.Ordinal);
        var workbook = XlsFormXml.Load(zip, "xl/workbook.xml");
        foreach (var sheet in workbook.Descendants(S + "sheet"))
        {
            var name = (string?)sheet.Attribute("name") ?? string.Empty;
            var relation = (string?)sheet.Attribute(XlsFormXml.DocumentRelationships + "id") ?? string.Empty;
            if (name is not ("survey" or "choices" or "settings") || sheets.ContainsKey(name))
            {
                throw new InvalidDataException("Only one each of survey, choices and settings worksheets is supported.");
            }

            if (!relationships.TryGetValue(relation, out var target))
            {
                throw new InvalidDataException("Worksheet relationship is missing.");
            }

            sheets.Add(name, new XlsFormSheetReader(name, strings, _errors).Read(XlsFormXml.Load(zip, target)));
        }

        if (!sheets.TryGetValue("survey", out var survey))
        {
            throw new InvalidDataException("The survey worksheet is missing.");
        }

        return new XlsFormResult<XlsFormWorkbook>
        {
            Diagnostics = _errors,
            Value = _errors.Count > 0 ? null : new XlsFormWorkbook
            {
                Survey = survey, Choices = sheets.GetValueOrDefault("choices") ?? [],
                Settings = sheets.GetValueOrDefault("settings") ?? []
            }
        };
    }

    private Dictionary<string, string> ReadRelationships()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var document = XlsFormXml.Load(zip, "xl/_rels/workbook.xml.rels");
        foreach (var relation in document.Descendants(XlsFormXml.Relationships + "Relationship"))
        {
            if ((string?)relation.Attribute("TargetMode") is "External")
            {
                throw new InvalidDataException("External workbook relationships are not supported.");
            }

            if ((string?)relation.Attribute("Type") != XlsFormXml.DocumentRelationships.NamespaceName + "/worksheet")
            {
                continue;
            }

            var target = (string?)relation.Attribute("Target") ?? string.Empty;
            var path = target.StartsWith("/xl/", StringComparison.Ordinal) ? target[1..] : "xl/" + target;
            if (!path.StartsWith("xl/worksheets/", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal)
                || path.Contains('\\', StringComparison.Ordinal) || !result.TryAdd((string?)relation.Attribute("Id") ?? string.Empty, path))
            {
                throw new InvalidDataException("Invalid or duplicate worksheet relationship.");
            }
        }

        return result;
    }

    private string[] ReadSharedStrings()
    {
        if (zip.GetEntry("xl/sharedStrings.xml") is null)
        {
            return [];
        }

        return XlsFormXml.Load(zip, "xl/sharedStrings.xml").Descendants(S + "si").Select(XlsFormXml.RichText).ToArray();
    }
}
