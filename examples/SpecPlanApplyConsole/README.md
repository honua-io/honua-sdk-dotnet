# Spec Plan/Apply Console

Runnable scaffold for the .NET Spec workflow. The sample builds a typed
`SpecDocumentRequest`, calls `PlanAsync`, then consumes apply events from
`ApplyAsync`.

By default it uses a deterministic in-process HTTP handler that returns a plan
and SSE apply stream. This keeps the demo runnable before every Honua Server
environment exposes the Spec apply endpoints.

## Run With Simulated Spec API

```bash
dotnet run --project examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj
```

## Run Against Honua Server

```bash
export HONUA_SPEC_MODE=server
export HONUA_SPEC_SERVER_URL=https://localhost:5001
export HONUA_SPEC_API_KEY=
export HONUA_SPEC_BEARER_TOKEN=

dotnet run --project examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj
```

Credentials are optional for local unauthenticated targets. If `HONUA_SPEC_API_KEY`
or `HONUA_SPEC_BEARER_TOKEN` is set, use HTTPS except for loopback HTTP during
local development.

## Expected Output Shape

```text
Mode: simulated
Spec: dotnet-demo-suite

Plan: plan-dotnet-demo-suite
  source-permits [Dataset] deps=(none) hash=sha256:source-permits
  active-permits [Compute] deps=source-permits hash=sha256:active-permits
  operator-summary [Report] deps=active-permits hash=sha256:operator-summary

Apply: apply-dotnet-demo-suite
  #1 ApplyStarted
  #2 Cached source-permits
  #3 Running active-permits
  #4 Succeeded active-permits
  #5 ApplyCompleted

Summary: total=3 ran=1 cached=1 failed=0
```

## Validation

- `dotnet build examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj`
- `dotnet run --project examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj`

Live server validation should be added when the target Honua deployment exposes
`/v1/spec/plan` and `/v1/spec/apply` with compatible contracts.
