using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.Grpc.Models;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Wfs.Models;

namespace Honua.Sdk.IntegrationTests;

[Collection("StagingIntegration")]
[Trait("Category", "Integration")]
[Trait("Scope", "Staging")]
public sealed class StagingReadOnlyIntegrationTests(StagingIntegrationFixture fixture)
{
    private readonly StagingIntegrationFixture _fixture = fixture;

    [StagingConfiguredFact]
    public async Task AdminCompatibility_AndServiceSettings_AreReadable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        await _fixture.RecordCheckAsync(
            "admin-compatibility",
            "IHonuaAdminClient.CheckCompatibilityAsync",
            "/api/v1/admin/capabilities",
            async cancellationToken =>
            {
                var compatibility = await _fixture.AdminClient.CheckCompatibilityAsync(cancellationToken).ConfigureAwait(false);

                Assert.True(
                    compatibility.IsSupported,
                    compatibility.UnsupportedReason ?? "Staging server did not meet the admin compatibility baseline.");

                return
                    $"serverVersion={compatibility.ServerVersion}; " +
                    $"releaseChannel={compatibility.ReleaseChannel}; " +
                    $"minimumVersion={compatibility.MinimumSupportedServerVersion}";
            },
            timeout.Token).ConfigureAwait(false);

        await _fixture.RecordCheckAsync(
            "admin-service-settings",
            "IHonuaAdminClient.GetServiceSettingsAsync",
            $"/api/v1/admin/services/{Uri.EscapeDataString(_fixture.Options.ServiceName)}/settings",
            async cancellationToken =>
            {
                var settings = await _fixture.AdminClient.GetServiceSettingsAsync(
                    _fixture.Options.ServiceName,
                    cancellationToken).ConfigureAwait(false);

                Assert.Equal(_fixture.Options.ServiceName, settings.ServiceName);
                Assert.Contains(settings.EnabledProtocols, protocol => string.Equals(protocol, "Grpc", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(settings.EnabledProtocols, protocol => string.Equals(protocol, "FeatureServer", StringComparison.OrdinalIgnoreCase));

                return
                    $"enabledProtocols={string.Join(",", settings.EnabledProtocols)}; " +
                    $"availableProtocols={string.Join(",", settings.AvailableProtocols)}";
            },
            timeout.Token).ConfigureAwait(false);
    }

    [StagingConfiguredFact]
    public async Task GrpcQuery_IsBounded_AndReadOnly()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        await _fixture.RecordCheckAsync(
            "grpc-query",
            "IHonuaGrpcClient.QueryFeaturesAsync",
            "grpc://FeatureService/QueryFeatures",
            async cancellationToken =>
            {
                var response = await _fixture.GrpcClient.QueryFeaturesAsync(
                    new Honua.Sdk.Grpc.Models.QueryFeaturesRequest
                    {
                        ServiceId = _fixture.Options.ServiceName,
                        LayerId = _fixture.Options.LayerId,
                        Where = "1=1",
                        ReturnGeometry = false,
                        ResultRecordCount = 3
                    },
                    cancellationToken).ConfigureAwait(false);

                Assert.InRange(response.Features.Count, 1, 3);
                Assert.All(response.Features, feature => Assert.Null(feature.Geometry));

                return $"rows={response.Features.Count}; returnGeometry=false; resultRecordCount=3";
            },
            timeout.Token).ConfigureAwait(false);
    }

