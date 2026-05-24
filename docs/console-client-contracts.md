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
| Native job lifecycle streaming | Not a browser runtime surface | `Honua.Sdk.Grpc` (`IHonuaProcessGrpcClient`) |
| Spec validate/plan/apply workflows | `Honua.Sdk.Spec` REST/SSE candidate | Same REST/SSE client |

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
unwrapped by `HonuaAdminClient`. Feature-event replay intentionally returns the
raw replay page shape because the server endpoint is not envelope-wrapped.
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
identifier. Current server contract fixtures use the canonical process id
`honua-geoprocessing`.

```csharp
var job = await processes.SubmitJobAsync(
    "honua-geoprocessing",
    new HonuaProcessExecuteRequest
    {
        Inputs = new HonuaProcessExecuteInputs
        {
            Plan = new HonuaAnalysisPlan
            {
                PlanId = "plan-1",
                WorkflowFamily = "analyze",
                Outputs = ["summary"],
                Steps =
                [
                    new HonuaPlanStep
                    {
                        StepId = "buffer",
                        Kind = "geometry.buffer",
                        Inputs = new Dictionary<string, string>
                        {
                            ["distance"] = "25"
                        }
                    }
                ]
            }
        }
    },
    cancellationToken);
```

Native hosts that need ProcessService access resolve
`IHonuaProcessGrpcClient` from `Honua.Sdk.Grpc`. Current server-backed calls are
validate, dry-run, submit, get, result, and cancel; they map onto the same shared
process model package where practical. `ExecutePlanAsync` and
`ExecutePlanStreamAsync` remain proto wrapper methods and may return
`Unimplemented` until server support lands. gRPC failures throw
`HonuaGrpcException` and preserve the gRPC status code.

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

The focused unit tests deserialize these fixtures through source-generated JSON
contexts and assert route/path coverage:

- `tests/Honua.Sdk.Abstractions.Tests/ConsoleContractFixtureTests.cs`
- `tests/Honua.Sdk.Admin.Tests/ConsoleAdminContractTests.cs`
- `tests/Honua.Sdk.Processes.Tests/ConsoleJobFixtureTests.cs`
- `tests/Honua.Sdk.Grpc.Tests/HonuaProcessGrpcClientTests.cs`

The OGC Processes path fixture is pinned from
`honua-server/src/Honua.Server/ogc-processes-openapi.json`. Admin OpenAPI and
server-side mTLS policy artifacts still need server-owned refresh work before
they can become authoritative SDK CI inputs.
