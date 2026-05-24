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
- `IHonuaProcessGrpcClient` for ProcessService job lifecycle calls and progress
  streams.
- `HonuaEnvironmentAuthMode.NativeMutualTls` and trust-state DTOs for profile
  selection and diagnostics.

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
