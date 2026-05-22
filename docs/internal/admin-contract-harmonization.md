# Admin Contract Harmonization

This inventory tracks the `honua-server-admin` surfaces that overlap reusable
SDK contracts. Stable REST DTOs and reusable HTTP clients belong in
`Honua.Sdk.Admin`; page state, stubs, telemetry adapters, and presentation logic
stay in `honua-server-admin`.

## Graduated SDK Surfaces

`Honua.Sdk.Admin` now owns reusable contracts and client methods for:

- service summaries, service settings, layer metadata, and layer styles
- secure connections, table discovery, encryption checks, and key rotation
- metadata resources, manifests, compatibility, and server configuration
- layer publishing and layer enablement
- deploy preflight, deploy plans, deploy operations, telemetry status,
  migration status, and recent errors
- OIDC provider CRUD and identity-provider catalog health checks
- license status, entitlements, and replacement-license upload

The admin UI should consume these through a published `Honua.Sdk.Admin` NuGet
package before deleting local duplicate DTOs or HTTP clients.

## UI-Only Surfaces

These stay in `honua-server-admin`:

- Blazor pages, MudBlazor components, dialogs, form models, and view models
- workspace state classes such as publishing, identity, license, usage
  analytics, data connections, spatial SQL, print service, and annotations
- local stubs and sample data used to run the UI without a server
- telemetry adapters that map UI workflows to `ILogger`
- diagnostic copy, expiry bands, mutation guards, SQL result exporters, and
  other presentation-specific helpers

## Still Needs Classification

These surfaces should not move blindly:

- spatial SQL execution, schema, explain-plan, and named-view DTOs
- print/export DTOs
- annotation workspace DTOs
- open data hub DTOs
- usage analytics reports

Before graduation, confirm that each surface is stable server API rather than a
UI-only workspace contract. If the server API is missing or still unstable,
create or link the `honua-server` dependency issue first.

## Follow-Up Sequence

1. Publish a `Honua.Sdk.Admin` package containing the graduated identity and
   license contracts.
2. Update `honua-server-admin` to consume `Honua.Sdk.Admin` through GitHub
   Packages, not a sibling project reference.
3. Replace duplicated admin UI DTOs and typed HTTP clients with thin adapters
   over the SDK client, keeping UI diagnostics and local stubs in the admin
   repo.
4. Add shared JSON fixture tests in the admin repo that validate the SDK
   contract payloads used by identity and license pages.
