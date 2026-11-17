// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Forms.Authoring;

namespace Honua.Sdk.Cli;

internal sealed record FormsDiagnostic
{
    [JsonPropertyName("sheet")]
    public string Sheet { get; init; } = string.Empty;

    [JsonPropertyName("row")]
    public int Row { get; init; }

    [JsonPropertyName("column")]
    public string Column { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    internal static FormsDiagnostic From(XlsFormDiagnostic diagnostic) => new()
    {
        Sheet = diagnostic.Sheet,
        Row = diagnostic.RowNumber,
        Column = diagnostic.Column,
        Message = diagnostic.Message
    };
}

internal sealed record FormsResult
{
    internal static readonly FormsResult Help = new() { ExitCode = 0, Outcome = "help" };

    internal required int ExitCode { get; init; }

    internal required string Outcome { get; init; }

    internal IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();

    internal IReadOnlyList<FormsDiagnostic> Diagnostics { get; init; } = [];

    internal string? Message { get; init; }

    internal string? Document { get; init; }

    internal static FormsResult Succeeded(string outcome, IReadOnlyDictionary<string, string> fields)
        => new() { ExitCode = 0, Outcome = outcome, Fields = fields };

    internal static FormsResult Rejected(string message, IEnumerable<XlsFormDiagnostic> diagnostics)
        => Rejected(message, diagnostics.Select(FormsDiagnostic.From).ToArray());

    internal static FormsResult Rejected(string message, IReadOnlyList<FormsDiagnostic> diagnostics)
        => new() { ExitCode = 1, Outcome = "rejected", Message = message, Diagnostics = diagnostics };

    internal static FormsResult Failed(string message)
        => new() { ExitCode = 1, Outcome = "failed", Message = message };
}

internal static class FormsReport
{
    internal const string Format = "honua.forms-result.v1";

    internal static async Task WriteJsonAsync(FormsResult result, TextWriter output)
    {
        if (ReferenceEquals(result, FormsResult.Help))
            return;
        if (result.Document is not null)
        {
            await output.WriteLineAsync(result.Document).ConfigureAwait(false);
            return;
        }

        FormsReportBody body = new()
        {
            Format = Format,
            Outcome = result.Outcome,
            Fields = result.Fields,
            Message = result.Message,
            Diagnostics = result.Diagnostics
        };
        await output.WriteLineAsync(JsonSerializer.Serialize(body, FormsJsonContext.Default.FormsReportBody)).ConfigureAwait(false);
    }

    internal static async Task WriteTextAsync(FormsResult result, CliStreams streams)
    {
        if (ReferenceEquals(result, FormsResult.Help))
            return;
        if (result.Document is not null)
        {
            await streams.Output.WriteLineAsync(result.Document).ConfigureAwait(false);
            return;
        }

        TextWriter target = result.ExitCode == 0 ? streams.Output : streams.Error;
        if (result.Message is not null)
            await streams.Error.WriteLineAsync(Printable(result.Message)).ConfigureAwait(false);
        foreach (KeyValuePair<string, string> field in result.Fields)
            await target.WriteLineAsync(Printable($"{field.Key}={field.Value}")).ConfigureAwait(false);
        foreach (FormsDiagnostic diagnostic in result.Diagnostics)
        {
            string line = $"{diagnostic.Sheet} row {diagnostic.Row}, column {diagnostic.Column}: {diagnostic.Message}";
            await streams.Error.WriteLineAsync(Printable(line)).ConfigureAwait(false);
        }
    }

    internal static string Printable(string text)
        => string.Create(text.Length, text, static (span, source) =>
        {
            for (int index = 0; index < source.Length; index++)
                span[index] = char.IsControl(source[index]) ? '?' : source[index];
        });
}

internal sealed class FormsReportBody
{
    [JsonPropertyName("format")]
    public string Format { get; init; } = string.Empty;

    [JsonPropertyName("outcome")]
    public string Outcome { get; init; } = string.Empty;

    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("diagnostics")]
    public IReadOnlyList<FormsDiagnostic> Diagnostics { get; init; } = [];
}

internal sealed class FormsImportResponse
{
    [JsonPropertyName("formId")]
    public string FormId { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}

internal sealed class FormsProblemBody
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("diagnostics")]
    public FormsDiagnostic[]? Diagnostics { get; init; }
}

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FormDefinition))]
[JsonSerializable(typeof(FormsReportBody))]
[JsonSerializable(typeof(FormsImportResponse))]
[JsonSerializable(typeof(FormsProblemBody))]
internal sealed partial class FormsJsonContext : JsonSerializerContext
{
}
