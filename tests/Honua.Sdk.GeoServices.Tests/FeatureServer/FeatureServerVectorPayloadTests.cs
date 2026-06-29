// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Honua.Sdk.Geometry.Vector;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

public sealed class FeatureServerVectorPayloadTests
{
    [Fact]
    public async Task QueryVectorAsync_DefaultsToEsriJsonAndParsesNtsGeometry()
    {
        const string json = """
            {
              "objectIdFieldName": "OBJECTID",
              "spatialReference": { "wkid": 4326 },
              "features": [
                {
                  "attributes": { "OBJECTID": 7, "name": "Harbor" },
                  "geometry": { "x": -157.865, "y": 21.306 }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Equal("/rest/services/parks/FeatureServer/0/query", req.RequestUri!.AbsolutePath);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.QueryVectorAsync("parks", 0, new FeatureServerQueryParams());

        Assert.Equal(VectorPayloadFormat.EsriJson, result.Format);
        var feature = Assert.Single(result.Features);
        Assert.Equal("7", feature.Id);
        Assert.Equal("Harbor", feature.Attributes["name"].GetString());
        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public async Task QueryVectorAsync_GeoJson_RequestsGeoJsonAndParsesNtsGeometry()
    {
        const string geoJson = """
            {
              "type": "FeatureCollection",
              "numberReturned": 1,
              "features": [
                {
                  "type": "Feature",
                  "id": "parks.9",
                  "properties": { "name": "Lagoon" },
                  "geometry": { "type": "Point", "coordinates": [-157.84, 21.29] }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("f=geojson", req.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(geoJson));
        });

        var result = await client.QueryVectorAsync(
            "parks",
            0,
            new FeatureServerQueryParams(),
            VectorPayloadFormat.GeoJson);

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        var point = Assert.IsType<Point>(Assert.Single(result.Features).Geometry);
        Assert.Equal(-157.84, point.X, 2);
    }

    [Fact]
    public async Task QueryVectorAsync_GenericClient_200ErrorEnvelope_ThrowsHonuaFeatureServerException()
    {
        // The extension fallback path (non-HonuaFeatureServerClient implementations) goes through
        // QueryRawAsync. GeoServices reports failures in-band as HTTP 200 with a JSON {error} body;
        // the vector path must surface that as a HonuaFeatureServerException rather than feeding the
        // error body to the binary payload reader.
        var client = new FakeRawFeatureServerClient(
            () => TestHelpers.CreateGeoServicesErrorResponse(400, "Invalid where clause.", ["WHERE parse error"]));

        var exception = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryVectorAsync("parks", 0, new FeatureServerQueryParams()));

        Assert.Equal("Invalid where clause.", exception.Message);
        Assert.Equal(400, exception.GeoServicesErrorCode);
    }

    [Fact]
    public async Task QueryVectorAsync_GenericClient_GeoJsonBody_ParsesSuccessfully()
    {
        // A genuine JSON (GeoJSON) payload on the fallback path must still parse — the envelope
        // check is a no-op when there is no {error} object.
        const string geoJson = """
            {
              "type": "FeatureCollection",
              "features": [
                {
                  "type": "Feature",
                  "id": "parks.1",
                  "properties": { "name": "Lagoon" },
                  "geometry": { "type": "Point", "coordinates": [-157.84, 21.29] }
                }
              ]
            }
            """;
        var client = new FakeRawFeatureServerClient(() => TestHelpers.CreateRawJsonResponse(geoJson));

        var result = await client.QueryVectorAsync(
            "parks",
            0,
            new FeatureServerQueryParams(),
            VectorPayloadFormat.GeoJson);

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        Assert.Single(result.Features);
    }

    /// <summary>
    /// Minimal <see cref="IHonuaFeatureServerClient"/> stub that is NOT a
    /// <see cref="HonuaFeatureServerClient"/>, so the vector extension exercises its
    /// generic-client fallback path. Only <see cref="QueryRawAsync"/> is implemented.
    /// </summary>
    private sealed class FakeRawFeatureServerClient : IHonuaFeatureServerClient
    {
        private readonly Func<HttpResponseMessage> _rawResponseFactory;

        public FakeRawFeatureServerClient(Func<HttpResponseMessage> rawResponseFactory)
            => _rawResponseFactory = rawResponseFactory;

        public Task<HttpResponseMessage> QueryRawAsync(
            string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default)
            => Task.FromResult(_rawResponseFactory());

        public Task<FeatureServerServiceInfo> GetServiceInfoAsync(string serviceId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureServerLayerInfo> GetLayerInfoAsync(string serviceId, int layerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureServerQueryResponse> QueryAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureServerFeature?> GetFeatureAsync(string serviceId, int layerId, long objectId, FeatureServerQueryParams? query = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> QueryCountAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<long>> QueryIdsAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureServerExtent> QueryExtentAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<FeatureServerQueryResponse> QueryPagesAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureServerQueryResponse> QueryStatisticsAsync(string serviceId, int layerId, FeatureServerStatisticsParams query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureServerValidateSqlResponse> ValidateSqlAsync(string serviceId, int layerId, string where, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
