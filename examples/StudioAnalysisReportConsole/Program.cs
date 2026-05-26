using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio;
using Honua.Sdk.Studio.Exceptions;

var mode = Environment.GetEnvironmentVariable("HONUA_STUDIO_MODE") ?? "simulated";
var useServer = string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase);
var baseAddress = new Uri(Environment.GetEnvironmentVariable("HONUA_STUDIO_SERVER_URL") ?? "https://localhost:5001");
var apiKey = Environment.GetEnvironmentVariable("HONUA_STUDIO_API_KEY");
var bearerToken = Environment.GetEnvironmentVariable("HONUA_STUDIO_BEARER_TOKEN");
var jobId = Environment.GetEnvironmentVariable("HONUA_STUDIO_JOB_ID") ?? "job-demo";

if (useServer && HasCredentials(apiKey, bearerToken) && RequiresHttpsForAuthentication(baseAddress))
{
    Console.Error.WriteLine("Authenticated Studio requests require HTTPS, except loopback HTTP for local development.");
    return 2;
}

// Browser/Blazor Web and native MAUI hosts both register the same client via
// AddHonuaStudio(...); the difference is auth and transport configuration:
//
//   builder.Services.AddHonuaStudio(o =>
//   {
//       o.BaseAddress = serverUri;
//       o.BearerTokenProvider = tokenProvider.GetAccessTokenAsync;   // Blazor Web / BFF
//       // o.PrimaryHttpMessageHandlerFactory = () => nativeMtlsHandler; // MAUI native mTLS
//   });
//
// This sample drives the client directly so it can run without a DI host.
using HttpMessageHandler transportHandler = useServer ? new HttpClientHandler() : new SimulatedStudioHandler();
using var authHandler = new DemoAuthHandler(apiKey, bearerToken, transportHandler);
using var http = new HttpClient(authHandler, disposeHandler: false)
{
    BaseAddress = baseAddress
};

var client = new HonuaStudioReportsClient(http);

Console.WriteLine($"Mode: {(useServer ? "server" : "simulated")}");
Console.WriteLine($"Job: {jobId}");
Console.WriteLine();

try
{
    var report = await client.GetReportAsync(jobId);
    PrintReport(report);

    var rendered = await client.RenderReportAsync(jobId, HonuaReportRenderFormat.Markdown);
    Console.WriteLine();
    Console.WriteLine($"Rendered ({rendered.MediaType}):");
    Console.WriteLine(rendered.Content);
    return 0;
}
catch (HonuaStudioApiException ex)
{
    Console.Error.WriteLine($"Studio request failed: {(int)ex.StatusCode} {ex.Message}");
    return 3;
}
catch (HonuaStudioContractException ex)
{
    Console.Error.WriteLine($"Studio response did not satisfy the contract: {ex.Message}");
    return 4;
}

static void PrintReport(HonuaAnalysisReport report)
{
    Console.WriteLine($"Report: {report.ReportId} ({report.ReportContractVersion})");
    Console.WriteLine($"Process: {report.ProcessId} [family={report.ProcessFamily}] narrative={report.NarrativeMode}");
    Console.WriteLine($"Summary: {report.Summary.Title}");
    Console.WriteLine("Sections:");

    foreach (var section in report.Sections)
    {
        switch (section)
        {
            case HonuaHeadingSection heading:
                Console.WriteLine($"  [{heading.Kind}] h{heading.Level}: {heading.Text}");
                break;
            case HonuaParagraphSection paragraph:
                Console.WriteLine($"  [{paragraph.Kind}] {paragraph.Text}");
                break;
            case HonuaKeyMetricSection metric:
                Console.WriteLine($"  [{metric.Kind}] {metric.Label}: {metric.Value} {metric.Unit}".TrimEnd());
                break;
            case HonuaTableSection table:
                Console.WriteLine($"  [{table.Kind}] {table.Columns.Count} columns, {table.Rows.Count} rows (+{table.TruncatedRowCount} truncated)");
                break;
            case HonuaChartSection chart:
                Console.WriteLine($"  [{chart.Kind}] {chart.ChartKind} chart, {chart.Series.Count} series");
                break;
            case HonuaMapEmbedSection map:
                Console.WriteLine($"  [{map.Kind}] {map.Caption} -> {map.MapPackageUri}");
                break;
            case HonuaNarrativeSection narrative:
                Console.WriteLine($"  [{narrative.Kind}] {(narrative.LlmText ?? narrative.DeterministicText)}");
                break;
            case HonuaProvenanceFooterSection footer:
                Console.WriteLine($"  [{footer.Kind}] sources={string.Join(", ", footer.Sources)}");
                break;
            default:
                Console.WriteLine($"  [{section.Kind}] (unexpected section subtype)");
                break;
        }
    }
}

static bool HasCredentials(string? apiKey, string? bearerToken) =>
    !string.IsNullOrWhiteSpace(apiKey) || !string.IsNullOrWhiteSpace(bearerToken);

static bool RequiresHttpsForAuthentication(Uri uri)
{
    if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    return !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        (!uri.IsLoopback && !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}

internal sealed class DemoAuthHandler : DelegatingHandler
{
    private readonly string? _apiKey;
    private readonly string? _bearerToken;

    public DemoAuthHandler(string? apiKey, string? bearerToken, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _apiKey = apiKey;
        _bearerToken = bearerToken;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        }

        if (!string.IsNullOrWhiteSpace(_bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}

internal sealed class SimulatedStudioHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        if (path.EndsWith("/render", StringComparison.Ordinal))
        {
            return Task.FromResult(TextResponse(RenderedMarkdown(), "text/markdown"));
        }

        if (path.StartsWith("/api/v1/analysis/reports/", StringComparison.Ordinal))
        {
            return Task.FromResult(JsonResponse(ReportJson()));
        }

        return Task.FromResult(JsonResponse(
            """
            {"type":"about:blank","title":"Not Found","status":404,"detail":"Simulated endpoint not found."}
            """,
            HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage TextResponse(string text, string mediaType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(text, Encoding.UTF8)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType) { CharSet = "utf-8" };
        return response;
    }

    private static string ReportJson() =>
        """
        {
          "reportId": "report-demo-001",
          "reportContractVersion": "honua.report.v1",
          "jobId": "job-demo",
          "resultPackageId": "result-pkg-demo",
          "processId": "analytics.buffer-aggregate",
          "processFamily": "analytics",
          "templateId": "analysis-report.generic",
          "templateVersion": "1.0.0",
          "summary": { "title": "Flood buffer impact summary" },
          "sections": [
            { "kind": "heading", "text": "Flood Buffer Impact", "level": 1 },
            { "kind": "key-metric", "label": "Affected population", "value": "12,480", "unit": "people" },
            { "kind": "map-embed", "caption": "Flood buffer overlay", "mapPackageUri": "honua://map-packages/map-demo" },
            { "kind": "narrative", "slotId": "summary", "deterministicText": "12,480 people are within the buffer.", "mode": "deterministic" }
          ],
          "narrativeMode": "deterministic",
          "provenance": { "sources": [ { "sourceId": "dataset:flood-extent" } ], "processDefinitions": [ "geometry.buffer" ] },
          "generatedAt": "2026-05-24T18:00:00Z"
        }
        """;

    private static string RenderedMarkdown() =>
        """
        # Flood Buffer Impact

        **Affected population:** 12,480 people

        12,480 people are within the buffer.
        """;
}
