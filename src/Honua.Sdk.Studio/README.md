# Honua.Sdk.Studio

Browser-safe Console Studio read client and shared analysis-report contracts for
Honua Console hosts (Blazor Web and optional .NET MAUI Blazor Hybrid).

Install directly when a host only needs to retrieve and render analysis reports:

```bash
dotnet add package Honua.Sdk.Studio
```

```csharp
using Honua.Sdk.Studio.Extensions;

builder.Services.AddHonuaStudio(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");
    o.BearerTokenProvider = tokenProvider.GetAccessTokenAsync;
});
```

Use `IHonuaStudioReportsClient` to fetch the structured report envelope and to
render Markdown or HTML for completed geoprocessing jobs. The report contracts
themselves live in `Honua.Sdk.Abstractions` (namespace
`Honua.Sdk.Abstractions.Studio`) so browser and native hosts share one DTO set.

## REST surface

The client targets the analysis-reporting surface under
`/api/v1/analysis/reports`.

| SDK method | HTTP contract |
|---|---|
| `GetReportAsync` | `GET /api/v1/analysis/reports/{jobId}` |
| `RenderReportAsync` | `GET /api/v1/analysis/reports/{jobId}/render?format=md\|html` |

`GetReportAsync` returns `HonuaAnalysisReport` deserialized from the **unwrapped**
server JSON (analysis reports are not wrapped in the Admin `ApiResponse<T>`
envelope). `RenderReportAsync` returns `HonuaRenderedReport` carrying the text
body and its media type (`text/markdown` or `text/html`).

```csharp
var report = await studio.GetReportAsync(jobId, cancellationToken);

foreach (var section in report.Sections)
{
    switch (section)
    {
        case HonuaHeadingSection heading:
            Console.WriteLine(new string('#', heading.Level) + " " + heading.Text);
            break;
        case HonuaKeyMetricSection metric:
            Console.WriteLine($"{metric.Label}: {metric.Value} {metric.Unit}");
            break;
        // Add cases as the report contract adds section kinds; unknown kinds
        // surface as HonuaStudioContractException so drift is visible.
    }
}

var markdown = await studio.RenderReportAsync(
    jobId,
    HonuaReportRenderFormat.Markdown,
    cancellationToken);
```

## Report sections

`HonuaAnalysisReportSection` is a polymorphic hierarchy keyed by the `kind`
discriminator and resolved through a source-generated JSON context
(trimming/AOT-safe). Modeled section kinds:

`heading`, `paragraph`, `key-metric`, `table`, `chart`, `map-embed`,
`narrative`, `provenance-footer`.

The eight kinds are exhaustive within report contract version
`honua.report.v1`. Gate on `HonuaAnalysisReport.ReportContractVersion` for
version-level compatibility. New *fields* on a known kind are tolerated (ignored)
on read; an entirely unmodeled `kind` within a supported contract version is
genuine drift and surfaces as `HonuaStudioContractException` so it is caught
loudly rather than silently dropped.

## Errors

Non-success HTTP statuses throw `HonuaStudioApiException` (preserving
`StatusCode`, problem `Title`/`Detail`, and the raw `ResponseBody`). Successful
responses whose body does not satisfy the report contract throw
`HonuaStudioContractException`.

## Scope

This package wraps the analysis-report read path that the server exposes today.
Map/App package bodies, publication/share/embed, and discrete
query/dashboard/form/workflow/ETL package clients are gated on server contracts
that do not yet exist and are tracked as separate, server-paired tickets.

## Documentation

- [Console client contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/console-client-contracts.md)
- [Client behavior](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/client-behavior.md)
- [Browser / WASM support](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/browser-wasm-support.md)
- [Studio analysis report sample](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/examples/StudioAnalysisReportConsole/README.md)
