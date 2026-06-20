// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Console.Share;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// Client for the Console Share export-definition, export-run, and Share-traffic
/// admin surface. Wraps the server APIs under <c>/api/v1/admin/share</c>.
/// </summary>
public interface IHonuaConsoleShareExportClient
{
    /// <summary>
    /// Lists scheduled Share export definitions.
    /// Maps to <c>GET /api/v1/admin/share/exports</c>.
    /// </summary>
    /// <param name="query">Optional filters and paging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A cursor-paged list of export definitions.</returns>
    Task<HonuaShareExportDefinitionPage> ListExportDefinitionsAsync(HonuaShareExportDefinitionQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a scheduled Share export definition.
    /// Maps to <c>POST /api/v1/admin/share/exports</c>.
    /// </summary>
    /// <param name="request">Export definition to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created export definition.</returns>
    Task<HonuaShareExportDefinition> CreateExportDefinitionAsync(HonuaShareExportDefinitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a scheduled Share export definition.
    /// Maps to <c>GET /api/v1/admin/share/exports/{exportId}</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The export definition.</returns>
    Task<HonuaShareExportDefinition> GetExportDefinitionAsync(string exportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a scheduled Share export definition.
    /// Maps to <c>PUT /api/v1/admin/share/exports/{exportId}</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="request">Replacement export definition.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated export definition.</returns>
    Task<HonuaShareExportDefinition> UpdateExportDefinitionAsync(string exportId, HonuaShareExportDefinitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a scheduled Share export definition and its run history.
    /// Maps to <c>DELETE /api/v1/admin/share/exports/{exportId}</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteExportDefinitionAsync(string exportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a Share export run.
    /// Maps to <c>POST /api/v1/admin/share/exports/{exportId}/trigger</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The accepted export run.</returns>
    Task<HonuaShareExportRun> TriggerExportAsync(string exportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a scheduled Share export definition.
    /// Maps to <c>POST /api/v1/admin/share/exports/{exportId}/pause</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated export definition.</returns>
    Task<HonuaShareExportDefinition> PauseExportAsync(string exportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a scheduled Share export definition.
    /// Maps to <c>POST /api/v1/admin/share/exports/{exportId}/resume</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated export definition.</returns>
    Task<HonuaShareExportDefinition> ResumeExportAsync(string exportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists Share export run history for a definition.
    /// Maps to <c>GET /api/v1/admin/share/exports/{exportId}/runs</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="cursor">Optional page cursor from a prior response.</param>
    /// <param name="limit">Optional page size; the server clamps to its range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A cursor-paged list of export runs.</returns>
    Task<HonuaShareExportRunPage> ListExportRunsAsync(string exportId, string? cursor = null, int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single Share export run.
    /// Maps to <c>GET /api/v1/admin/share/exports/{exportId}/runs/{runId}</c>.
    /// </summary>
    /// <param name="exportId">Export definition identifier.</param>
    /// <param name="runId">Run identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The export run.</returns>
    Task<HonuaShareExportRun> GetExportRunAsync(string exportId, string runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the aggregate Share traffic summary.
    /// Maps to <c>GET /api/v1/admin/share/traffic</c>.
    /// </summary>
    /// <param name="query">Optional period filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate traffic summary.</returns>
    Task<HonuaShareTrafficSummary> GetTrafficSummaryAsync(HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the aggregate Share traffic time series.
    /// Maps to <c>GET /api/v1/admin/share/traffic/series</c>.
    /// </summary>
    /// <param name="query">Optional period and bucket filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregate traffic time series.</returns>
    Task<HonuaShareTrafficSeries> GetTrafficSeriesAsync(HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the per-item Share traffic summary for a service layer.
    /// Maps to <c>GET /api/v1/admin/services/{serviceName}/layers/{layerId}/share/traffic</c>.
    /// </summary>
    /// <param name="serviceName">Service name of the item.</param>
    /// <param name="layerId">Layer identifier of the item.</param>
    /// <param name="resourceId">Optional share/resource identifier filter.</param>
    /// <param name="query">Optional period filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The per-item traffic summary.</returns>
    Task<HonuaShareTrafficSummary> GetItemTrafficSummaryAsync(string serviceName, int layerId, string? resourceId = null, HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the per-item Share traffic time series for a service layer.
    /// Maps to <c>GET /api/v1/admin/services/{serviceName}/layers/{layerId}/share/traffic/series</c>.
    /// </summary>
    /// <param name="serviceName">Service name of the item.</param>
    /// <param name="layerId">Layer identifier of the item.</param>
    /// <param name="resourceId">Optional share/resource identifier filter.</param>
    /// <param name="query">Optional period and bucket filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The per-item traffic time series.</returns>
    Task<HonuaShareTrafficSeries> GetItemTrafficSeriesAsync(string serviceName, int layerId, string? resourceId = null, HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default);
}
