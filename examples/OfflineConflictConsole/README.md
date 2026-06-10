# OfflineConflictConsole

Deterministic console sample for the Honua SDK offline conflict workflow. It uses
`Honua.Sdk.Offline.OfflineSyncEngine` with in-memory stores to show how a local
edit produces an `OfflineConflictEnvelope`, how a host detects it, and how the
three built-in resolution strategies behave.

## What it demonstrates

A single pending `Update` (objectId 42) is pushed against a scripted provider
that rejects optimistic writes with HTTP 409 but accepts a forced write. The
sample runs the same operation under each `OfflineConflictStrategy`:

| Path | Strategy | Outcome |
|------|----------|---------|
| 1 | `ManualReview` | Engine records an `OfflineConflictEnvelope`; the sample lists it from the conflict store, prints the local edit, then resolves it as a reviewer would. |
| 2 | `ServerWins` | Engine keeps the server version and marks the local operation handled; no envelope. |
| 3 | `ClientWins` | Engine retries with `ForceWrite`; the second edit request succeeds, no envelope. |

The sample also shows the detection surface (`IOfflineConflictStore.ListConflictsAsync`)
and the resolve surface (`IOfflineConflictStore.ResolveConflictAsync`).

## Run

```bash
dotnet run --project examples/OfflineConflictConsole/OfflineConflictConsole.csproj
```

No live Honua Server is required. The query/edit clients and all stores are
in-memory and deterministic, so the transcript is stable and CI-friendly.

## SDK / mobile boundary

The stores here (`InMemoryOfflineStore`, `InMemoryChangeJournal`,
`InMemoryConflictStore`) are sample fakes. A real host backs these contracts with
GeoPackage, SQLite, or browser storage and lives in `honua-mobile`. This sample
stays focused on the provider-neutral SDK contracts only.
