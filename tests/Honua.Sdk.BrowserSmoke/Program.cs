using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.BrowserSmoke;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.Offline;
using Honua.Sdk.Offline.Abstractions;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Spec;
using Honua.Sdk.Spec.Extensions;
using Honua.Sdk.Wfs;
using Honua.Sdk.Wfs.Extensions;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

var server = new Uri("https://honua.example.test/");
ConfigureRestClients(builder.Services, server);
RegisterOfflineContracts(builder.Services);

await builder.Build().RunAsync().ConfigureAwait(false);

static void ConfigureRestClients(IServiceCollection services, Uri server)
{
    services.AddHonuaAdmin(options =>
    {
        ConfigureAdminBrowserCandidate(options, server);
    });
    services.AddHonuaGeocoding(options =>
    {
        ConfigureAdminBrowserCandidate(options, server);
    });
    services.AddHonuaSpec(options =>
    {
        ConfigureSpecBrowserCandidate(options, server);
    });
    services.AddHonuaWfs(options =>
    {
        ConfigureWfsBrowserCandidate(options, server);
    });
    services.AddHonuaFeatureServer(options =>
    {
        ConfigureGeoServicesBrowserCandidate(options, server);
    });
    services.AddHonuaOgcFeatures(options =>
    {
        ConfigureOgcFeaturesBrowserCandidate(options, server);
    });
}

static void ConfigureAdminBrowserCandidate(HonuaAdminClientOptions options, Uri server)
{
    options.BaseAddress = server;
    options.BearerTokenProvider = NoBrowserTokenAsync;
    options.EnableRetry = false;
}

static void ConfigureSpecBrowserCandidate(HonuaSpecClientOptions options, Uri server)
{
    options.BaseAddress = server;
    options.BearerTokenProvider = NoBrowserTokenAsync;
    options.EnableRetry = false;
}

static void ConfigureWfsBrowserCandidate(HonuaWfsClientOptions options, Uri server)
{
    options.BaseAddress = server;
    options.BearerTokenProvider = NoBrowserTokenAsync;
    options.EnableRetry = false;
}

static void ConfigureGeoServicesBrowserCandidate(HonuaGeoServicesClientOptions options, Uri server)
{
    options.BaseAddress = server;
    options.BearerTokenProvider = NoBrowserTokenAsync;
    options.EnableRetry = false;
}

static void ConfigureOgcFeaturesBrowserCandidate(HonuaOgcFeaturesClientOptions options, Uri server)
{
    options.BaseAddress = server;
    options.BearerTokenProvider = NoBrowserTokenAsync;
    options.EnableRetry = false;
}

static Task<string?> NoBrowserTokenAsync(CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult<string?>(null);
}

static void RegisterOfflineContracts(IServiceCollection services)
{
    var source = new SourceDescriptor
    {
        Id = "parks",
        Protocol = FeatureProtocolIds.OgcFeatures,
        Locator = new SourceLocator { CollectionId = "parks" },
    };

    var manifest = new OfflinePackageManifest
    {
        PackageId = "browser-smoke",
        DisplayName = "Browser smoke",
        Sources =
        [
            new OfflineSourceDescriptor
            {
                SourceId = "parks",
                Source = source,
                Where = "status = 'open'",
                FilterLanguage = FeatureFilterLanguage.Cql2Text,
                OutFields = ["name", "status"],
                PageSize = 100,
            },
        ],
    };

    services.AddSingleton(source);
    services.AddSingleton(manifest);
    services.AddSingleton(new OfflineSyncEngineOptions());
}
