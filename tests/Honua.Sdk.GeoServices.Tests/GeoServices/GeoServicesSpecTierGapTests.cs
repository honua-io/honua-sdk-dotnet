// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.GeoServices.Tests.GeoServices;

/// <summary>
/// Spec-tier coverage gaps for GeoServices ImageServer, GeometryServer, and the
/// raster <c>exportImage</c> operation.
///
/// Unlike the implemented FeatureServer (<c>HonuaFeatureServerClient</c>) and
/// NAServer routing (<c>HonuaRoutingClient</c>) clients, there is currently no
/// .NET client for these surfaces — only the protocol identifiers and capability
/// sets exist (locked down by <c>GeoServicesSpecTierContractTests</c>).
///
/// Each test below is intentionally <c>Skip</c>-marked. The skip reason names the
/// missing type/operation, and the body documents the GeoServices REST contract
/// the future client is expected to honor (endpoint path, request parameters, and
/// response shape) using the same MockHttpHandler/form-capture pattern used by the
/// routing tests. When a client is implemented, remove the <c>Skip</c> and wire the
/// assertions to the real client — the documented contract should not need to
/// change.
///
/// Keeping these as discoverable skipped tests (rather than omitting them) makes
/// the coverage gap visible in test runs and prevents "invented" behavior from
/// silently shipping.
/// </summary>
public sealed class GeoServicesSpecTierGapTests
{
    private const string ImageServerGap =
        "GeoServices ImageServer client is not implemented yet (spec-tier only: " +
        "FeatureProtocolIds.GeoServicesImageService exists, no HonuaImageServerClient). " +
        "Tracked under honua-sdk-dotnet#166.";

    private const string GeometryServerGap =
        "GeoServices GeometryServer client is not implemented yet (spec-tier only: " +
        "FeatureProtocolIds.GeoServicesGeometryService exists, no HonuaGeometryServerClient). " +
        "Tracked under honua-sdk-dotnet#166.";

    private const string ExportImageGap =
        "GeoServices MapServer/ImageServer exportImage operation is not implemented yet " +
        "(spec-tier only: advertised via the 'render'/'image' capabilities, no export client). " +
        "Tracked under honua-sdk-dotnet#166.";

    // -- ImageServer ----------------------------------------------------------

    [Fact(Skip = ImageServerGap)]
    public void ImageServer_GetServiceMetadata_ContractIsDocumented()
    {
        // Expected contract for the future HonuaImageServerClient:
        //
        //   GET /rest/services/{serviceId}/ImageServer?f=json
        //
        // Parses the ImageServer service description into a metadata model:
        //   - serviceDescription, name, pixelType
        //   - extent (xmin/ymin/xmax/ymax + spatialReference)
        //   - bandCount, minValues/maxValues per band
        //   - supportedQueryFormats, supportedImageFormatTypes (e.g. "PNG,JPG,...")
        //   - allowedMosaicMethods, defaultMosaicMethod
        //   - hasRasterAttributeTable, hasHistograms
        //
        // Errors surface as HonuaFeatureServerException (GeoServices `error`
        // envelope: { error: { code, message, details[] } }).
        Assert.Fail("Unreachable: documentation-only contract for unimplemented ImageServer metadata.");
    }

    [Fact(Skip = ImageServerGap)]
    public void ImageServer_ExportImage_ContractIsDocumented()
    {
        // Expected contract:
        //
        //   GET (or POST form) /rest/services/{serviceId}/ImageServer/exportImage
        //   Parameters:
        //     bbox            "<xmin>,<ymin>,<xmax>,<ymax>"
        //     bboxSR / imageSR spatial reference wkids
        //     size            "<width>,<height>"
        //     format          png | png8 | png24 | jpg | tiff | ...
        //     pixelType       (optional override)
        //     noData, noDataInterpretation
        //     interpolation   RSP_BilinearInterpolation | RSP_NearestNeighbor | ...
        //     mosaicRule, renderingRule (JSON)
        //     f               image | json (json returns href + extent metadata)
        //
        // With f=image the response body is the raw raster stream
        // (Content-Type image/png etc.) — the client should expose it as a
        // ResponseOwningStream so the caller owns disposal, mirroring how
        // FeatureServer streams vector tile/PBF payloads.
        //
        // With f=json the response is { href, width, height, extent, scale }.
        Assert.Fail("Unreachable: documentation-only contract for unimplemented ImageServer exportImage.");
    }

