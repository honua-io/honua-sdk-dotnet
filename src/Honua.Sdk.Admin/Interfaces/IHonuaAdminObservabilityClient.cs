// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Operational observability: recent errors, telemetry subsystem status,
/// and database migration status.
/// </summary>
public interface IHonuaAdminObservabilityClient
{
    /// <summary>
    /// Gets recent errors from the server.
    /// </summary>
    /// <param name="limit">Optional maximum number of errors to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of recent errors.</returns>
    Task<IReadOnlyList<RecentError>> GetRecentErrorsAsync(int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent errors from the server, including response metadata.
    /// </summary>
    /// <param name="limit">Optional maximum number of errors to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recent-errors response payload.</returns>
    Task<RecentErrorsResponse> GetRecentErrorsResponseAsync(int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current telemetry subsystem status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The telemetry status.</returns>
    Task<TelemetryStatus> GetTelemetryStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current database migration status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration status.</returns>
    Task<MigrationStatus> GetMigrationStatusAsync(CancellationToken cancellationToken = default);
}
