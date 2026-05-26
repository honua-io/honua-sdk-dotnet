# Spec Workspace Contracts

`Honua.Sdk.Spec` is the client-stable contract package for Honua spec
workspace automation. It exists so admin UI, CLI, tests, and other clients use
one typed surface for spec validation, planning, apply streaming, and
cancellation instead of copying internal server or admin workspace models. It
also exposes the current generated-artifact retrieval analog:
content-hash-addressed cached artifacts from `/v1/spec/artifact/{hash}`.

## Package Surface

The package owns:

- `IHonuaSpecClient` for `/v1/spec/validate`, `/v1/spec/plan`,
  `/v1/spec/apply`, `/v1/spec/cancel`, and `/v1/spec/artifact/{hash}`.
- DTOs under `Honua.Sdk.Spec.Models` for spec document requests, validation
  diagnostics, plan responses, warnings, apply events, apply summaries,
  cancellation payloads, cached artifacts (`HonuaSpecArtifact`), and
  problem-details errors.
- HTTP auth, timeout, and retry configuration through `HonuaSpecClientOptions`,
  matching the other HTTP SDK packages. Credentials are only sent over HTTPS,
  except loopback HTTP for local development.
- Source-generated JSON serialization for the REST/SSE shape.
- Golden JSON fixtures under
  `tests/Honua.Sdk.Spec.Tests/Fixtures/Json/` for sibling repos to mirror in
  contract tests.

The package does not own UI state, parser internals, canonicalization,
planning, execution, cache behavior, RBAC, operator capability registries,
local demo stubs, panes, previews, or display behavior.

`GetArtifactAsync` returns raw artifact bytes, the response content type, and
the `X-Spec-Content-Hash` echo. If the server omits `Content-Type`, the SDK
uses `application/octet-stream`; if the hash header is absent, it uses the
requested hash. Successful responses are binary payloads buffered into
`HonuaSpecArtifact.Content`; failed responses read problem-details bodies before
throwing `HonuaSpecException`. This method is for bounded,
content-hash-addressed cache artifacts. There is no publish/share/embed client
in this SDK slice because the server does not expose that HTTP surface yet.

## Repo Ownership

| Repo | Owns |
|------|------|
| `honua-sdk-dotnet` | Stable client DTOs, REST/SSE client behavior, auth/retry/timeout behavior, JSON fixtures, NuGet package publishing. |
| `honua-server` | Spec parser, canonicalizer, planner, apply orchestrator, server validation, cache policy, authorization, endpoint behavior, server domain models. |
| `honua-server-admin` | Blazor/MudBlazor workspace UI, editor state, local stubs, preview/pane models, operator workflow state, and UX copy. |
| `geospatial-grpc` | Canonical `.proto` definitions if the spec workspace surface is exposed over gRPC. |

## Consumption Rules

- Admin, CLI, and automation clients should consume `Honua.Sdk.Spec` through a
  versioned NuGet `PackageReference`.
- Do not add long-lived sibling `ProjectReference` links or copy SDK source
  into admin/mobile/server repos.
- If the wire shape changes, update `Honua.Sdk.Spec` DTOs and JSON fixtures in
  the same PR as the client behavior.
- If the gRPC shape changes, start in `geospatial-grpc`; server and SDK consume
  the published `Geospatial.Grpc` package afterward.
- Server-side implementation details remain inputs for SDK review, not source
  files to package wholesale.

## Dependency Sequence

Spec workspace cutover should follow the cross-repo dependency order:

1. Publish `Geospatial.Grpc` from `geospatial-grpc`.
2. Update `honua-server` to consume `Geospatial.Grpc` where protobuf contracts
   are involved.
3. Publish `Honua.Sdk.Spec` with the stable REST/SSE contracts.
4. Update admin UI and CLI callers to consume `Honua.Sdk.Spec` from GitHub
   Packages.
