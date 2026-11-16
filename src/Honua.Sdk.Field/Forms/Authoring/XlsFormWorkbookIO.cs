// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.IO.Compression;
using System.Xml;

namespace Honua.Sdk.Field.Forms.Authoring;

/// <summary>Reads and writes bounded XLSForm .xlsx workbooks without evaluating formulas.</summary>
public static class XlsFormWorkbookIO
{
    /// <summary>Reads a workbook, leaving the input stream open and reporting unsupported content.</summary>
    public static XlsFormResult<XlsFormWorkbook> Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            using var buffer = new MemoryStream();
            CopyBounded(input, buffer);
            buffer.Position = 0;
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read);
            XlsFormXml.CheckArchive(zip);
            return new XlsFormWorkbookReader(zip).Read();
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException)
        {
            return new XlsFormResult<XlsFormWorkbook>
            {
                Diagnostics = [new XlsFormDiagnostic
                {
                    Sheet = "workbook", RowNumber = 1, Column = "file",
                    Message = "Invalid or unsupported workbook: " + ex.Message
                }]
            };
        }
    }

    /// <summary>Writes rows as literal cell text, leaving the output stream open.</summary>
    public static void Write(Stream output, XlsFormWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(workbook);
        XlsFormWorkbookWriter.Write(output, workbook);
    }

    private static void CopyBounded(Stream input, Stream output)
    {
        const int maxBytes = 8 * 1024 * 1024;
        var buffer = new byte[8192];
        var total = 0;
        int count;
        while ((count = input.Read(buffer)) > 0)
        {
            total += count;
            if (total > maxBytes)
            {
                throw new InvalidDataException("The compressed workbook exceeds 8 MiB.");
            }

            output.Write(buffer.AsSpan(0, count));
        }
    }
}
