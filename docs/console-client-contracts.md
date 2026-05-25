# Console Client Contracts

Honua Console uses the SDK as its .NET contract boundary for the Blazor Web
host, shared Razor components, and optional .NET MAUI Blazor Hybrid host. The
SDK owns reusable contracts and transport clients; Console hosts own UI,
profile persistence, OS certificate access, secure storage, and lifecycle
integration.

## Package Map

| Console need | Browser / Blazor Web | Native / MAUI Hybrid |
| --- | --- | --- |
| P0 shell, route guards, environment selection | `Honua.Sdk.Abstractions` (`HonuaConsoleShellDescriptor`, route guards, environment profiles) | Same contracts |
| Metadata, RBAC, users, alerts, streaming operations, observability | `Honua.Sdk.Admin` REST over browser `HttpClient` or a BFF | Same Admin REST client |
| Process/job discovery and polling | `Honua.Sdk.Processes` OGC API Processes REST | Same REST models, plus native gRPC when needed |
| Native ProcessService job lifecycle | Not a browser runtime surface | `Honua.Sdk.Grpc` (`IHonuaProcessGrpcClient`) |
| Spec validate/plan/apply workflows | `Honua.Sdk.Spec` REST/SSE candidate | Same REST/SSE client |
| Analysis report retrieve/render | `Honua.Sdk.Studio` (`IHonuaStudioReportsClient`) | Same client |
| Generated artifact retrieval | `Honua.Sdk.Spec` (`IHonuaSpecClient.GetArtifactAsync`) | Same client |
| Server capability / edition gating | `Honua.Sdk.Admin` (`GetCapabilitiesAsync`, `GetLicenseEntitlementsAsync`) | Same Admin REST client |

`Honua.Sdk.Grpc` is intentionally native-only. Browser hosts should use REST
clients and browser-safe realtime transports exposed by the server or BFF.

## Shell And Route Guards

`Honua.Sdk.Abstractions.Console` provides DTOs for shell bootstrap and route
protection:

- `HonuaConsoleShellDescriptor`
- `HonuaConsolePrincipal`
- `HonuaConsoleNavigationItem`
- `HonuaConsoleRouteGuard`
- `HonuaConsoleRouteGuardDecision`
- `HonuaConsolePermissionGrant`

The route guard fixture at
`contracts/fixtures/console/route-guard-rbac.v1.json` includes allowed,
denied, and challenge decisions and is validated by
`ConsoleContractFixtureTests`.

## Environment Profiles

`Honua.Sdk.Abstractions.Environments` provides host-neutral environment
profiles:

- `HonuaEnvironmentProfileSet`
- `HonuaEnvironmentProfile`
- `HonuaTenantScope`
- `HonuaEnvironmentAuthMode`
- `HonuaTransportCapabilities`
- `HonuaTrustProfile`
- `HonuaClientCertificateReference`
- `HonuaEnvironmentTrustState`

The mTLS state is explicit through `HonuaCertificateValidationStatus`:
`NotConfigured`, `Missing`, `Expired`, `ExpiringSoon`, `Untrusted`,
`Rejected`, `WrongEnvironment`, and `Ready`.

The SDK stores certificate selectors only. It never stores certificate bytes,
private keys, secret file paths, or OS keychain handles. Native hosts resolve
the selector, perform platform trust checks, and then write sanitized status
back into the profile.

## Browser Versus Native Transport

Browser-safe clients use `HttpClient` REST/OpenAPI and server-sent event style
responses where supported. They require browser-safe auth: delegated bearer
tokens, same-origin sessions, or a BFF. Privileged Admin API keys and service
tokens must stay server-side.

Native MAUI hosts can use the same DTO packages and add native capabilities:

- `PrimaryHttpMessageHandlerFactory` for REST/gRPC handlers that attach client
  certificates or enterprise trust configuration.
- `IHonuaProcessGrpcClient` for server-backed ProcessService job lifecycle calls.
- `HonuaEnvironmentAuthMode.NativeMutualTls` and trust-state DTOs for profile
  selection and diagnostics.

## Control-Plane REST Contracts

`Honua.Sdk.Admin` now exposes narrow Console-facing sub-interfaces in addition
to the existing service, publishing, metadata, observability, deployment, and
identity surfaces. Prefer injecting the narrow interface at call sites:

| Console workflow | Interface | Response contract |
| --- | --- | --- |
| Route guard role management | `IHonuaAdminRolesClient` | `RoleResponse`, `PermissionGrantResponse` |
| Operator list and effective permissions | `IHonuaAdminUsersClient` | `UserListResponse`, `UserResponse`, `EffectivePermissionsResponse` |
| Alert zone and rule editing | `IHonuaAdminAlertsClient` | `AlertZoneResponse`, `AlertRuleResponse` |
| Feature-event replay / recovery | `IHonuaAdminFeatureEventsClient` | `FeatureEventReplayResponse` with `NextCursor` / `HasMore` |
| Streaming subscriber operations | `IHonuaAdminStreamingOperationsClient` | `SubscriberListResponse` |

