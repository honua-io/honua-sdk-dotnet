// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Studio;

namespace Honua.Sdk.Studio;

/// <summary>
/// Browser-safe read client for Console Studio analysis reports. Wraps the
/// server analysis-reporting surface under <c>/api/v1/analysis/reports</c>.
/// </summary>
public interface IHonuaStudioReportsClient
{
    /// <summary>
    /// Retrieves the structured analysis report envelope for a completed job.
    /// Maps to <c>GET /api/v1/analysis/reports/{jobId}</c>. The response is
    /// returned unwrapped (no <c>ApiResponse&lt;T&gt;</c> envelope).
    /// </summary>
    /// <param name="jobId">Originating geoprocessing job identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The analysis report envelope.</returns>
    Task<HonuaAnalysisReport> GetReportAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the analysis report for a completed job as Markdown or HTML.
    /// Maps to <c>GET /api/v1/analysis/reports/{jobId}/render?format=md|html</c>.
    /// </summary>
    /// <param name="jobId">Originating geoprocessing job identifier.</param>
    /// <param name="format">Render format (Markdown or HTML).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered report body and its media type.</returns>
    Task<HonuaRenderedReport> RenderReportAsync(string jobId, HonuaReportRenderFormat format, CancellationToken cancellationToken = default);
}
