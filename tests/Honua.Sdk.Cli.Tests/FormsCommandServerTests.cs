using System.Net;
using System.Text;
using Honua.Sdk.Cli;

namespace Honua.Sdk.Cli.Tests;

public sealed class FormsCommandServerTests : IDisposable
{
    private const string BaseUrl = "https://forms.example.test/honua";
    private const string ApiKey = "test-admin-key";
    private readonly TemporaryDirectory _temporary = new();
    private readonly string? _previousApiKey = Environment.GetEnvironmentVariable("HONUA_API_KEY");

    public FormsCommandServerTests() => Environment.SetEnvironmentVariable("HONUA_API_KEY", ApiKey);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("HONUA_API_KEY", _previousApiKey);
        _temporary.Dispose();
    }

    [Fact]
    public async Task Import_PostsWorkbookAndPrintsDraftFormId()
    {
        string workbook = WriteWorkbook("survey.xlsx", [0x50, 0x4B, 0x03, 0x04]);
        CapturedRequest? captured = null;
        using HttpClient client = new(new StubHandler(async request =>
        {
            captured = await CapturedRequest.ReadAsync(request);
            return Json(HttpStatusCode.Created, """{"formId":"inspection","version":3,"status":"draft","etag":"\"x\""}""");
        }));

        CliRun run = await CliRun.ExecuteAsync(client,
            "forms", "import", workbook, "--service", "parcels", "--layer", "0", "--base-url", BaseUrl);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("formId=inspection\nversion=3\nstatus=draft\n", run.Stdout.ReplaceLineEndings("\n"));
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal($"{BaseUrl}/api/v1/admin/forms/packages/import?serviceId=parcels&layerId=0", captured.Uri);
        Assert.Equal(ApiKey, captured.ApiKey);
        Assert.Contains("name=file", captured.Body, StringComparison.Ordinal);
        Assert.Contains("filename=survey.xlsx", captured.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_ApiKeyWithControlCharacter_ExitsTwoWithoutCallingServer()
    {
        Environment.SetEnvironmentVariable(FormsServer.ApiKeyVariable, "key\r\nX-Injected: 1");
        string workbook = WriteWorkbook("survey.xlsx", [0x50, 0x4B, 0x03, 0x04]);
        StubHandler handler = new(_ => throw new InvalidOperationException("The server must not be called."));
        using HttpClient client = new(handler);

        CliRun run = await CliRun.ExecuteAsync(client,
            "forms", "import", workbook, "--service", "parcels", "--layer", "0", "--base-url", BaseUrl);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("control character", run.Stderr, StringComparison.Ordinal);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Import_MissingApiKey_ExitsTwoWithoutCallingServer()
    {
        Environment.SetEnvironmentVariable(FormsServer.ApiKeyVariable, null);
        string workbook = WriteWorkbook("survey.xlsx", [0x50, 0x4B, 0x03, 0x04]);
        StubHandler handler = new(_ => throw new InvalidOperationException("The server must not be called."));
        using HttpClient client = new(handler);

        CliRun run = await CliRun.ExecuteAsync(client,
            "forms", "import", workbook, "--service", "parcels", "--layer", "0", "--base-url", BaseUrl);

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("HONUA_API_KEY", run.Stderr, StringComparison.Ordinal);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Export_EqualsShortOption_WritesTheNamedFile()
    {
        byte[] workbook = [0x50, 0x4B, 0x03, 0x04, 0x14];
        string output = Path.Combine(_temporary.Path, "named.xlsx");
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(workbook)
        })));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "inspection", "-o=" + output, "--base-url", BaseUrl);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(workbook, File.ReadAllBytes(output));
    }

    [Fact]
    public void DefaultWorkbookName_ReplacesControlCharacters()
    {
        Assert.Equal("in_spection.xlsx", CliFiles.DefaultWorkbookName("in\nspection"));
        Assert.Equal("form.xlsx", CliFiles.DefaultWorkbookName("\n"));
    }

    [Fact]
    public async Task Export_TransportFailure_PrintsSanitizedFormsError()
    {
        using HttpClient client = new(new StubHandler(_ => throw new HttpRequestException("upstream\r\nfailed")));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "inspection", "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("honua forms failed: upstream??failed", run.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", run.Stderr, StringComparison.Ordinal);
        Assert.DoesNotContain("honua doctor", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_DeclaredOversizedResponse_FailsWithoutWriting()
    {
        string output = Path.Combine(_temporary.Path, "inspection.xlsx");
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(OversizedWorkbook())));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "inspection", "-o", output, "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.False(File.Exists(output));
        Assert.Contains("16 MiB", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_CreatedWithoutFormId_Fails()
    {
        string workbook = WriteWorkbook("survey.xlsx", [0x50, 0x4B, 0x03, 0x04]);
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(
            Json(HttpStatusCode.Created, """{"formId":"  ","version":1,"status":"draft"}"""))));

        CliRun run = await CliRun.ExecuteAsync(client,
            "forms", "import", workbook, "--service", "parcels", "--layer", "0", "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("no form id", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_RejectedWorkbook_PrintsEveryCellAndExitsOne()
    {
        string workbook = WriteWorkbook("survey.xlsx", [1, 2, 3]);
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.BadRequest, """
            {"title":"Bad Request","status":400,"detail":"The workbook is not an XLSForm survey a Honua form package can represent.",
             "diagnostics":[{"sheet":"survey","row":4,"column":"type","message":"Unsupported question type 'range'."},
                            {"sheet":"choices","row":2,"column":"list_name","message":"Unused list."}]}
            """))));

        CliRun run = await CliRun.ExecuteAsync(client,
            "forms", "import", workbook, "--service", "parcels", "--layer", "0", "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.Equal(string.Empty, run.Stdout);
        Assert.Contains("HTTP 400: The workbook is not an XLSForm survey", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("survey row 4, column type: Unsupported question type 'range'.", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("choices row 2, column list_name: Unused list.", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Import_RejectedWorkbookAsJson_ReportsDiagnosticsOnStdout()
    {
        string workbook = WriteWorkbook("survey.xlsx", [1]);
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.BadRequest,
            """{"detail":"rejected","diagnostics":[{"sheet":"survey","row":2,"column":"name","message":"Missing."}]}"""))));

        CliRun run = await CliRun.ExecuteAsync(client,
            "forms", "import", workbook, "--service", "parcels", "--layer", "0", "--base-url", BaseUrl, "--json");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("\"outcome\": \"rejected\"", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("\"column\": \"name\"", run.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_WritesWorkbookToOutput()
    {
        byte[] workbook = [0x50, 0x4B, 0x03, 0x04, 0x14];
        string output = Path.Combine(_temporary.Path, "out", "inspection.xlsx");
        CapturedRequest? captured = null;
        using HttpClient client = new(new StubHandler(async request =>
        {
            captured = await CapturedRequest.ReadAsync(request);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(workbook) };
        }));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "inspection form", "-o", output, "--base-url", BaseUrl);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(workbook, File.ReadAllBytes(output));
        Assert.Contains($"output={output}", run.Stdout, StringComparison.Ordinal);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal($"{BaseUrl}/api/v1/admin/forms/packages/inspection%20form/export", captured.Uri);
        Assert.Equal(ApiKey, captured.ApiKey);
        Assert.StartsWith(FormsServer.WorkbookContentType, captured.Accept, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_UnrepresentableForm_PrintsDiagnosticsAndWritesNothing()
    {
        string output = Path.Combine(_temporary.Path, "inspection.xlsx");
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.UnprocessableEntity,
            """{"detail":"The published package uses behavior XLSForm cannot represent.","diagnostics":[{"sheet":"survey","row":3,"column":"type","message":"Signature fields have no XLSForm type."}]}"""))));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "inspection", "-o", output, "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.False(File.Exists(output));
        Assert.Contains("HTTP 422", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("survey row 3, column type: Signature fields have no XLSForm type.", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_NonWorkbookResponse_FailsWithoutWriting()
    {
        string output = Path.Combine(_temporary.Path, "inspection.xlsx");
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>sign in</html>", Encoding.UTF8, "text/html")
        })));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "inspection", "-o", output, "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.False(File.Exists(output));
        Assert.Contains("not an XLSX workbook", run.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_MissingForm_ReportsStatusWithoutDiagnostics()
    {
        using HttpClient client = new(new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.NotFound,
            """{"detail":"Published form package 'missing' was not found."}"""))));

        CliRun run = await CliRun.ExecuteAsync(client, "forms", "export", "missing",
            "-o", Path.Combine(_temporary.Path, "missing.xlsx"), "--base-url", BaseUrl);

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("HTTP 404: Published form package 'missing' was not found.", run.Stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("import", "survey.xlsx", "--layer", "0", "--base-url", BaseUrl)]
    [InlineData("import", "survey.xlsx", "--service", "parcels", "--layer", "first", "--base-url", BaseUrl)]
    [InlineData("import", "survey.xlsx", "--service", "parcels", "--layer", "0", "--base-url", "http://forms.example.test")]
    [InlineData("export", "inspection", "--base-url", BaseUrl, "--local")]
    [InlineData("export", "one", "two", "--base-url", BaseUrl)]
    [InlineData("publish", "inspection")]
    [InlineData("export", "..", "--base-url", BaseUrl)]
    [InlineData("export", "inspection", "--unknown", "x")]
    [InlineData("import", "survey.xlsx", "--service", "parcels", "--layer", "0", "--base-url", BaseUrl, "-o", "out.json")]
    [InlineData("export", "inspection", "--base-url", BaseUrl, "--output=")]
    public async Task InvalidArguments_ExitTwoWithoutCallingServer(params string[] arguments)
    {
        StubHandler handler = new(_ => throw new InvalidOperationException("The server must not be called."));
        using HttpClient client = new(handler);

        CliRun run = await CliRun.ExecuteAsync(client, ["forms", .. arguments]);

        Assert.Equal(2, run.ExitCode);
        Assert.NotEqual(string.Empty, run.Stderr);
        Assert.Equal(0, handler.CallCount);
    }

    private static HttpResponseMessage OversizedWorkbook()
    {
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x50, 0x4B, 0x03, 0x04])
        };
        response.Content.Headers.ContentLength = 17 * 1024 * 1024;
        return response;
    }

    private string WriteWorkbook(string name, byte[] content)
    {
        string path = Path.Combine(_temporary.Path, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string? ApiKey, string Accept, string Body)
    {
        internal static async Task<CapturedRequest> ReadAsync(HttpRequestMessage request)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            string? apiKey = request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? values) ? values.Single() : null;
            string accept = request.Headers.Accept.ToString();
            return new CapturedRequest(request.Method, request.RequestUri!.AbsoluteUri, apiKey, accept, body);
        }
    }
}

internal sealed record CliRun(int ExitCode, string Stdout, string Stderr)
{
    internal static async Task<CliRun> ExecuteAsync(HttpClient client, params string[] args)
    {
        StringWriter stdout = new();
        StringWriter stderr = new();
        int exitCode = await HonuaCli.RunAsync(args, stdout, stderr, client);
        return new CliRun(exitCode, stdout.ToString(), stderr.ToString());
    }
}

internal sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    internal int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return respond(request);
    }
}
