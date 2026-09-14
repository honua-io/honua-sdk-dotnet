// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Migration toolkit helpers for admin clients.
/// </summary>
public static class HonuaAdminMigrationClientExtensions
{
    /// <summary>
    /// Scans a supported source environment and returns the migration source inventory artifact.
    /// </summary>
    /// <param name="client">The admin client.</param>
    /// <param name="request">The migration inventory scan request.</param>
    /// <param name="exportJson">When true, requests the server's JSON attachment form with <c>export=json</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration source inventory artifact returned by the server.</returns>
    public static Task<MigrationSourceInventoryArtifact> ScanMigrationSourceAsync(
        this IHonuaAdminClient client,
        MigrationInventoryScanRequest request,
        bool exportJson = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client is HonuaAdminClient honuaClient)
        {
            return honuaClient.ScanMigrationSourceAsync(request, exportJson, cancellationToken);
        }

        throw new NotSupportedException("This IHonuaAdminClient implementation does not support migration source scanning.");
    }

    /// <summary>Discovers the layers and metadata exposed by an ArcGIS service.</summary>
    public static Task<GeoservicesDiscoverResponse> DiscoverGeoservicesServiceAsync(
        this IHonuaAdminClient client,
        GeoservicesDiscoverRequest request,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).DiscoverGeoservicesServiceAsync(request, cancellationToken);

    /// <summary>Queues a single-layer GeoServices import job.</summary>
    public static Task<GeoservicesImportJobResponse> StartGeoservicesImportAsync(
        this IHonuaAdminClient client,
        GeoservicesStartImportRequest request,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).StartGeoservicesImportAsync(request, cancellationToken);

    /// <summary>Lists active (non-terminal) GeoServices import jobs.</summary>
    public static Task<IReadOnlyList<GeoservicesImportProgress>> ListGeoservicesImportJobsAsync(
        this IHonuaAdminClient client,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).ListGeoservicesImportJobsAsync(cancellationToken);

    /// <summary>Gets the current status of a GeoServices import job.</summary>
    public static Task<GeoservicesImportProgress> GetGeoservicesImportJobStatusAsync(
        this IHonuaAdminClient client,
        string jobId,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).GetGeoservicesImportJobStatusAsync(jobId, cancellationToken);

    /// <summary>Requests cancellation of a running GeoServices import job.</summary>
    public static Task<GeoservicesImportCancelResponse> CancelGeoservicesImportJobAsync(
        this IHonuaAdminClient client,
        string jobId,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).CancelGeoservicesImportJobAsync(jobId, cancellationToken);

    /// <summary>
    /// Polls a GeoServices import job until it reaches a terminal status or the bound elapses. Never
    /// calls <see cref="StartGeoservicesImportAsync"/> again; safe to call after a client restart.
    /// </summary>
    public static Task<GeoservicesImportProgress> WaitForGeoservicesImportJobAsync(
        this IHonuaAdminClient client,
        string jobId,
        TimeSpan? pollInterval = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).WaitForGeoservicesImportJobAsync(jobId, pollInterval, timeout, cancellationToken);

    /// <summary>Starts an ordered, resumable batch (multi-layer) migration run from a footprint selection.</summary>
    public static Task<MigrationBatchResponse> StartMigrationBatchAsync(
        this IHonuaAdminClient client,
        MigrationBatchStartRequest request,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).StartMigrationBatchAsync(request, cancellationToken);

    /// <summary>Gets the rolled-up status and per-child progress of a batch migration run.</summary>
    public static Task<MigrationBatchResponse> GetMigrationBatchAsync(
        this IHonuaAdminClient client,
        string batchId,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).GetMigrationBatchAsync(batchId, cancellationToken);

    /// <summary>Lists migration runs (most recent first, paged).</summary>
    public static Task<MigrationRunListResponse> ListMigrationRunsAsync(
        this IHonuaAdminClient client,
        MigrationRunListQuery? query = null,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).ListMigrationRunsAsync(query, cancellationToken);

    /// <summary>Gets a single migration run by id.</summary>
    public static Task<MigrationRunDto> GetMigrationRunAsync(
        this IHonuaAdminClient client,
        string runId,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).GetMigrationRunAsync(runId, cancellationToken);

    /// <summary>Marks a running migration run as cancelled.</summary>
    public static Task<MigrationRunDto> CancelMigrationRunAsync(
        this IHonuaAdminClient client,
        string runId,
        string? reason = null,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).CancelMigrationRunAsync(runId, reason, cancellationToken);

    /// <summary>Downloads the evidence pack recorded for a migration run, verbatim as JSON text.</summary>
    public static Task<MigrationRunArtifactDownload> GetMigrationRunEvidencePackAsync(
        this IHonuaAdminClient client,
        string runId,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).GetMigrationRunEvidencePackAsync(runId, cancellationToken);

    /// <summary>Downloads and parses the signed reconciliation scorecard recorded for a migration run.</summary>
    public static Task<MigrationReconciliationScorecard> GetMigrationRunReconciliationScorecardAsync(
        this IHonuaAdminClient client,
        string runId,
        CancellationToken cancellationToken = default)
        => AsHonuaAdminClient(client).GetMigrationRunReconciliationScorecardAsync(runId, cancellationToken);

    private static HonuaAdminClient AsHonuaAdminClient(IHonuaAdminClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client as HonuaAdminClient
            ?? throw new NotSupportedException("This IHonuaAdminClient implementation does not support the GeoServices/migration import lifecycle.");
    }
}
