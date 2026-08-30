using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.Grpc.Models;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Wfs.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class FeatureProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task GrpcQuery_IsBoundedAndCanOmitGeometry()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var response = await _fixture.GrpcClient.QueryFeaturesAsync(
            new Honua.Sdk.Grpc.Models.QueryFeaturesRequest
            {
                ServiceId = _fixture.Options.ServiceName,
                LayerId = _fixture.Options.LayerId,
                Where = "1=1",
                ReturnGeometry = false,
                ResultRecordCount = 3
            },
            timeout.Token).ConfigureAwait(false);

        Assert.InRange(response.Features.Count, 1, 3);
        Assert.All(response.Features, feature => Assert.Null(feature.Geometry));
    }

    [ProtocolIntegrationFact]
    public async Task WfsCapabilities_DescribeFeatureType_GetFeature_AndCount_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var capabilities = await _fixture.WfsClient.GetCapabilitiesAsync(timeout.Token).ConfigureAwait(false);
        Assert.Contains(
            capabilities.FeatureTypes,
            featureType => string.Equals(featureType.Name, _fixture.Options.WfsTypeName, StringComparison.OrdinalIgnoreCase));

        var schema = await _fixture.WfsClient.DescribeFeatureTypeAsync(
            _fixture.Options.WfsTypeName,
            timeout.Token).ConfigureAwait(false);
        Assert.False(string.IsNullOrWhiteSpace(schema.ElementName));

        var features = await _fixture.WfsClient.GetFeaturesAsync(
            new GetFeaturesRequest
            {
                TypeNames = _fixture.Options.WfsTypeName,
                Count = 2
            },
            timeout.Token).ConfigureAwait(false);
        Assert.InRange(features.Features.Count, 1, 2);

        var count = await _fixture.WfsClient.GetFeatureCountAsync(
            _fixture.Options.WfsTypeName,
            cancellationToken: timeout.Token).ConfigureAwait(false);
        Assert.True(count is null or >= 1);
    }

    [ProtocolIntegrationFact]
    public async Task FeatureServer_Metadata_QueryStatisticsAndVectorPayload_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var serviceInfo = await _fixture.FeatureServerClient.GetServiceInfoAsync(
            _fixture.Options.ServiceName,
            timeout.Token).ConfigureAwait(false);
        Assert.Contains(serviceInfo.Layers ?? [], layer => layer.Id == _fixture.Options.LayerId);

        var layer = await _fixture.FeatureServerClient.GetLayerInfoAsync(
            _fixture.Options.ServiceName,
            _fixture.Options.LayerId,
            timeout.Token).ConfigureAwait(false);
        Assert.Equal(_fixture.Options.LayerId, layer.Id);

        var query = await _fixture.FeatureServerClient.QueryAsync(
            _fixture.Options.ServiceName,
            _fixture.Options.LayerId,
            new FeatureServerQueryParams
            {
                Where = "1=1",
                ReturnGeometry = false,
                ResultRecordCount = 3
            },
            timeout.Token).ConfigureAwait(false);
        Assert.InRange(query.Features?.Count ?? 0, 1, 3);
        Assert.All(
            query.Features ?? [],
            feature => Assert.True(
                !feature.Geometry.HasValue || feature.Geometry.Value.ValueKind == JsonValueKind.Null,
                "FeatureServer query returned geometry for returnGeometry=false."));

        var count = await _fixture.FeatureServerClient.QueryCountAsync(
            _fixture.Options.ServiceName,
            _fixture.Options.LayerId,
            new FeatureServerQueryParams { Where = "1=1" },
            timeout.Token).ConfigureAwait(false);
        Assert.True(count >= 1);

        var ids = await _fixture.FeatureServerClient.QueryIdsAsync(
            _fixture.Options.ServiceName,
            _fixture.Options.LayerId,
            new FeatureServerQueryParams { Where = "1=1" },
            timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(ids);
    }

    [ProtocolIntegrationFact]
    public async Task OgcCollections_Items_Queryables_AndSingleItem_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var collections = await _fixture.OgcFeaturesClient.ListCollectionsAsync(timeout.Token).ConfigureAwait(false);
        Assert.Contains(
            collections,
            collection => string.Equals(collection.Id, _fixture.Options.OgcCollectionId, StringComparison.OrdinalIgnoreCase));

        var collection = await _fixture.OgcFeaturesClient.GetCollectionAsync(
            _fixture.Options.OgcCollectionId,
            timeout.Token).ConfigureAwait(false);
        Assert.Equal(_fixture.Options.OgcCollectionId, collection.Id);

        var queryables = await _fixture.OgcFeaturesClient.GetQueryablesAsync(
            _fixture.Options.OgcCollectionId,
            timeout.Token).ConfigureAwait(false);
        Assert.NotNull(queryables);

        var items = await _fixture.OgcFeaturesClient.GetItemsAsync(
            _fixture.Options.OgcCollectionId,
            new OgcItemsParams { Limit = 2 },
            timeout.Token).ConfigureAwait(false);
        var features = items.Features ?? [];
        Assert.InRange(features.Count, 1, 2);

        var firstItemId = GetFeatureId(features[0]);
        var item = await _fixture.OgcFeaturesClient.GetItemAsync(
            _fixture.Options.OgcCollectionId,
            firstItemId,
            timeout.Token).ConfigureAwait(false);
        Assert.Equal("Feature", item.Type);
    }

    [ProtocolIntegrationFact]
    public async Task SourceFacade_QueriesConfiguredFeatureProtocols()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var sources = new[]
        {
            CreateSource(
                "grpc",
                FeatureProtocolIds.Grpc,
                new SourceLocator { ServiceId = _fixture.Options.ServiceName, LayerId = _fixture.Options.LayerId }),
            CreateSource(
                "geoservices",
                FeatureProtocolIds.GeoServicesFeatureService,
                new SourceLocator { ServiceId = _fixture.Options.ServiceName, LayerId = _fixture.Options.LayerId }),
            CreateSource(
                "wfs",
                FeatureProtocolIds.Wfs,
                new SourceLocator { TypeName = _fixture.Options.WfsTypeName }),
            CreateSource(
                "ogc",
                FeatureProtocolIds.OgcFeatures,
                new SourceLocator { CollectionId = _fixture.Options.OgcCollectionId })
        };

        foreach (HonuaSource source in sources)
        {
            var result = await source.QueryAsync(
                new SourceQuery
                {
                    Where = BuildWhere(source.Descriptor.CanonicalProtocol),
                    FilterLanguage = BuildFilterLanguage(source.Descriptor.CanonicalProtocol),
                    ReturnGeometry = false,
                    Limit = 1
                },
                timeout.Token).ConfigureAwait(false);

            Assert.InRange(result.Features.Count, 1, 1);
        }
    }

    private HonuaSource CreateSource(string id, string protocol, SourceLocator locator)
    {
        var queryClient = _fixture.Services
            .GetServices<IHonuaFeatureQueryClient>()
            .Single(client => FeatureProtocolIds.Matches(protocol, client.ProviderName));
        var editClient = _fixture.Services
            .GetServices<IHonuaFeatureEditClient>()
            .SingleOrDefault(client => FeatureProtocolIds.Matches(protocol, client.ProviderName));

        return new HonuaSource(
            new SourceDescriptor
            {
                Id = id,
                Protocol = protocol,
                Locator = locator
            },
            queryClient,
            editClient,
            queryClient);
    }

    private static string GetFeatureId(OgcFeature feature)
    {
        var id = feature.Id ?? throw new Xunit.Sdk.XunitException("OGC items response did not include a feature ID.");
        return id.ValueKind switch
        {
            JsonValueKind.String => id.GetString() ?? throw new Xunit.Sdk.XunitException("OGC feature ID was empty."),
            JsonValueKind.Number => id.GetRawText(),
            _ => throw new Xunit.Sdk.XunitException($"Unsupported OGC feature ID kind: {id.ValueKind}.")
        };
    }

    private static string? BuildWhere(string protocol)
        => protocol is FeatureProtocolIds.Grpc or FeatureProtocolIds.GeoServicesFeatureService
            ? "1=1"
            : null;

    private static FeatureFilterLanguage BuildFilterLanguage(string protocol)
        => protocol is FeatureProtocolIds.Grpc or FeatureProtocolIds.GeoServicesFeatureService
            ? FeatureFilterLanguage.SqlWhere
            : FeatureFilterLanguage.ProviderDefault;
}
