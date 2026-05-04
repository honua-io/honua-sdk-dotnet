using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Spec;
using Honua.Sdk.Spec.Models;

var mode = Environment.GetEnvironmentVariable("HONUA_SPEC_MODE") ?? "simulated";
var useServer = string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase);
var baseAddress = new Uri(Environment.GetEnvironmentVariable("HONUA_SPEC_SERVER_URL") ?? "https://localhost:5001");
var apiKey = Environment.GetEnvironmentVariable("HONUA_SPEC_API_KEY");
var bearerToken = Environment.GetEnvironmentVariable("HONUA_SPEC_BEARER_TOKEN");

if (useServer && HasCredentials(apiKey, bearerToken) && RequiresHttpsForAuthentication(baseAddress))
{
    Console.Error.WriteLine("Authenticated Spec requests require HTTPS, except loopback HTTP for local development.");
    return 2;
}

using HttpMessageHandler transportHandler = useServer ? new HttpClientHandler() : new SimulatedSpecHandler();
using var authHandler = new DemoAuthHandler(apiKey, bearerToken, transportHandler);
using var http = new HttpClient(authHandler, disposeHandler: false)
{
    BaseAddress = baseAddress
};

var client = new HonuaSpecClient(http);
var document = CreateDocument();

Console.WriteLine($"Mode: {(useServer ? "server" : "simulated")}");
Console.WriteLine($"Spec: {document.SpecId}");
Console.WriteLine();

try
{
    var plan = await client.PlanAsync(document);
    PrintPlan(plan);

    await using var apply = await client.ApplyAsync(document);
    await PrintApplyAsync(apply);
    return 0;
}
catch (HonuaSpecException ex)
{
    Console.Error.WriteLine($"Spec request failed: {(int)ex.StatusCode} {ex.Message}");
    return 3;
}

static SpecDocumentRequest CreateDocument() => new()
{
    GrammarVersion = "2026.1",
    ProcessFamilyVersion = "2026.1",
    SpecId = "dotnet-demo-suite",
    CacheMode = SpecCacheMode.ReadWrite,
    MaxConcurrency = 2,
    Nodes =
    [
        new SpecNodeRequest
        {
            Id = "source-permits",
            Kind = SpecResourceKind.Dataset,
            SourcePins = new Dictionary<string, string>
            {
                ["provider"] = "honua",
                ["collection"] = "permits"
            },
            CanonicalFragment = "dataset:permits"
        },
        new SpecNodeRequest
        {
            Id = "active-permits",
            Kind = SpecResourceKind.Compute,
            Op = "filter",
            Inputs = new Dictionary<string, string>
            {
                ["source"] = "source-permits"
            },
            Parameters = new Dictionary<string, string>
            {
                ["where"] = "status = 'active'"
            },
            CanonicalFragment = "filter:status-active"
        },
        new SpecNodeRequest
        {
            Id = "operator-summary",
            Kind = SpecResourceKind.Report,
            Op = "summarize",
            Inputs = new Dictionary<string, string>
            {
                ["source"] = "active-permits"
            },
            Parameters = new Dictionary<string, string>
            {
                ["groupBy"] = "district"
            },
            CanonicalFragment = "summarize:district"
        }
    ]
};

static void PrintPlan(SpecPlanResponse plan)
{
    Console.WriteLine($"Plan: {plan.PlanId}");
    foreach (var node in plan.Nodes)
    {
        var dependencies = node.DependsOn.Count == 0 ? "(none)" : string.Join(", ", node.DependsOn);
        Console.WriteLine($"  {node.NodeId} [{node.Kind}] deps={dependencies} hash={node.ContentHash}");
    }

    if (plan.Warnings.Count > 0)
    {
        Console.WriteLine("Warnings:");
        foreach (var warning in plan.Warnings)
        {
            Console.WriteLine($"  {warning.Code}: {warning.Message}");
        }
    }

    Console.WriteLine();
}

static async Task PrintApplyAsync(SpecApplyStream apply)
{
    Console.WriteLine($"Apply: {apply.ApplyToken ?? "(server did not return a token header)"}");
    SpecApplySummary? summary = null;

    await foreach (var evt in apply.Events)
    {
        var node = string.IsNullOrWhiteSpace(evt.NodeId) ? string.Empty : $" {evt.NodeId}";
        Console.WriteLine($"  #{evt.Sequence} {evt.Kind}{node}");
        summary = evt.Summary ?? summary;
    }

    if (summary is not null)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Summary: total={summary.TotalNodes} ran={summary.RanNodes} cached={summary.CachedNodes} failed={summary.FailedNodes}");
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

internal sealed class SimulatedSpecHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath;
        return Task.FromResult(path switch
        {
            "/v1/spec/plan" => JsonResponse(PlanJson()),
            "/v1/spec/apply" => SseResponse(ApplyEvents()),
            _ => JsonResponse(
                """
                {"type":"about:blank","title":"Not found","status":404,"detail":"Simulated endpoint not found.","code":"not_found"}
                """,
                HttpStatusCode.NotFound)
        });
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage SseResponse(string events)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(events, Encoding.UTF8, "text/event-stream")
        };
        response.Headers.TryAddWithoutValidation("X-Spec-Apply-Token", "apply-dotnet-demo-suite");
        return response;
    }

    private static string PlanJson() =>
        """
        {
          "planId": "plan-dotnet-demo-suite",
          "grammarVersion": "2026.1",
          "processFamilyVersion": "2026.1",
          "nodes": [
            {
              "nodeId": "source-permits",
              "kind": "Dataset",
              "dependsOn": [],
              "contentHash": "sha256:source-permits",
              "cost": { "estimatedRows": 1200, "estimatedBytes": 256000, "estimatedDurationMs": 0 }
            },
            {
              "nodeId": "active-permits",
              "kind": "Compute",
              "op": "filter",
              "dependsOn": [ "source-permits" ],
              "contentHash": "sha256:active-permits",
              "cost": { "estimatedRows": 420, "estimatedBytes": 86000, "estimatedDurationMs": 125 }
            },
            {
              "nodeId": "operator-summary",
              "kind": "Report",
              "op": "summarize",
              "dependsOn": [ "active-permits" ],
              "contentHash": "sha256:operator-summary",
              "cost": { "estimatedRows": 12, "estimatedBytes": 4096, "estimatedDurationMs": 75 }
            }
          ],
          "warnings": []
        }
        """;

    private static string ApplyEvents() =>
        """
        data: {"sequence":1,"kind":"ApplyStarted","applyToken":"apply-dotnet-demo-suite","timestamp":"2026-05-03T12:00:00Z"}

        data: {"sequence":2,"kind":"Cached","applyToken":"apply-dotnet-demo-suite","nodeId":"source-permits","contentHash":"sha256:source-permits","timestamp":"2026-05-03T12:00:01Z"}

        data: {"sequence":3,"kind":"Running","applyToken":"apply-dotnet-demo-suite","nodeId":"active-permits","timestamp":"2026-05-03T12:00:02Z"}

        data: {"sequence":4,"kind":"Succeeded","applyToken":"apply-dotnet-demo-suite","nodeId":"active-permits","contentHash":"sha256:active-permits","timestamp":"2026-05-03T12:00:03Z","actualCost":{"rows":420,"bytes":86000,"durationMs":118}}

        data: {"sequence":5,"kind":"ApplyCompleted","applyToken":"apply-dotnet-demo-suite","timestamp":"2026-05-03T12:00:04Z","summary":{"totalNodes":3,"cachedNodes":1,"ranNodes":1,"failedNodes":0,"skippedNodes":0,"totalDurationMs":150,"cancelled":false}}

        """;
}