Most Admin endpoints use the standard Admin `ApiResponse<T>` envelope and are
unwrapped by `HonuaAdminClient`. The Console publishing/table-discovery path
returns the raw `TableDiscoveryResponse`, and feature-event replay returns the
raw `FeatureEventReplayResponse`, because those server endpoints are not
envelope-wrapped.
Admin requests are emitted as camelCase JSON, and Admin response binding is
case-insensitive. The replay fixture intentionally covers the current
server-shaped PascalCase raw payload (`Events`, `NextCursor`, `HasMore`, and
event fields), which still maps to the typed SDK properties.
Non-success HTTP statuses throw `HonuaAdminApiException`; successful responses
that do not satisfy the expected contract throw `HonuaAdminOperationException`.

```csharp
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;

public sealed class RouteGuardStore(
    IHonuaAdminUsersClient users,
    IHonuaAdminRolesClient roles)
{
    public async Task<EffectivePermissionsResponse> LoadAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var configuredRoles = await roles.ListRolesAsync(cancellationToken);
        var effective = await users.GetEffectivePermissionsAsync(userId, cancellationToken);
        var knownRoleNames = configuredRoles.Select(role => role.Name).ToHashSet(StringComparer.Ordinal);

        if (!effective.Roles.All(knownRoleNames.Contains))
        {
            throw new InvalidOperationException("Effective permissions reference an unknown role.");
        }

        return effective;
    }
}
```

## Process And Job Contracts

`Honua.Sdk.Processes` provides the browser-safe OGC API Processes REST surface
under `/ogc/processes`. `SubmitJobAsync` posts an execution request to
`/ogc/processes/processes/{processId}/execution` with `Prefer: respond-async`
and returns the accepted `HonuaProcessJobStatus`.

| REST method | Path | SDK method |
| --- | --- | --- |
| `GET` | `/ogc/processes?f=json` | `GetLandingPageAsync` |
| `GET` | `/ogc/processes/conformance?f=json` | `GetConformanceAsync` |
| `GET` | `/ogc/processes/processes` | `ListProcessesAsync` |
| `GET` | `/ogc/processes/processes/{processId}` | `GetProcessAsync` |
| `POST` | `/ogc/processes/processes/{processId}/execution` | `SubmitJobAsync` |
| `GET` | `/ogc/processes/jobs` | `ListJobsAsync` |
| `GET` | `/ogc/processes/jobs/{jobId}` | `GetJobAsync` |
| `DELETE` | `/ogc/processes/jobs/{jobId}` | `DismissJobAsync` |
| `GET` | `/ogc/processes/jobs/{jobId}/results` | `GetJobResultsAsync` |

Job status uses the OGC field names on the wire (`processID`, `jobID`,
`status`, `progress`) and typed SDK properties (`ProcessId`, `JobId`,
`Status`, `Progress`). Document-mode results are represented by
`HonuaProcessResults.Outputs`, a JSON extension-data dictionary keyed by output
identifier. `HonuaProcessExecuteInputs.FromPlan(...)` emits the canonical
`inputs.plan` shape for `honua-geoprocessing`; the direct-input overload emits
advertised concrete process parameters directly under `inputs` with no `plan`
property. Current server contract fixtures use the canonical process id
`honua-geoprocessing`.

```csharp
var job = await processes.SubmitJobAsync(
    "honua-geoprocessing",
    new HonuaProcessExecuteRequest
    {
        Inputs = HonuaProcessExecuteInputs.FromPlan(
            new HonuaAnalysisPlan
            {
                PlanId = "plan-1",
                WorkflowFamily = "analyze",
                Outputs = ["featureLayer"],
                Steps =
                [
                    new HonuaPlanStep
                    {
                        StepId = "buffer",
                        Kind = "geoprocess",
                        ProcessId = "geometry.buffer",
                        Inputs = new Dictionary<string, string>
                        {
                            ["wkb"] = "AAAA",
                            ["srid"] = "4326",
                            ["distance"] = "25"
                        }
                    }
                ]
            })
    },
    cancellationToken);
```

For concrete processes that advertise direct inputs, submit the input values
without the `plan` wrapper:

```csharp
using System.Text.Json;

var bufferJob = await processes.SubmitJobAsync(
    "geometry.buffer",
    new Dictionary<string, JsonElement>
    {
        ["wkb"] = JsonSerializer.SerializeToElement("AAAA"),
        ["srid"] = JsonSerializer.SerializeToElement(4326),
        ["distance"] = JsonSerializer.SerializeToElement(25.5)
    },
    cancellationToken);
```

Native hosts that need ProcessService access resolve
`IHonuaProcessGrpcClient` from `Honua.Sdk.Grpc`. Current server-backed calls are
validate, dry-run, submit, get, result, and cancel; they map onto the same shared
process model package where practical. `ExecutePlanAsync` and
`ExecutePlanStreamAsync` remain proto wrapper methods and may return
`Unimplemented` until server support lands. gRPC failures throw
`HonuaGrpcException` and preserve the gRPC status code. `HonuaProcessJobStatus`,
`HonuaProcessJobProgress`, and `HonuaProcessExecutionResult` use the OGC API
Processes job status values (`accepted`, `running`, `successful`, `failed`,
`dismissed`) for both the REST and gRPC clients.

