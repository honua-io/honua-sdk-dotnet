using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Scenes.Extensions;
using Honua.Sdk.Spec;
using Honua.Sdk.Spec.Extensions;
using Honua.Sdk.Wfs;
using Honua.Sdk.Wfs.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ProtocolIntegrationCollection : ICollectionFixture<ProtocolIntegrationFixture>
{
    public const string Name = "ProtocolIntegration";
}

public sealed class ProtocolIntegrationFixture : IAsyncLifetime, IDisposable
{
    private IContainer? _serverContainer;

    public ProtocolIntegrationOptions Options { get; } = ProtocolIntegrationOptions.Load();

    public Uri BaseUri { get; private set; } = new("http://localhost");

    public ServiceProvider Services { get; private set; } = new ServiceCollection().BuildServiceProvider();

    public IHonuaAdminClient AdminClient => Services.GetRequiredService<IHonuaAdminClient>();

    public IHonuaCatalogClient CatalogClient => Services.GetRequiredService<IHonuaCatalogClient>();

    public IHonuaGeocodingClient GeocodingClient => Services.GetRequiredService<IHonuaGeocodingClient>();

    public IHonuaSpecClient SpecClient => Services.GetRequiredService<IHonuaSpecClient>();

    public IHonuaGrpcClient GrpcClient => Services.GetRequiredService<IHonuaGrpcClient>();

    public IHonuaWfsClient WfsClient => Services.GetRequiredService<IHonuaWfsClient>();

    public IHonuaFeatureServerClient FeatureServerClient => Services.GetRequiredService<IHonuaFeatureServerClient>();

    public IHonuaFeatureServerEditClient FeatureServerEditClient => Services.GetRequiredService<IHonuaFeatureServerEditClient>();

    public IHonuaOgcFeaturesClient OgcFeaturesClient => Services.GetRequiredService<IHonuaOgcFeaturesClient>();

    public IHonuaSceneClient SceneClient => Services.GetRequiredService<IHonuaSceneClient>();

    public IHonuaRoutingClient RoutingClient => Services.GetRequiredService<IHonuaRoutingClient>();

    public async Task InitializeAsync()
    {
        if (!Options.Enabled)
        {
            return;
        }

        BaseUri = Options.ExternalBaseUri ?? await StartServerContainerAsync().ConfigureAwait(false);
        Services = BuildServiceProvider(BaseUri);
    }

    public async Task DisposeAsync()
    {
        if (_serverContainer is not null)
        {
            await _serverContainer.DisposeAsync().ConfigureAwait(false);
        }
    }

    public void Dispose() => Services.Dispose();

    public CancellationTokenSource CreateTimeoutScope(TimeSpan? timeout = null)
        => new(timeout ?? TimeSpan.FromSeconds(45));

    private async Task<Uri> StartServerContainerAsync()
    {
        if (string.IsNullOrWhiteSpace(Options.ServerImage))
        {
            throw new InvalidOperationException("HONUA_PROTOCOL_SERVER_IMAGE is required when HONUA_PROTOCOL_EXTERNAL_BASE_URL is not set.");
        }

        var image = Options.ServerImage;
        var builder = new ContainerBuilder(image)
            .WithPortBinding(Options.ServerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPort(Options.ServerPort)
                .ForPath(Options.ServerHealthPath)
                .ForStatusCodeMatching(statusCode => statusCode >= System.Net.HttpStatusCode.OK &&
                    statusCode < System.Net.HttpStatusCode.MultipleChoices)));

        foreach (var pair in Options.BuildContainerEnvironment())
        {
            builder = builder.WithEnvironment(pair.Key, pair.Value);
        }

        _serverContainer = builder.Build();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await _serverContainer.StartAsync(timeout.Token).ConfigureAwait(false);

        return new UriBuilder(
            Options.ServerScheme,
            _serverContainer.Hostname,
            _serverContainer.GetMappedPublicPort(Options.ServerPort)).Uri;
    }

    private ServiceProvider BuildServiceProvider(Uri baseUri)
    {
        var services = new ServiceCollection();
        services.AddHonuaAdmin(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaGeocoding(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaSpec(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaGrpc(options =>
        {
            options.Address = baseUri.ToString();
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaWfs(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
            options.RoutingServiceId = Options.RouteServiceId ?? "Routing";
            options.RoutingRouteLayerName = Options.RouteLayerName ?? "Route";
            options.RoutingServiceAreaLayerName = Options.ServiceAreaLayerName ?? "ServiceArea";
            options.RoutingClosestFacilityLayerName = Options.ClosestFacilityLayerName ?? "ClosestFacility";
        });
        services.AddHonuaRouting(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
            options.RoutingServiceId = Options.RouteServiceId ?? "Routing";
            options.RoutingRouteLayerName = Options.RouteLayerName ?? "Route";
            options.RoutingServiceAreaLayerName = Options.ServiceAreaLayerName ?? "ServiceArea";
            options.RoutingClosestFacilityLayerName = Options.ClosestFacilityLayerName ?? "ClosestFacility";
        });
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaScenes(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });

        return services.BuildServiceProvider();
    }
}