    [Fact(Skip = ImageServerGap)]
    public void ImageServer_IdentifyPixel_ContractIsDocumented()
    {
        // Expected contract:
        //
        //   GET /rest/services/{serviceId}/ImageServer/identify
        //   Parameters: geometry (point), geometryType=esriGeometryPoint,
        //               mosaicRule, renderingRule, pixelSize, f=json
        //   Response:   { objectId, name, value, location, catalogItems, ... }
        Assert.Fail("Unreachable: documentation-only contract for unimplemented ImageServer identify.");
    }

    // -- GeometryServer -------------------------------------------------------

    [Fact(Skip = GeometryServerGap)]
    public void GeometryServer_Project_ContractIsDocumented()
    {
        // Expected contract for the future HonuaGeometryServerClient:
        //
        //   POST /rest/services/Geometry/GeometryServer/project (form-encoded)
        //   Parameters:
        //     geometries     { geometryType, geometries: [...] }
        //     inSR           input spatial reference wkid
        //     outSR          output spatial reference wkid
        //     transformation (optional datum transformation id)
        //     f              json
        //   Response: { geometries: [ projected geometries ] }
        //
        // NOTE: per AGENTS.md, CRS transforms should be performed via ProjNet
        // locally where possible; this client is the remote fallback that
        // delegates to the server GeometryServer for transformations the SDK
        // cannot perform offline.
        Assert.Fail("Unreachable: documentation-only contract for unimplemented GeometryServer project.");
    }

    [Fact(Skip = GeometryServerGap)]
    public void GeometryServer_Buffer_ContractIsDocumented()
    {
        // Expected contract:
        //
        //   POST /rest/services/Geometry/GeometryServer/buffer (form-encoded)
        //   Parameters:
        //     geometries, inSR, outSR, bufferSR
        //     distances      comma-separated list
        //     unit           esriSRUnit_* / esriSRUnit2_*
        //     unionResults   true | false
        //     geodesic       true | false
        //     f              json
        //   Response: { geometries: [ polygon geometries ] }
        //
        // NOTE: per AGENTS.md, planar buffers belong in NetTopologySuite; this
        // client is for server-side / geodesic buffering the local engine does
        // not cover.
        Assert.Fail("Unreachable: documentation-only contract for unimplemented GeometryServer buffer.");
    }

    [Fact(Skip = GeometryServerGap)]
    public void GeometryServer_LengthsAndAreas_ContractIsDocumented()
    {
        // Expected contract:
        //
        //   POST /rest/services/Geometry/GeometryServer/lengths
        //   POST /rest/services/Geometry/GeometryServer/areasAndLengths
        //   Parameters: polylines/polygons, sr, lengthUnit, areaUnit,
        //               calculationType (planar | geodesic | preserveShape), f=json
        //   Response (lengths):         { lengths: [ ... ] }
        //   Response (areasAndLengths): { areas: [ ... ], lengths: [ ... ] }
        Assert.Fail("Unreachable: documentation-only contract for unimplemented GeometryServer measurements.");
    }

    // -- exportImage error contract -------------------------------------------

    [Fact(Skip = ExportImageGap)]
    public void ExportImage_GeoServicesError_SurfacesAsHonuaFeatureServerException()
    {
        // The exportImage and GeometryServer operations must surface GeoServices
        // error envelopes ({ error: { code, message, details[] } }) returned with
        // HTTP 200 as HonuaFeatureServerException, identical to the routing and
        // FeatureServer clients (see HonuaRoutingClient.TryExtractGeoServicesError).
        // The HTTP status on the exception should map from error.code.
        Assert.Fail("Unreachable: documentation-only error contract for unimplemented export/geometry operations.");
    }
}
