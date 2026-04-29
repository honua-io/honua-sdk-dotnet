// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Offline;

/// <summary>
/// Planned query for one offline source.
/// </summary>
public sealed record OfflineDownloadRequest
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>Source descriptor used to create the provider query.</summary>
    public required SourceDescriptor Source { get; init; }

    /// <summary>Provider-neutral query request.</summary>
    public required FeatureQueryRequest Query { get; init; }

    /// <summary>Provider sync token selected for this pull.</summary>
    public string? LastSyncToken { get; init; }

    /// <summary>Maximum number of features to store for this source.</summary>
    public int? MaxFeatureCount { get; init; }
}

/// <summary>
/// Download plan for an offline package.
/// </summary>
public sealed record OfflineDownloadPlan
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Planned source downloads.</summary>
    public IReadOnlyList<OfflineDownloadRequest> Requests { get; init; } = [];
}

/// <summary>
/// Builds bounded provider-neutral feature queries from offline package manifests.
/// </summary>
public static class OfflineDownloadPlanner
{
    /// <summary>
    /// Creates a download plan for every source in an offline package manifest.
    /// </summary>
    /// <param name="manifest">Offline package manifest.</param>
    /// <param name="checkpoints">Source checkpoints keyed by source identifier.</param>
    /// <returns>A download plan.</returns>
    public static OfflineDownloadPlan CreatePlan(
        OfflinePackageManifest manifest,
        IReadOnlyDictionary<string, OfflineSyncCheckpoint?>? checkpoints = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.PackageId);

        var requests = new List<OfflineDownloadRequest>(manifest.Sources.Count);
        foreach (var source in manifest.Sources)
        {
            OfflineSyncCheckpoint? checkpoint = null;
            if (checkpoints is not null)
            {
                checkpoints.TryGetValue(source.SourceId, out checkpoint);
            }

            requests.Add(CreateRequest(manifest, source, checkpoint));
        }

        return new OfflineDownloadPlan
        {
            PackageId = manifest.PackageId,
            Requests = requests,
        };
    }

    /// <summary>
    /// Creates a download request for one source in an offline package manifest.
    /// </summary>
    /// <param name="manifest">Offline package manifest.</param>
    /// <param name="source">Offline source descriptor.</param>
    /// <param name="checkpoint">Persisted source checkpoint, when available.</param>
    /// <returns>A planned source download request.</returns>
    public static OfflineDownloadRequest CreateRequest(
        OfflinePackageManifest manifest,
        OfflineSourceDescriptor source,
        OfflineSyncCheckpoint? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.PackageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceId);

        var lastSyncToken = checkpoint?.SyncToken ?? source.LastSyncToken;

        return new OfflineDownloadRequest
        {
            PackageId = manifest.PackageId,
            SourceId = source.SourceId,
            Source = source.Source,
            Query = source.ToQueryRequest(lastSyncToken),
            LastSyncToken = lastSyncToken,
            MaxFeatureCount = source.MaxFeatureCount,
        };
    }
}
