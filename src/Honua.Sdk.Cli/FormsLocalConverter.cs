// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text;
using System.Text.Json;
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Forms.Authoring;

namespace Honua.Sdk.Cli;

/// <summary>Converts between XLSForm workbooks and form definition JSON without a server.</summary>
internal static class FormsLocalConverter
{
    internal static FormsResult Import(string workbookPath, string? outputPath)
    {
        XlsFormResult<XlsFormWorkbook> workbook;
        using (FileStream stream = new(workbookPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            workbook = XlsFormWorkbookIO.Read(stream);
        if (workbook.Value is null)
            return FormsResult.Rejected("The workbook cannot be read as an XLSForm survey.", workbook.Diagnostics);

        XlsFormResult<FormDefinition> form = XlsFormConverter.Import(workbook.Value);
        if (form.Value is null)
            return FormsResult.Rejected("The workbook uses XLSForm content a Honua form cannot represent.", form.Diagnostics);

        string json = JsonSerializer.Serialize(form.Value, FormsJsonContext.Default.FormDefinition);
        if (outputPath is null)
            return new FormsResult { ExitCode = 0, Outcome = "converted", Document = json };

        string destination = CliFiles.WriteAtomically(outputPath, Encoding.UTF8.GetBytes(json + "\n"));
        return FormsResult.Succeeded("converted", new Dictionary<string, string>
        {
            ["formId"] = form.Value.FormId,
            ["output"] = destination
        });
    }

    internal static async Task<FormsResult> ExportAsync(string formPath, string? outputPath, CancellationToken cancellationToken)
    {
        byte[] content = await CliFiles.ReadBoundedAsync(formPath, CliFiles.MaxWorkbookBytes, cancellationToken)
            .ConfigureAwait(false);
        FormDefinition form = JsonSerializer.Deserialize(content, FormsJsonContext.Default.FormDefinition)
            ?? throw new JsonException("The form definition is empty.");

        XlsFormResult<XlsFormWorkbook> exported = XlsFormConverter.Export(form);
        if (exported.Value is null)
            return FormsResult.Rejected("The form uses behavior XLSForm cannot represent.", exported.Diagnostics);

        using MemoryStream buffer = new();
        XlsFormWorkbookIO.Write(buffer, exported.Value);
        string destination = CliFiles.WriteAtomically(outputPath ?? CliFiles.DefaultWorkbookName(form.FormId), buffer.ToArray());
        return FormsResult.Succeeded("converted", new Dictionary<string, string>
        {
            ["formId"] = form.FormId,
            ["output"] = destination
        });
    }
}
