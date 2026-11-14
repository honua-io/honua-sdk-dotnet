using System.Text.Json;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class FeatureReadProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task SingleFeature_MatchesRequestedObjectId()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var layer = await _fixture.FeatureServerClient.GetLayerInfoAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, timeout.Token).ConfigureAwait(false);
        var ids = await _fixture.FeatureServerClient.QueryIdsAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId,
            new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(ids);
        var feature = await _fixture.FeatureServerClient.GetFeatureAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, ids[0],
            new FeatureServerQueryParams { ReturnGeometry = false }, timeout.Token).ConfigureAwait(false);

        Assert.NotNull(feature);
        Assert.Equal(ids[0], ObjectId(feature, layer.ObjectIdField));
        Assert.True(!feature.Geometry.HasValue || feature.Geometry.Value.ValueKind == JsonValueKind.Null);
    }

    [ProtocolIntegrationFact]
    public async Task Extent_ContainsReturnedFeatureGeometry()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var query = new FeatureServerQueryParams { Where = "1=1", OutSR = 4326 };
        var extent = await _fixture.FeatureServerClient.QueryExtentAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, query, timeout.Token).ConfigureAwait(false);
        Assert.NotNull(extent.SpatialReference);
        Assert.Equal(4326, extent.SpatialReference.Wkid);
        Assert.True(double.IsFinite(extent.Xmin) && double.IsFinite(extent.Ymin)
            && double.IsFinite(extent.Xmax) && double.IsFinite(extent.Ymax));
        var response = await _fixture.FeatureServerClient.QueryAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId,
            query with { ResultRecordCount = 2, ReturnGeometry = true }, timeout.Token).ConfigureAwait(false);

        Assert.NotEmpty(response.Features ?? []);
        foreach (var feature in response.Features ?? [])
        {
            var geometry = Assert.IsType<JsonElement>(feature.Geometry);
            Assert.InRange(geometry.GetProperty("x").GetDouble(), extent.Xmin, extent.Xmax);
            Assert.InRange(geometry.GetProperty("y").GetDouble(), extent.Ymin, extent.Ymax);
        }
    }

    [ProtocolIntegrationFact]
    public async Task RawQuery_ReturnsBoundedJsonWithoutGeometry()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        using var response = await _fixture.FeatureServerClient.QueryRawAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId,
            new FeatureServerQueryParams
            {
                Where = "1=1", Format = FeatureServerFormat.Json,
                ResultRecordCount = 2, ReturnGeometry = false
            }, timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
        var features = body.RootElement.GetProperty("features");
        Assert.InRange(features.GetArrayLength(), 1, 2);
        foreach (var feature in features.EnumerateArray())
        {
            Assert.NotEmpty(feature.GetProperty("attributes").EnumerateObject());
            Assert.True(!feature.TryGetProperty("geometry", out var geometry) || geometry.ValueKind == JsonValueKind.Null);
        }
    }

    [ProtocolIntegrationFact]
    public async Task OffsetPages_AdvanceWithoutRepeatingFeatures()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var layer = await _fixture.FeatureServerClient.GetLayerInfoAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId, timeout.Token).ConfigureAwait(false);
        var ids = new HashSet<long>();
        await foreach (var page in _fixture.FeatureServerClient.QueryPagesAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId,
            new FeatureServerQueryParams
            {
                Where = "1=1", ResultRecordCount = 1, ReturnGeometry = false,
                OrderByFields = layer.ObjectIdField
            }, timeout.Token).ConfigureAwait(false))
        {
            var feature = Assert.Single(page.Features ?? []);
            Assert.True(ids.Add(ObjectId(feature, page.ObjectIdFieldName)), "Offset paging repeated a feature.");
            if (ids.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(2, ids.Count);
    }

    [ProtocolIntegrationFact]
    public async Task ObjectIdBatches_ReturnExactlyRequestedFeatures()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var ids = await _fixture.FeatureServerClient.QueryIdsAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId,
            new FeatureServerQueryParams { Where = "1=1" }, timeout.Token).ConfigureAwait(false);
        var requested = ids.Take(2).ToArray();
        Assert.Equal(2, requested.Length);
        var returned = new HashSet<long>();
        await foreach (var page in _fixture.FeatureServerClient.QueryAllFeaturesByObjectIdBatchesAsync(
            _fixture.Options.ServiceName, _fixture.Options.LayerId,
            new FeatureServerQueryParams { ObjectIds = requested, ReturnGeometry = false },
            batchSize: 1, cancellationToken: timeout.Token).ConfigureAwait(false))
        {
            var feature = Assert.Single(page.Features ?? []);
            var id = ObjectId(feature, page.ObjectIdFieldName);
            Assert.Contains(id, requested);
            Assert.True(returned.Add(id), "Object-ID batching repeated a feature.");
        }

        Assert.Equal(requested.Order(), returned.Order());
    }

    private static long ObjectId(FeatureServerFeature feature, string? field)
    {
        Assert.NotNull(field);
        Assert.False(string.IsNullOrWhiteSpace(field));
        Assert.NotNull(feature.Attributes);
        return feature.Attributes[field].GetInt64();
    }
}
