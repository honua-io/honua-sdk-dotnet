// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Admin raster import (raster write/output) operations over
/// <c>/api/v1/admin/import/raster</c>. This is the write half of the raster geoprocessing
/// round-trip; the read half lives on the read-only <c>IHonuaRasterDataClient</c>.
/// </summary>
public interface IHonuaAdminRasterImportClient
{
    /// <summary>
    /// Imports (uploads) a raster file into PostGIS via multipart upload
    /// (<c>POST /api/v1/admin/import/raster</c>).
    /// </summary>
    /// <param name="request">The raster import request (content stream, target layer, sidecars, options).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed raster import result.</returns>
    Task<RasterImportResult> ImportRasterAsync(RasterImportRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raster file formats supported by the server
    /// (<c>GET /api/v1/admin/import/raster/formats</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The supported raster formats and descriptions.</returns>
    Task<RasterFormatsResponse> GetSupportedRasterFormatsAsync(CancellationToken cancellationToken = default);
}
