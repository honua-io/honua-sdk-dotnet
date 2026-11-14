using System.Text.Json;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Wfs.Formats;
using Honua.Sdk.OgcFeatures.Wfs.Models;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class FeatureDiscoveryProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task OgcDiscovery_AdvertisesCollectionsAndCoreConformance()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var landing = await _fixture.OgcFeaturesClient.GetLandingPageAsync(timeout.Token).ConfigureAwait(false);
        Assert.Contains(landing.Links ?? [], link => link.Rel == "data" && !string.IsNullOrWhiteSpace(link.Href));
        Assert.Contains(landing.Links ?? [], link => link.Rel == "conformance" && !string.IsNullOrWhiteSpace(link.Href));

        var conformance = await _fixture.OgcFeaturesClient.GetConformanceAsync(timeout.Token).ConfigureAwait(false);
        Assert.Contains("http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core", conformance.ConformsTo ?? []);
    }

    [ProtocolIntegrationFact]
    public async Task OgcRawItems_ReturnsBoundedGeoJson()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        using var response = await _fixture.OgcFeaturesClient.GetItemsRawAsync(
            _fixture.Options.OgcCollectionId,
            new OgcItemsParams { Limit = 2, Format = OgcFeaturesFormat.GeoJson },
            timeout.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
        Assert.Equal("FeatureCollection", body.RootElement.GetProperty("type").GetString());
        var features = body.RootElement.GetProperty("features");
        Assert.InRange(features.GetArrayLength(), 1, 2);
        Assert.All(features.EnumerateArray(), feature => Assert.Equal("Feature", feature.GetProperty("type").GetString()));
    }

    [ProtocolIntegrationFact]
    public async Task OgcPages_FollowsNextLinkWithoutRepeatingFeatures()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var pageCount = 0;
        await foreach (var page in _fixture.OgcFeaturesClient.GetItemsPagesAsync(
            _fixture.Options.OgcCollectionId,
            new OgcItemsParams { Limit = 1 },
            timeout.Token).ConfigureAwait(false))
        {
            var feature = Assert.Single(page.Features ?? []);
            var id = Assert.IsType<JsonElement>(feature.Id).ToString();
            Assert.False(string.IsNullOrWhiteSpace(id));
            Assert.True(ids.Add(id), "Pagination repeated a feature instead of advancing.");
            if (++pageCount == 2)
            {
                break;
            }
        }

        Assert.Equal(2, pageCount);
    }

    [ProtocolIntegrationFact]
    public async Task WfsFormatHandler_ReturnsBoundedGeoJson()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var handler = new GeoJsonFeatureCollectionHandler();
        var result = await _fixture.WfsClient.GetFeaturesAsync(
            new GetFeaturesRequest { TypeNames = _fixture.Options.WfsTypeName, Count = 2 },
            handler,
            timeout.Token).ConfigureAwait(false);
        Assert.InRange(result.Features.Count, 1, 2);
        Assert.All(result.Features, feature => Assert.False(string.IsNullOrWhiteSpace(feature.Id)));
    }

    [ProtocolIntegrationFact]
    public async Task WfsEnumeration_AdvancesPastTheFirstPage()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var feature in _fixture.WfsClient.GetFeaturesAsyncEnumerable(
            new GetFeaturesRequest { TypeNames = _fixture.Options.WfsTypeName, Count = 1 },
            timeout.Token).ConfigureAwait(false))
        {
            Assert.False(string.IsNullOrWhiteSpace(feature.Id));
            Assert.True(ids.Add(feature.Id), "Pagination repeated a feature instead of advancing.");
            if (ids.Count == 2)
            {
                break;
            }
        }

        Assert.Equal(2, ids.Count);
    }
}