## Analysis Report And Artifact Contracts

`Honua.Sdk.Studio` provides the browser-safe Console Studio read surface for
analysis reports. The report DTOs live in `Honua.Sdk.Abstractions`
(`Honua.Sdk.Abstractions.Studio`) so browser and native hosts share one set.

| REST method | Path | SDK method |
| --- | --- | --- |
| `GET` | `/api/v1/analysis/reports/{jobId}` | `IHonuaStudioReportsClient.GetReportAsync` |
| `GET` | `/api/v1/analysis/reports/{jobId}/render?format=md\|html` | `IHonuaStudioReportsClient.RenderReportAsync` |
| `GET` | `/v1/spec/artifact/{hash}` | `IHonuaSpecClient.GetArtifactAsync` |

`GetReportAsync` returns `HonuaAnalysisReport` from the **unwrapped** server JSON
(analysis reports are not wrapped in the Admin `ApiResponse<T>` envelope).
`HonuaAnalysisReport.Sections` is a polymorphic hierarchy keyed by the `kind`
discriminator (`heading`, `paragraph`, `key-metric`, `table`, `chart`,
`map-embed`, `narrative`, `provenance-footer`) resolved through a
source-generated JSON context. Those eight kinds are exhaustive within report
contract version `honua.report.v1`; consumers gate on
`HonuaAnalysisReport.ReportContractVersion`. New fields on a known kind are
tolerated; an unmodeled `kind` surfaces as `HonuaStudioContractException`
(loud drift signal). `RenderReportAsync` sends a format-specific `Accept`
header and returns `HonuaRenderedReport` carrying the Markdown or HTML body and
its media type. If the response omits `Content-Type`, the SDK reports the media
type implied by the requested format.

`GetArtifactAsync` retrieves a cached artifact by content hash and returns
`HonuaSpecArtifact` (bytes, content type, and the `X-Spec-Content-Hash` echo).
If the server omits `Content-Type`, the SDK uses `application/octet-stream`; if
the hash header is absent, it uses the requested hash. The SDK buffers
successful artifact responses into `HonuaSpecArtifact.Content`; use this for
bounded cache entries, not large publish/download flows. This is the closest
analog the server exposes today to generated-artifact retrieval; there is no
`/publish`, `/share`, or `/embed` surface yet.

`HonuaAnalysisResultPackage` and its `HonuaArtifactRef` / `HonuaWorkspaceRef` /
`HonuaGeoprocessingError` members are **deserialization-only** projections of
the server `AnalysisResultPackage`. There is no HTTP retrieval client for them
(the server exposes result packages only by id reference and over MCP). Their
enum members mirror the server's numeric wire encoding. Do not confuse this with
`HonuaProcessResults` from `/ogc/processes/jobs/{jobId}/results`, which is a
flattened OGC outputs dictionary, not the raw result package.

Capability and edition state is read through `Honua.Sdk.Admin`
(`GetCapabilitiesAsync`, `GetLicenseEntitlementsAsync`); native transport,
gRPC, and mTLS capability state is read from `Honua.Sdk.Abstractions.Environments`
(`HonuaTransportCapabilities`, `HonuaTrustProfile`, `HonuaEnvironmentTrustState`).
Together these satisfy the Console capability-manifest need without a single
combined server document. Map/App package bodies, publication/share/embed, and
discrete query/dashboard/form/workflow/ETL package clients are gated on server
contracts that do not yet exist and are tracked as server-paired child tickets.

## Fixtures And Drift Checks

Console contract fixtures live under `contracts/fixtures/console/`:

- `environment-profiles.v1.json`
- `route-guard-rbac.v1.json`
- `metadata-rbac.v1.json`
- `admin-publishing-workflow.v1.json`
- `observability-dashboard.v1.json`
- `jobs.v1.json`
- `alerts-rules.v1.json`
- `ogc-processes-openapi-paths.v1.json`
- `analysis-report.v1.json`
- `analysis-result-package.v1.json`

The focused unit tests deserialize these fixtures through source-generated JSON
contexts and assert route/path coverage:

- `tests/Honua.Sdk.Abstractions.Tests/ConsoleContractFixtureTests.cs`
- `tests/Honua.Sdk.Abstractions.Tests/ConsoleTransportInspectionTests.cs`
- `tests/Honua.Sdk.Admin.Tests/ConsoleAdminContractTests.cs`
- `tests/Honua.Sdk.Processes.Tests/ConsoleJobFixtureTests.cs`
- `tests/Honua.Sdk.Grpc.Tests/HonuaProcessGrpcClientTests.cs`
- `tests/Honua.Sdk.Studio.Tests/StudioReportContractFixtureTests.cs`
- `tests/Honua.Sdk.Studio.Tests/HonuaStudioReportsClientTests.cs`

The OGC Processes path fixture is pinned from
`honua-server/src/Honua.Server/ogc-processes-openapi.json`. Admin OpenAPI and
server-side mTLS policy artifacts still need server-owned refresh work before
they can become authoritative SDK CI inputs.
