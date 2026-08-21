using System.Text.Json;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.Grpc.Models;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Wfs.Models;

namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// Live conformance tier: drives the canonical <c>geospatial-grpc</c> fixture
/// workflows through the real SDK protocol clients (gRPC, GeoServices
/// FeatureServer, WFS, OGC API Features) against a pinned, Testcontainers-managed
/// <c>honua-server:nightly</c>, and asserts the responses round-trip into the
/// SDK models without contract drift.
/// <para>
/// This is the layer that would have caught honua-server#1238: a server-side
/// projection/shape change that the schema tier cannot see because it never
/// reaches the live server. Assertions tied to already-tracked nightly gaps are
/// marked known-expected-failing (see <see cref="LiveConformanceFactAttribute"/>
/// and <see cref="ConformanceKnownGaps"/>); any other drift fails the gate.
/// </para>
/// </summary>
[Collection(LiveConformanceCollection.Name)]
[Trait("Category", "Conformance")]
public sealed class LiveConformanceTests(LiveConformanceFixture fixture)
{
    private readonly LiveConformanceFixture _fixture = fixture;

    [LiveConformanceFact]
    public async Task GrpcQueryFeatures_RoundTripsCanonicalResponseShape()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        // Canonical FeatureService query workflow (feature_query_request.json),
        // bounded so it does not depend on the seed's row count.
        var response = await _fixture.GrpcClient.QueryFeaturesAsync(
            new QueryFeaturesRequest
            {
                ServiceId = _fixture.Options.ServiceName,
                LayerId = _fixture.Options.LayerId,
                Where = "1=1",
                ReturnGeometry = true,
                OutFields = ["*"],
                ResultRecordCount = 1,
            },
            timeout.Token).ConfigureAwait(false);

        // Contract surface the canonical QueryFeaturesResponse fixture asserts:
        // an object-id field name, a typed field list, and at least one feature
        // whose attributes survive the gRPC wire + SDK conversion round-trip.
        Assert.False(string.IsNullOrWhiteSpace(response.ObjectIdFieldName),
            "gRPC query response dropped objectIdFieldName.");
        Assert.NotEmpty(response.Fields);
        var feature = Assert.Single(response.Features);
        Assert.NotEmpty(feature.Attributes);
        Assert.NotNull(feature.Geometry);
    }

    [LiveConformanceFact]
    public async Task GrpcQuery_GeometryOmission_IsHonored()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var response = await _fixture.GrpcClient.QueryFeaturesAsync(
            new QueryFeaturesRequest
            {
                ServiceId = _fixture.Options.ServiceName,
                LayerId = _fixture.Options.LayerId,
                Where = "1=1",
                ReturnGeometry = false,
                ResultRecordCount = 1,
            },
            timeout.Token).ConfigureAwait(false);

        Assert.All(response.Features, f => Assert.Null(f.Geometry));
    }

    [LiveConformanceFact]
    public async Task Wfs_GetFeatures_RoundTripsCanonicalFeatureCollection()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var capabilities = await _fixture.WfsClient.GetCapabilitiesAsync(timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(capabilities.FeatureTypes);

        // Round-trip the configured type when present, else the first advertised
        // one — the conformance contract is the FeatureCollection shape, not the
        // seed's naming, which is owned by honua-server.
        var typeName =
            capabilities.FeatureTypes.FirstOrDefault(ft =>
                string.Equals(ft.Name, _fixture.Options.WfsTypeName, StringComparison.OrdinalIgnoreCase))?.Name
            ?? capabilities.FeatureTypes[0].Name;

        var features = await _fixture.WfsClient.GetFeaturesAsync(
            new GetFeaturesRequest { TypeNames = typeName, Count = 1 },
            timeout.Token).ConfigureAwait(false);

        // The conformance contract is the FeatureCollection shape surviving the WFS GeoJSON
        // wire + SDK conversion round-trip. An empty result must fail the drift gate (a
        // no-features regression would otherwise pass silently), so require exactly one
        // feature and assert its content survived — mirroring the gRPC and OGC cases above.
        var feature = Assert.Single(features.Features);
        Assert.NotNull(feature.Geometry);
        Assert.False(string.IsNullOrWhiteSpace(feature.Geometry!.Type),
            "WFS feature geometry dropped its GeoJSON type.");
        Assert.NotEmpty(feature.Properties);
    }

    [LiveConformanceFact]
    public async Task FeatureServerQuery_RoundTripsCanonicalAttributeProjection()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var query = await _fixture.FeatureServerClient.QueryAsync(
            _fixture.Options.ServiceName,
            _fixture.Options.LayerId,
            new FeatureServerQueryParams { Where = "1=1", OutFields = "*", ReturnGeometry = true, ResultRecordCount = 1 },
            timeout.Token).ConfigureAwait(false);

        var feature = Assert.Single(query.Features ?? []);
        // The canonical contract: JSONB attributes project into their native
        // JSON types, not a nested envelope or stringified JSON.
        Assert.NotNull(feature.Attributes);
        Assert.True(feature.Attributes!.TryGetValue("tags", out var tags));
        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.All(tags.EnumerateArray(), value => Assert.Equal(JsonValueKind.String, value.ValueKind));

        Assert.True(feature.Attributes.TryGetValue("numbers", out var numbers));
        Assert.Equal(JsonValueKind.Array, numbers.ValueKind);
        Assert.All(numbers.EnumerateArray(), value => Assert.Equal(JsonValueKind.Number, value.ValueKind));
    }

    [LiveConformanceFact]
    public async Task OgcItems_RoundTripCanonicalPropertyProjection()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var collections = await _fixture.OgcFeaturesClient.ListCollectionsAsync(timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(collections);
        var collectionId =
            collections.FirstOrDefault(c =>
                string.Equals(c.Id, _fixture.Options.OgcCollectionId, StringComparison.OrdinalIgnoreCase))?.Id
            ?? collections[0].Id;

        var items = await _fixture.OgcFeaturesClient.GetItemsAsync(
            collectionId,
            new OgcItemsParams { Limit = 1 },
            timeout.Token).ConfigureAwait(false);

        var feature = Assert.Single(items.Features ?? []);
        Assert.Equal("Feature", feature.Type);
        Assert.NotNull(feature.Properties);
        Assert.True(feature.Properties!.TryGetValue("tags", out var tags));
        Assert.Equal(JsonValueKind.Array, tags.ValueKind);
        Assert.All(tags.EnumerateArray(), value => Assert.Equal(JsonValueKind.String, value.ValueKind));

        Assert.True(feature.Properties.TryGetValue("numbers", out var numbers));
        Assert.Equal(JsonValueKind.Array, numbers.ValueKind);
        Assert.All(numbers.EnumerateArray(), value => Assert.Equal(JsonValueKind.Number, value.ValueKind));
    }
}
