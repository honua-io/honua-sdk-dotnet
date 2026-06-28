// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Tests.Fixtures;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.OgcFeatures.Tests.OgcFeatures;

public sealed class OgcFeaturesVectorPayloadTests
{
    [Fact]
    public async Task GetItemsVectorAsync_DefaultsToGeoJsonAndParsesNtsGeometry()
    {
        const string geoJson = """
            {
              "type": "FeatureCollection",
              "numberMatched": 1,
              "numberReturned": 1,
              "features": [
                {
                  "type": "Feature",
                  "id": "parks.1",
                  "properties": { "name": "Kewalo" },
                  "geometry": { "type": "Point", "coordinates": [-157.861, 21.293] }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            Assert.Equal("/ogc/features/collections/parks/items", req.RequestUri!.AbsolutePath);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(geoJson));
        });

        var result = await client.GetItemsVectorAsync("parks");

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        Assert.Equal(1, result.NumberMatched);
        var feature = Assert.Single(result.Features);
        Assert.Equal("Kewalo", feature.Attributes["name"].GetString());
        Assert.IsType<Point>(feature.Geometry);
    }

    [Fact]
    public async Task GetItemsVectorAsync_Gml_RequestsGmlAndParsesNtsGeometry()
    {
        const string gml = """
            <gml:FeatureCollection
                xmlns:gml="http://www.opengis.net/gml/3.2"
                xmlns:honua="https://honua.io/schemas/test"
                numberMatched="1"
                numberReturned="1">
              <gml:featureMember>
                <honua:park gml:id="parks.2">
                  <honua:name>Magic Island</honua:name>
                  <honua:shape>
                    <gml:Point srsName="EPSG:4326">
                      <gml:pos>-157.847 21.286</gml:pos>
                    </gml:Point>
                  </honua:shape>
                </honua:park>
              </gml:featureMember>
            </gml:FeatureCollection>
            """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            Assert.Contains("f=gml", req.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(gml, System.Text.Encoding.UTF8, "application/gml+xml")
            });
        });

        var result = await client.GetItemsVectorAsync(
            "parks",
            new OgcItemsParams { Limit = 1 },
            VectorPayloadFormat.Gml);

        Assert.Equal(VectorPayloadFormat.Gml, result.Format);
        var feature = Assert.Single(result.Features);
        Assert.Equal("parks.2", feature.Id);
        Assert.Equal("Magic Island", feature.Attributes["name"].GetString());
        Assert.Equal(4326, Assert.IsType<Point>(feature.Geometry).SRID);
    }

    [Fact]
    public async Task GetItemsVectorAsync_NonHonuaClientFailure_ThrowsHonuaException()
    {
        // A non-Honua IHonuaOgcFeaturesClient takes the extension's fallback branch. A failed
        // response must surface as a HonuaOgcFeaturesException (mapped from RFC 7807 Problem
        // Details) so callers can catch(HonuaException) uniformly — not a raw HttpRequestException.
        const string problem = """
            {
              "type": "https://honua.io/errors/not-found",
              "title": "Collection not found",
              "detail": "No collection named 'parks'.",
              "status": 404
            }
            """;
        using var client = new FakeOgcFeaturesClient(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(problem, System.Text.Encoding.UTF8, "application/problem+json")
        });

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetItemsVectorAsync("parks"));

        // Catchable as the shared base type, with Problem Details preserved.
        Assert.IsAssignableFrom<Honua.Sdk.Abstractions.HonuaException>(ex);
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("No collection named 'parks'.", ex.ProblemDetail);
    }

    /// <summary>
    /// Minimal non-<see cref="HonuaOgcFeaturesClient"/> implementation that exercises the
    /// extension method's fallback branch. Only <see cref="GetItemsRawAsync"/> is meaningful.
    /// </summary>
    private sealed class FakeOgcFeaturesClient(HttpResponseMessage rawResponse) : IHonuaOgcFeaturesClient, IDisposable
    {
        public void Dispose() => rawResponse.Dispose();

        public Task<HttpResponseMessage> GetItemsRawAsync(
            string collectionId, OgcItemsParams? query = null, CancellationToken cancellationToken = default)
            => Task.FromResult(rawResponse);

        public Task<OgcLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OgcConformance> GetConformanceAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<OgcCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OgcCollection> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OgcQueryables> GetQueryablesAsync(string collectionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OgcFeatureCollection> GetItemsAsync(string collectionId, OgcItemsParams? query = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OgcFeature> GetItemAsync(string collectionId, string featureId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<OgcFeatureCollection> GetItemsPagesAsync(string collectionId, OgcItemsParams? query = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
