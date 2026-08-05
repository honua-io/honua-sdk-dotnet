# Honua.Sdk.Studio

Browser-safe Console Studio clients and shared contracts for Honua Console hosts
(Blazor Web and optional .NET MAUI Blazor Hybrid). The package ships three typed
clients registered by a single `AddHonuaStudio` call:

- `IHonuaStudioReportsClient` — retrieve/render analysis reports.
- `IHonuaCapabilityManifestClient` — fetch the server capability manifest
  (`GET /api/v1/capabilities/manifest`, schema `honua.capability_manifest.v1`) to
  gate authoring UI and tool exposure on what the connected server supports.
- `IHonuaStudioPackageClient` — the Studio package lifecycle
  (`/api/v1/studio/*`): family capabilities, draft CRUD, validate, preview-plan,
  content-version, publish-request, reopen, and rollback across every in-scope
  package family (query, map, analysis, dashboard, report, form, app, workflow,
  gp, etl).

The projected shapes mirror the honua-sdk-js Studio contract
(`src/studio/{types,validation,capability-manifest}.ts`) so the .NET and JS
projections do not diverge.

## Install

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.Studio --source honua
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
envelope). `RenderReportAsync` requests `text/markdown` for `format=md` and
`text/html` for `format=html`, then returns `HonuaRenderedReport` carrying the
text body and media type. If the response omits `Content-Type`, the SDK reports
the media type implied by the requested format.

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

## Capability manifest

`IHonuaCapabilityManifestClient.GetManifestAsync` fetches the frozen
`honua.capability_manifest.v1` document into a `CapabilityManifest`. Query
helpers mirror the JS `getCapability`/`hasCapability` helpers:

```csharp
var manifest = await capabilities.GetManifestAsync(cancellationToken: ct);

if (manifest.IsAvailable("studio.map")) { /* enable the map builder */ }
if (!manifest.IsAvailable("studio.ai.generate"))
{
    var reason = manifest.GetReasonCode("studio.ai.generate"); // e.g. "entitlement-inactive"
}
if (manifest.HasPackageFamily("dashboard")) { /* show the dashboard family */ }
```

`GetCapability(id)` returns the full `CapabilityEntry` (`Supported`, `Available`,
`ReasonCode`, `EntitlementKey`, `MinimumEdition`, …). The manifest also carries
scope, server/environment info, transports, limits, and entitlement policy.

## Studio package lifecycle

`IHonuaStudioPackageClient` covers the family-agnostic lifecycle; the family
discriminant travels on `StudioPackageEnvelope`. The envelope's `Bindings`,
`Dependencies`, and `Provenance` collections always serialize as arrays (the
server rejects null for any of them).

```csharp
var draft = await packages.CreateDraftAsync(new CreateStudioPackageDraftRequest
{
    PackageKey = "my-map",
    Envelope = new StudioPackageEnvelope
    {
        Family = StudioPackageFamily.Map,
        SchemaVersion = "honua_map_package.v1",
    },
}, ct);

var validation = await packages.ValidateDraftAsync(draft.DraftId, ct);
var plan = await packages.PreviewPlanAsync(draft.DraftId, ct);
var version = await packages.CreateContentVersionAsync(
    draft.DraftId, new SaveStudioContentVersionRequest { ChangeNote = "v1" }, ct);
var publish = await packages.CreatePublishRequestAsync(
    version.ItemId, version.VersionId, new CreateStudioPublicationRequest(), ct);
```

## Scope

AI generate endpoints (`/api/v1/studio/{map,app}-packages/generate`) return
AI-specific result shapes and are tracked separately.

## Documentation

- [Console client contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/console-client-contracts.md)
- [Client behavior](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/client-behavior.md)
- [Browser / WASM support](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/browser-wasm-support.md)
- [Studio analysis report sample](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/examples/StudioAnalysisReportConsole/README.md)
