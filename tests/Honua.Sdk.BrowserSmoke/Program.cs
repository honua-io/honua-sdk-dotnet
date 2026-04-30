using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Abstractions.Plugins;
using Honua.Sdk.Abstractions.UtilityNetworks;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.BrowserSmoke;
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;
using Honua.Sdk.Geometry;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.Offline;
using Honua.Sdk.Offline.Abstractions;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Scenes;
using Honua.Sdk.Scenes.Extensions;
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
RegisterBrowserFeatureMapSample(builder.Services);
RegisterFieldContracts(builder.Services);
RegisterGeometryContracts(builder.Services);
RegisterOfflineContracts(builder.Services);
RegisterAdvancedEditingContracts(builder.Services);
RegisterPluginContracts(builder.Services);
RegisterRealtimeContracts(builder.Services);
RegisterUtilityNetworkContracts(builder.Services);

var host = builder.Build();
_ = host.Services.GetRequiredService<BrowserFeatureMapSample>();
await host.RunAsync().ConfigureAwait(false);

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
    services.AddHonuaRouting(options =>
    {
        ConfigureGeoServicesBrowserCandidate(options, server);
    });
    services.AddHonuaOgcFeatures(options =>
    {
        ConfigureOgcFeaturesBrowserCandidate(options, server);
    });
    services.AddHonuaScenes(options =>
    {
        ConfigureSceneBrowserCandidate(options, server);
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

static void ConfigureSceneBrowserCandidate(HonuaSceneClientOptions options, Uri server)
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

static void RegisterGeometryContracts(IServiceCollection services)
{
    services.AddSingleton(HonuaSpatialReference.Wgs84);
    services.AddSingleton<HonuaCoordinateTransformer>();
}

static void RegisterBrowserFeatureMapSample(IServiceCollection services)
{
    services.AddSingleton(new BrowserFeatureMapSampleOptions());
    services.AddScoped<IBrowserGeoJsonDisplayAdapter>(_ => new NoopBrowserGeoJsonDisplayAdapter());
    services.AddScoped(sp => new BrowserFeatureMapSample(
        sp.GetRequiredService<IHonuaOgcFeaturesClient>(),
        sp.GetRequiredService<IHonuaGeocodingClient>(),
        sp.GetRequiredService<IBrowserGeoJsonDisplayAdapter>(),
        sp.GetRequiredService<BrowserFeatureMapSampleOptions>()));
}

static void RegisterFieldContracts(IServiceCollection services)
{
    var form = new FormDefinition
    {
        FormId = "inspection",
        Name = "Inspection",
        Sections =
        [
            new FormSection
            {
                SectionId = "main",
                Label = "Main",
                Fields =
                [
                    new FormField
                    {
                        FieldId = "asset_id",
                        Label = "Asset ID",
                        Type = FormFieldType.Text,
                        Required = true,
                    },
                ],
            },
        ],
    };

    var record = new FieldRecord
    {
        RecordId = "browser-record",
        FormId = form.FormId,
        Values =
        {
            ["asset_id"] = "A-100",
        },
    };

    services.AddSingleton(form);
    services.AddSingleton(FormValidator.Validate(form, record));
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

static void RegisterAdvancedEditingContracts(IServiceCollection services)
{
    var domain = new FeatureFieldDomain
    {
        DomainId = "status-domain",
        Name = "Status",
        FieldName = "status",
        Type = FeatureFieldDomainType.CodedValue,
        CodedValues =
        [
            new FeatureFieldDomainCode
            {
                Value = JsonValue("\"open\""),
                Label = "Open",
            },
        ],
    };
    var metadata = new FeatureEditingRulesMetadata
    {
        Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
        FieldDomains = [domain],
        AttributeRules =
        [
            new FeatureAttributeRule
            {
                RuleId = "status-required",
                Name = "Status is required",
                Type = FeatureAttributeRuleType.Constraint,
                FieldName = "status",
                Triggers = [FeatureAttributeRuleTrigger.Insert, FeatureAttributeRuleTrigger.Update],
                ErrorMessage = "Status is required.",
            },
        ],
        Versioning = new FeatureEditVersioningCapabilities
        {
            SupportsVersionName = true,
            SupportsEditSessions = true,
            DefaultVersionName = "sde.DEFAULT",
        },
    };
    var validation = new FeatureEditValidationResult
    {
        FieldName = "status",
        RuleId = "status-required",
        RuleName = "Status is required",
        Severity = FeatureEditValidationSeverity.Warning,
        Message = "Status should be reviewed.",
        SuggestedFix = "Choose an active status.",
    };

    services.AddSingleton(domain);
    services.AddSingleton(metadata);
    services.AddSingleton(validation);
    services.AddSingleton(new FeatureEditSession
    {
        SessionId = "browser-session",
        VersionName = "sde.DEFAULT",
        StartedAt = DateTimeOffset.UnixEpoch,
    });
}

static void RegisterPluginContracts(IServiceCollection services)
{
    var manifest = new HonuaPluginManifest
    {
        SchemaVersion = HonuaPluginManifest.CurrentSchemaVersion,
        PluginId = "io.honua.browser-smoke",
        DisplayName = "Browser Smoke",
        Publisher = "Honua",
        Version = "0.0.0-browser-smoke",
        Compatibility = new HonuaPluginCompatibility
        {
            SupportedHosts = [HonuaPluginHostKinds.Web],
        },
        Capabilities = ["feature-stream"],
    };

    services.AddSingleton(manifest);
    services.AddSingleton(manifest.Validate());
}

static void RegisterRealtimeContracts(IServiceCollection services)
{
    var processor = new FeatureStreamEventProcessor();
    var featureEvent = new FeatureStreamEvent
    {
        SubscriptionId = "browser-stream",
        Source = new FeatureSource { CollectionId = "parks" },
        Kind = FeatureStreamEventKind.Update,
        FeatureId = "park-1",
        Timestamp = DateTimeOffset.UnixEpoch,
        SequenceNumber = 1,
    };

    services.AddSingleton(processor);
    services.AddSingleton(new FeatureStreamCapabilities
    {
        SupportsConnect = true,
        SupportsSequenceNumbers = true,
        SupportsResumeTokens = true,
        NativeSurface = "browser-host-adapter",
    });
    services.AddSingleton(featureEvent);
    services.AddSingleton(processor.Process(featureEvent));
}

static void RegisterUtilityNetworkContracts(IServiceCollection services)
{
    var source = new UtilityNetworkSource
    {
        ServiceId = "electric",
        NetworkId = "distribution",
        NetworkName = "Electric Distribution",
    };
    var startingPoint = new UtilityNetworkTraceStartingPoint
    {
        Element = new UtilityNetworkElementReference
        {
            ElementId = "switch-1",
            NetworkSourceId = "devices",
            NetworkSourceName = "Electric Device",
            TerminalId = "load",
        },
    };
    var namedConfiguration = new UtilityNetworkNamedTraceConfiguration
    {
        ConfigurationId = "primary-upstream",
        Name = "Primary upstream",
        TraceType = UtilityNetworkTraceType.Upstream,
        Configuration = new UtilityNetworkTraceConfiguration
        {
            TraceType = UtilityNetworkTraceType.Upstream,
            DomainNetwork = "ElectricDistribution",
            Tier = "MediumVoltage",
            OutputNetworkAttributes = ["phase", "status"],
        },
    };
    var traceRequest = new UtilityNetworkTraceRequest
    {
        Source = source,
        NamedConfigurationId = namedConfiguration.ConfigurationId,
        StartingPoints = [startingPoint],
        ReturnGeometry = true,
    };

    services.AddSingleton(source);
    services.AddSingleton(namedConfiguration);
    services.AddSingleton(traceRequest);
    services.AddSingleton(new UtilityNetworkTraceCapabilities
    {
        SupportsUpstreamTrace = true,
        SupportsNamedTraceConfigurations = true,
        SupportsTerminals = true,
        SupportsAssociations = true,
        NativeSurface = "browser-host-adapter",
    });
}

static JsonElement JsonValue(string json)
{
    using var document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
}