    [StagingConfiguredFact]
    public async Task WfsCapabilities_AndBoundedGetFeature_AreReadable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        await _fixture.RecordCheckAsync(
            "wfs-capabilities",
            "IHonuaWfsClient.GetCapabilitiesAsync",
            "/wfs?SERVICE=WFS&VERSION=2.0.0&REQUEST=GetCapabilities",
            async cancellationToken =>
            {
                var capabilities = await _fixture.WfsClient.GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);

                Assert.False(string.IsNullOrWhiteSpace(capabilities.Version));
                Assert.Contains(
                    capabilities.FeatureTypes,
                    featureType => string.Equals(featureType.Name, _fixture.Options.WfsTypeName, StringComparison.OrdinalIgnoreCase));

                return $"version={capabilities.Version}; featureTypes={capabilities.FeatureTypes.Count}";
            },
            timeout.Token).ConfigureAwait(false);

        await _fixture.RecordCheckAsync(
            "wfs-get-features",
            "IHonuaWfsClient.GetFeaturesAsync",
            "/wfs?SERVICE=WFS&VERSION=2.0.0&REQUEST=GetFeature",
            async cancellationToken =>
            {
                var features = await _fixture.WfsClient.GetFeaturesAsync(
                    new GetFeaturesRequest
                    {
                        TypeNames = _fixture.Options.WfsTypeName,
                        Count = 2
                    },
                    cancellationToken).ConfigureAwait(false);

                Assert.InRange(features.Features.Count, 1, 2);

                return
                    $"numberReturned={features.NumberReturned}; " +
                    $"numberMatched={features.NumberMatched?.ToString() ?? "unknown"}";
            },
            timeout.Token).ConfigureAwait(false);
    }

    [StagingConfiguredFact]
    public async Task FeatureServerMetadata_AndBoundedQuery_AreReadable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        await _fixture.RecordCheckAsync(
            "features-service-info",
            "IHonuaFeatureServerClient.GetServiceInfoAsync",
            $"/rest/services/{Uri.EscapeDataString(_fixture.Options.ServiceName)}/FeatureServer",
            async cancellationToken =>
            {
                var serviceInfo = await _fixture.FeatureServerClient.GetServiceInfoAsync(
                    _fixture.Options.ServiceName,
                    cancellationToken).ConfigureAwait(false);

                Assert.Contains(
                    serviceInfo.Layers ?? [],
                    layer => layer.Id == _fixture.Options.LayerId);

                return
                    $"layers={serviceInfo.Layers?.Count ?? 0}; " +
                    $"capabilities={serviceInfo.Capabilities}";
            },
            timeout.Token).ConfigureAwait(false);

        await _fixture.RecordCheckAsync(
            "features-query",
            "IHonuaFeatureServerClient.QueryAsync",
            $"/rest/services/{Uri.EscapeDataString(_fixture.Options.ServiceName)}/FeatureServer/{_fixture.Options.LayerId}/query",
            async cancellationToken =>
            {
                var response = await _fixture.FeatureServerClient.QueryAsync(
                    _fixture.Options.ServiceName,
                    _fixture.Options.LayerId,
                    new FeatureServerQueryParams
                    {
                        Where = "1=1",
                        ReturnGeometry = false,
                        ResultRecordCount = 3
                    },
                    cancellationToken).ConfigureAwait(false);

                var features = response.Features ?? [];
                Assert.InRange(features.Count, 1, 3);
                Assert.All(
                    features,
                    feature => Assert.True(
                        !feature.Geometry.HasValue || feature.Geometry.Value.ValueKind == JsonValueKind.Null,
                        "FeatureServer query unexpectedly returned geometry for a returnGeometry=false request."));

                return $"rows={features.Count}; returnGeometry=false; resultRecordCount=3";
            },
            timeout.Token).ConfigureAwait(false);
    }

    [StagingConfiguredFact]
    public async Task SourceFacade_QueriesAllConfiguredFeatureProviders()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        await _fixture.RecordCheckAsync(
            "source-facade-query",
            "IHonuaSource.QueryAsync",
            "source://grpc,geoservices-feature-service,wfs,ogc-features",
            async cancellationToken =>
            {
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

                var summaries = new List<string>();
                foreach (var source in sources)
                {
                    Assert.Contains(FeatureCapabilities.Query, source.Capabilities);

                    var result = await source.QueryAsync(
                        new SourceQuery
                        {
                            Where = BuildWhere(source.Descriptor.CanonicalProtocol),
                            FilterLanguage = BuildFilterLanguage(source.Descriptor.CanonicalProtocol),
                            ReturnGeometry = false,
                            Limit = 1
                        },
                        cancellationToken).ConfigureAwait(false);

                    Assert.InRange(result.Features.Count, 1, 1);
                    summaries.Add($"{source.Descriptor.Id}:{result.ProviderName}:rows={result.Features.Count}");
                }

                return string.Join("; ", summaries);
            },
            timeout.Token).ConfigureAwait(false);
    }

    [StagingConfiguredFact]
    public async Task OgcCollections_Items_AndSingleItem_AreReadable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        await _fixture.RecordCheckAsync(
            "ogc-collections",
            "IHonuaOgcFeaturesClient.ListCollectionsAsync",
            "/ogc/features/collections",
            async cancellationToken =>
            {
                var collections = await _fixture.OgcFeaturesClient.ListCollectionsAsync(cancellationToken).ConfigureAwait(false);

                Assert.Contains(
                    collections,
                    collection => string.Equals(collection.Id, _fixture.Options.OgcCollectionId, StringComparison.OrdinalIgnoreCase));

                return $"collections={collections.Count}";
            },
            timeout.Token).ConfigureAwait(false);

        string? firstItemId = null;

        await _fixture.RecordCheckAsync(
            "ogc-items",
            "IHonuaOgcFeaturesClient.GetItemsAsync",
            $"/ogc/features/collections/{Uri.EscapeDataString(_fixture.Options.OgcCollectionId)}/items",
            async cancellationToken =>
            {
                var items = await _fixture.OgcFeaturesClient.GetItemsAsync(
                    _fixture.Options.OgcCollectionId,
                    new OgcItemsParams
                    {
                        Limit = 2
                    },
                    cancellationToken).ConfigureAwait(false);

                var features = items.Features ?? [];
                Assert.InRange(features.Count, 1, 2);

                firstItemId = GetFeatureId(features[0]);
                Assert.False(string.IsNullOrWhiteSpace(firstItemId));

                return
                    $"numberReturned={items.NumberReturned?.ToString() ?? features.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)}; " +
                    $"firstItemId={firstItemId}";
            },
            timeout.Token).ConfigureAwait(false);

        await _fixture.RecordCheckAsync(
            "ogc-item",
            "IHonuaOgcFeaturesClient.GetItemAsync",
            $"/ogc/features/collections/{Uri.EscapeDataString(_fixture.Options.OgcCollectionId)}/items/{{featureId}}",
            async cancellationToken =>
            {
                Assert.False(string.IsNullOrWhiteSpace(firstItemId));

                var item = await _fixture.OgcFeaturesClient.GetItemAsync(
                    _fixture.Options.OgcCollectionId,
                    firstItemId!,
                    cancellationToken).ConfigureAwait(false);

                Assert.Equal("Feature", item.Type);
                Assert.True(item.Id.HasValue);

                return $"itemId={firstItemId}";
            },
            timeout.Token).ConfigureAwait(false);
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

    private static string? BuildWhere(string protocol)
        => protocol is FeatureProtocolIds.Grpc or FeatureProtocolIds.GeoServicesFeatureService
            ? "1=1"
            : null;

    private static FeatureFilterLanguage BuildFilterLanguage(string protocol)
        => protocol is FeatureProtocolIds.Grpc or FeatureProtocolIds.GeoServicesFeatureService
            ? FeatureFilterLanguage.SqlWhere
            : FeatureFilterLanguage.ProviderDefault;
}
