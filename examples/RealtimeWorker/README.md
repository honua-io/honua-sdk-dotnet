# Realtime Worker

Runnable worker-style demo for the SDK realtime contracts. Honua Server
realtime transport is still a server dependency, so the default mode uses
deterministic `FeatureStreamEvent` envelopes and the SDK's
`FeatureStreamEventProcessor` plus `FeatureStreamEventBuffer`.

## Run

```bash
dotnet run --project examples/RealtimeWorker/RealtimeWorker.csproj
```

## Expected Output Shape

```text
Mode: simulated
Transport: deterministic FeatureStreamEvent envelopes
Subscription: incidents-active source=incidents/0

Write decisions:
  #1 Subscribed accepted last=1 resume=resume-1
  #2 Insert incident-42 accepted last=2 resume=resume-2
  #2 Update incident-42 duplicate-sequence last=2 resume=resume-2
  #1 Update incident-42 stale-sequence last=2 resume=resume-1
  #3 Update incident-42 accepted last=3 resume=resume-3
  #4 Delete incident-42 accepted last=4 resume=resume-4

Projection:
  active=0 closed=1 lastSequence=4 resume=resume-4
```

## Live Transport Gate

Setting `HONUA_REALTIME_MODE=server` intentionally fails fast until Honua
Server exposes the negotiated realtime endpoint and auth requirements. The
sample is structured around SDK-owned contracts so the live adapter can replace
the simulated event source later without changing projection logic.

## Required Capabilities

- Simulated mode: none beyond `Honua.Sdk.Abstractions`.
- Live mode: future Honua Server realtime feature stream transport.

## Validation

- `dotnet build examples/RealtimeWorker/RealtimeWorker.csproj`
- `dotnet run --project examples/RealtimeWorker/RealtimeWorker.csproj`
- `dotnet test tests/DemoSuite.Tests/DemoSuite.Tests.csproj`
