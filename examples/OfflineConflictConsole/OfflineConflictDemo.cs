// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline;
using Honua.Sdk.Offline.Abstractions;

namespace OfflineConflictConsole;

/// <summary>
/// Demonstrates the offline conflict workflow end to end using the SDK
/// <see cref="OfflineSyncEngine"/>: produce a conflict envelope, detect it, and walk
/// the ServerWins, ClientWins, and ManualReview merge/resolve paths.
/// </summary>
public static class OfflineConflictDemo
{
    private const string PackageId = "field-area-1";
    private const string SourceId = "parks";

    /// <summary>
    /// Runs the conflict workflow demonstration and writes a deterministic transcript.
    /// </summary>
    /// <param name="output">Transcript writer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of the three resolution paths.</returns>
    public static async Task<OfflineConflictDemoSummary> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        await output.WriteLineAsync("Honua offline conflict workflow (deterministic, in-memory stores)");
        await output.WriteLineAsync(
            $"Package: {PackageId} source={SourceId} operation=update objectId=42");
        await output.WriteLineAsync();

        // 1. The server always rejects the optimistic update with a 409, but accepts a
        //    forced write. This is what produces the conflict envelope on the first push.
        static FeatureEditResponse Responder(FeatureEditRequest request) =>
            request.ForceWrite
                ? new FeatureEditResponse
                {
                    ProviderName = "scripted-offline",
                    UpdateResults = [new FeatureEditResult { Succeeded = true, ObjectId = 42 }],
                }
                : new FeatureEditResponse
                {
                    ProviderName = "scripted-offline",
                    UpdateResults =
                    [
                        new FeatureEditResult
                        {
                            Succeeded = false,
                            ObjectId = 42,
                            Error = new FeatureEditError
                            {
                                Code = 409,
                                Message = "edit conflict: server version is newer",
                            },
                        },
                    ],
                };

        await output.WriteLineAsync("== Path 1: ManualReview (detect + record conflict envelope) ==");
        var manual = await RunPathAsync(output, OfflineConflictStrategy.ManualReview, Responder, cancellationToken)
            .ConfigureAwait(false);

        await output.WriteLineAsync();
        await output.WriteLineAsync("== Path 2: ServerWins (keep server version, drop local edit) ==");
        var serverWins = await RunPathAsync(output, OfflineConflictStrategy.ServerWins, Responder, cancellationToken)
            .ConfigureAwait(false);

        await output.WriteLineAsync();
        await output.WriteLineAsync("== Path 3: ClientWins (force-write local edit over server) ==");
        var clientWins = await RunPathAsync(output, OfflineConflictStrategy.ClientWins, Responder, cancellationToken)
            .ConfigureAwait(false);

