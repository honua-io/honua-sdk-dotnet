using System.Text.Json;
using Honua.Sdk.Field.Forms.Authoring;

namespace Honua.Sdk.Cli.Tests;

public sealed class FormsCommandLocalTests : IDisposable
{
    private readonly TemporaryDirectory _temporary = new();
    private readonly StubHandler _handler = new(_ => throw new InvalidOperationException("--local must not call a server."));

    public void Dispose() => _temporary.Dispose();

    [Fact]
    public async Task LocalImportThenExport_RoundTripsTheWorkbook()
    {
        string workbook = WriteWorkbook("survey.xlsx",
            Row(2, ("type", "integer"), ("name", "age"), ("label", "Age"), ("required", "yes")),
            Row(3, ("type", "select_one colors"), ("name", "color"), ("label", "Color")));
        string formJson = Path.Combine(_temporary.Path, "form.json");
        string exported = Path.Combine(_temporary.Path, "exported.xlsx");
        using HttpClient client = new(_handler);

        CliRun import = await CliRun.ExecuteAsync(client, "forms", "import", workbook, "--local", "-o", formJson);
        CliRun export = await CliRun.ExecuteAsync(client, "forms", "export", formJson, "--local", "-o", exported, "--json");
        CliRun reimport = await CliRun.ExecuteAsync(client, "forms", "import", exported, "--local");

        Assert.Equal((0, 0, 0), (import.ExitCode, export.ExitCode, reimport.ExitCode));
        Assert.Contains("formId=survey", import.Stdout, StringComparison.Ordinal);
        Assert.Contains("\"outcome\": \"converted\"", export.Stdout, StringComparison.Ordinal);
        Assert.Equal(File.ReadAllText(formJson).TrimEnd(), reimport.Stdout.TrimEnd());
        Assert.Equal(0, _handler.CallCount);
    }

    [Fact]
    public async Task LocalImport_WithoutOutput_WritesFormJsonToStdout()
    {
        string workbook = WriteWorkbook("survey.xlsx", Row(2, ("type", "text"), ("name", "notes"), ("label", "Notes")));
        using HttpClient client = new(_handler);

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "import", workbook, "--local");

        Assert.Equal(0, run.ExitCode);
        using JsonDocument form = JsonDocument.Parse(run.Stdout);
        Assert.Equal("survey", form.RootElement.GetProperty("formId").GetString());
        string fieldId = form.RootElement.GetProperty("sections")[0].GetProperty("fields")[0].GetProperty("fieldId").GetString()!;
        Assert.Equal("notes", fieldId);
    }

    [Fact]
    public async Task LocalImport_UnsupportedRow_PrintsTheCellAndExitsOne()
    {
        string workbook = WriteWorkbook("survey.xlsx", Row(2, ("type", "select_one missing"), ("name", "choice")));
        string formJson = Path.Combine(_temporary.Path, "form.json");
        using HttpClient client = new(_handler);

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "import", workbook, "--local", "-o", formJson);

        Assert.Equal(1, run.ExitCode);
        Assert.False(File.Exists(formJson));
        Assert.Contains("survey row 2, column type:", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalImport_NotAWorkbook_ReportsTheFileAndExitsOne()
    {
        string path = Path.Combine(_temporary.Path, "notes.xlsx");
        await File.WriteAllTextAsync(path, "not a zip archive");
        using HttpClient client = new(_handler);

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "import", path, "--local");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("workbook row 1, column file:", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocalExport_InvalidFormJson_FailsWithoutOutput()
    {
        string path = Path.Combine(_temporary.Path, "form.json");
        string output = Path.Combine(_temporary.Path, "form.xlsx");
        await File.WriteAllTextAsync(path, "{ not json");
        using HttpClient client = new(_handler);

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", path, "--local", "-o", output);

        Assert.Equal(1, run.ExitCode);
        Assert.False(File.Exists(output));
        Assert.Contains("not valid JSON", run.Stderr, StringComparison.Ordinal);
    }

    private string WriteWorkbook(string name, params XlsFormRow[] survey)
    {
        XlsFormWorkbook workbook = new()
        {
            Survey = survey,
            Choices =
            [
                Row(2, ("list_name", "colors"), ("name", "red"), ("label", "Red")),
                Row(3, ("list_name", "colors"), ("name", "blue"), ("label", "Blue"))
            ],
            Settings = [Row(2, ("form_id", "survey"), ("form_title", "Survey"), ("version", "1"))]
        };
        string path = Path.Combine(_temporary.Path, name);
        using FileStream stream = File.Create(path);
        XlsFormWorkbookIO.Write(stream, workbook);
        return path;
    }

    private static XlsFormRow Row(int number, params (string Key, string Value)[] cells) => new()
    {
        RowNumber = number,
        Cells = cells.ToDictionary(c => c.Key, c => c.Value, StringComparer.Ordinal)
    };
}