        return new OfflineConflictDemoSummary(manual, serverWins, clientWins);
    }

    private static async Task<OfflineConflictPathResult> RunPathAsync(
        TextWriter output,
        OfflineConflictStrategy strategy,
        Func<FeatureEditRequest, FeatureEditResponse> responder,
        CancellationToken cancellationToken)
    {
        var journal = new InMemoryChangeJournal([CreatePendingUpdate()]);
        var conflictStore = new InMemoryConflictStore();
        var editClient = new ScriptedEditClient(responder);
        var store = new InMemoryOfflineStore();

        var engine = new OfflineSyncEngine(
            new ScriptedQueryClient(),
            editClient,
            store,
            journal,
            store,
            new OfflineSyncEngineOptions { ConflictStrategy = strategy },
            store,
            conflictStore);

        var push = await engine.PushAsync(PackageId, cancellationToken).ConfigureAwait(false);

        await output.WriteLineAsync(
            $"  push: loaded={push.Loaded} succeeded={push.Succeeded} conflicts={push.Conflicts} " +
            $"retryable={push.RetryableFailures} fatal={push.FatalFailures}");
        await output.WriteLineAsync($"  edit-requests sent to provider: {editClient.Requests.Count} " +
            $"(forceWrite flags: {string.Join(",", editClient.Requests.Select(r => r.ForceWrite))})");

        // Detection: surface any conflict envelope the engine handed to the conflict store.
        var open = await conflictStore.ListConflictsAsync(PackageId, cancellationToken).ConfigureAwait(false);
        var detected = open.Count > 0;

        if (detected)
        {
            var conflict = open[0];
            await output.WriteLineAsync(
                $"  conflict detected: op={conflict.OperationId} reason=\"{conflict.Reason}\" " +
                $"errorCode={conflict.Error?.Code?.ToString() ?? "none"}");
            await output.WriteLineAsync(
                $"    local edit: kind={conflict.LocalOperation.OperationKind} " +
                $"name={ReadLocalAttribute(conflict.LocalOperation, "name")}");

            // Resolve path: a host reviewer accepts the local edit and clears the envelope.
            await conflictStore.ResolveConflictAsync(conflict.OperationId, cancellationToken).ConfigureAwait(false);
            var remaining = await conflictStore.ListConflictsAsync(PackageId, cancellationToken).ConfigureAwait(false);
            await output.WriteLineAsync($"  resolved by reviewer: open conflicts now {remaining.Count}");
        }
        else
        {
            await output.WriteLineAsync("  no conflict envelope (resolved automatically by strategy)");
        }

        return new OfflineConflictPathResult(
            Strategy: strategy,
            Succeeded: push.Succeeded,
            Conflicts: push.Conflicts,
            EditRequestCount: editClient.Requests.Count,
            ConflictDetected: detected,
            JournalSucceeded: journal.Succeeded.Count,
            JournalConflicts: journal.Conflicts.Count);
    }

    private static OfflineChangeJournalEntry CreatePendingUpdate()
        => new()
        {
            OperationId = "op-1",
            PackageId = PackageId,
            SourceId = SourceId,
            Source = new FeatureSource { CollectionId = SourceId },
            OperationKind = OfflineEditOperationKind.Update,
            BaseSyncToken = "token-1",
            Feature = new FeatureEditFeature
            {
                ObjectId = 42,
                Attributes = new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Ala Moana (field edit)"),
                    ["status"] = JsonSerializer.SerializeToElement("inspected"),
                },
            },
        };

    private static string ReadLocalAttribute(OfflineChangeJournalEntry entry, string name)
    {
        if (entry.Feature is null ||
            !entry.Feature.Attributes.TryGetValue(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return "(none)";
        }

        return value.GetString() ?? "(none)";
    }
}

/// <summary>Outcome of a single conflict-resolution path.</summary>
/// <param name="Strategy">Conflict strategy exercised.</param>
/// <param name="Succeeded">Operations applied successfully.</param>
/// <param name="Conflicts">Operations placed into conflict review.</param>
/// <param name="EditRequestCount">Number of edit requests sent to the provider.</param>
/// <param name="ConflictDetected">Whether a conflict envelope was surfaced for review.</param>
/// <param name="JournalSucceeded">Operations marked succeeded in the change journal.</param>
/// <param name="JournalConflicts">Conflict envelopes recorded by the change journal.</param>
public sealed record OfflineConflictPathResult(
    OfflineConflictStrategy Strategy,
    int Succeeded,
    int Conflicts,
    int EditRequestCount,
    bool ConflictDetected,
    int JournalSucceeded,
    int JournalConflicts);

/// <summary>Combined summary of all three resolution paths.</summary>
/// <param name="ManualReview">ManualReview path result.</param>
/// <param name="ServerWins">ServerWins path result.</param>
/// <param name="ClientWins">ClientWins path result.</param>
public sealed record OfflineConflictDemoSummary(
    OfflineConflictPathResult ManualReview,
    OfflineConflictPathResult ServerWins,
    OfflineConflictPathResult ClientWins);
