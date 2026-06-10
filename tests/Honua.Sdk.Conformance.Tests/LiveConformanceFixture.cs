using System.Globalization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.OgcFeatures.Wfs;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// Live conformance fixture. Boots the pinned <c>honua-server:nightly</c> image
/// via Testcontainers (the same mechanism as
/// <c>tests/Honua.Sdk.ProtocolIntegration.Tests/ProtocolIntegrationFixture</c>)
/// or attaches to an already-running server, then wires up the protocol clients
/// (gRPC, GeoServices FeatureServer, WFS, OGC API Features) the conformance
/// fixtures exercise.
/// <para>
/// Activated only when <c>HONUA_PROTOCOL_INTEGRATION=true</c> and a server
/// target is configured (<c>HONUA_PROTOCOL_SERVER_IMAGE</c> — pinned to the
/// nightly digest in CI — or <c>HONUA_PROTOCOL_EXTERNAL_BASE_URL</c>). Otherwise
/// every live conformance fact skips with a clear reason.
/// </para>
/// </summary>
public sealed class LiveConformanceFixture : IAsyncLifetime, IDisposable
{
    private IContainer? _serverContainer;

    public LiveConformanceOptions Options { get; } = LiveConformanceOptions.Load();

    public Uri BaseUri { get; private set; } = new("http://localhost");

    public ServiceProvider Services { get; private set; } = new ServiceCollection().BuildServiceProvider();

    public IHonuaGrpcClient GrpcClient => Services.GetRequiredService<IHonuaGrpcClient>();

    public IHonuaFeatureServerClient FeatureServerClient => Services.GetRequiredService<IHonuaFeatureServerClient>();

    public IHonuaWfsClient WfsClient => Services.GetRequiredService<IHonuaWfsClient>();

    public IHonuaOgcFeaturesClient OgcFeaturesClient => Services.GetRequiredService<IHonuaOgcFeaturesClient>();

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
            throw new InvalidOperationException(
                "HONUA_PROTOCOL_SERVER_IMAGE is required when HONUA_PROTOCOL_EXTERNAL_BASE_URL is not set.");
        }

        var builder = new ContainerBuilder(Options.ServerImage)
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

    // Explicit per-client request timeout for the conformance run. The resilience
    // pipeline requires its 30s circuit-breaker sampling window to be at least
    // double the per-attempt timeout, so the timeout must be <= 15s; the default
    // option timeout (100s) violates this and the options validator rejects it.
    // A 10s bound keeps every client's resilience pipeline valid and the run fast.
    private static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(10);

    private ServiceProvider BuildServiceProvider(Uri baseUri)
    {
        var services = new ServiceCollection();
        // gRPC may live on a separate host:port (h2c) from the HTTP protocol
        // surfaces. When HONUA_PROTOCOL_GRPC_BASE_URL is set (Testcontainers maps
        // 8081 separately), the gRPC client targets it; otherwise it shares the
        // common base.
        var grpcBaseUri = Options.GrpcBaseUri ?? baseUri;
        services.AddHonuaGrpc(options =>
        {
            options.BaseAddress = grpcBaseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
            options.Timeout = ClientTimeout;
        });
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
            options.Timeout = ClientTimeout;
        });
        services.AddHonuaWfs(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
            options.Timeout = ClientTimeout;
        });
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = baseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
            options.Timeout = ClientTimeout;
        });

        return services.BuildServiceProvider();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveConformanceCollection : ICollectionFixture<LiveConformanceFixture>
{
    public const string Name = "LiveConformance";
}

/// <summary>
/// Environment-driven configuration for the live conformance tier. Mirrors the
/// <c>HONUA_PROTOCOL_*</c> variables used by the protocol-integration suite so
/// the conformance CI job can share the same server wiring and fixture
/// identifiers.
/// </summary>
public sealed record LiveConformanceOptions
{
    public bool Enabled { get; init; }

    public Uri? ExternalBaseUri { get; init; }

    public Uri? GrpcBaseUri { get; init; }

    public string? ServerImage { get; init; }

    public ushort ServerPort { get; init; } = 8080;

    public string ServerScheme { get; init; } = "http";

    public string ServerHealthPath { get; init; } = "/healthz/ready";

    public string? ApiKey { get; init; }

    public string? BearerToken { get; init; }

    public string ServiceName { get; init; } = "sdk_integration";

    public int LayerId { get; init; }

    public string WfsTypeName { get; init; } = "public:sdk_integration_points";

    public string OgcCollectionId { get; init; } = "sdk_integration_points";

    public string? SeedProfile { get; init; }

    public bool HasServerTarget =>
        ExternalBaseUri is not null || !string.IsNullOrWhiteSpace(ServerImage);

    public static LiveConformanceOptions Load()
    {
        var enabled = ReadBoolean("HONUA_PROTOCOL_INTEGRATION");
        if (!enabled)
        {
            return new LiveConformanceOptions();
        }

        return new LiveConformanceOptions
        {
            Enabled = true,
            ExternalBaseUri = ReadUri("HONUA_PROTOCOL_EXTERNAL_BASE_URL"),
            GrpcBaseUri = ReadUri("HONUA_PROTOCOL_GRPC_BASE_URL"),
            ServerImage = ReadString("HONUA_PROTOCOL_SERVER_IMAGE"),
            ServerPort = ReadUShort("HONUA_PROTOCOL_SERVER_PORT", 8080),
            ServerScheme = ReadString("HONUA_PROTOCOL_SERVER_SCHEME") ?? "http",
            ServerHealthPath = ReadString("HONUA_PROTOCOL_SERVER_HEALTH_PATH") ?? "/healthz/ready",
            ApiKey = ReadString("HONUA_PROTOCOL_API_KEY"),
            BearerToken = ReadString("HONUA_PROTOCOL_BEARER_TOKEN"),
            ServiceName = ReadString("HONUA_PROTOCOL_SERVICE_NAME") ?? "sdk_integration",
            LayerId = ReadInt("HONUA_PROTOCOL_LAYER_ID", 0),
            WfsTypeName = ReadString("HONUA_PROTOCOL_WFS_TYPENAME") ?? "public:sdk_integration_points",
            OgcCollectionId = ReadString("HONUA_PROTOCOL_OGC_COLLECTION_ID") ?? "sdk_integration_points",
            SeedProfile = ReadString("HONUA_PROTOCOL_SEED_PROFILE"),
        };
    }

    public static string? GetSkipReason()
    {
        var options = Load();
        if (!options.Enabled)
        {
            return "Set HONUA_PROTOCOL_INTEGRATION=true to run live conformance against a Testcontainers honua-server.";
        }

        if (!options.HasServerTarget)
        {
            return "Set HONUA_PROTOCOL_SERVER_IMAGE (pinned nightly digest) or HONUA_PROTOCOL_EXTERNAL_BASE_URL to run live conformance.";
        }

        return null;
    }

    public IReadOnlyDictionary<string, string> BuildContainerEnvironment()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        AddIfPresent(values, "HONUA_PROTOCOL_SEED_PROFILE", SeedProfile);
        AddIfPresent(values, "HONUA_SEED_PROFILE", SeedProfile);
        AddIfPresent(values, "HONUA_API_KEY", ApiKey);
        AddIfPresent(values, "HONUA_BEARER_TOKEN", BearerToken);
        return values;
    }

    private static string? ReadString(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Uri? ReadUri(string name)
    {
        var value = ReadString(name);
        return value is null ? null : new Uri(value, UriKind.Absolute);
    }

    private static bool ReadBoolean(string name)
    {
        var value = ReadString(name);
        return value is not null &&
            (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));
    }

    private static int ReadInt(string name, int defaultValue)
    {
        var value = ReadString(name);
        return value is null
            ? defaultValue
            : int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static ushort ReadUShort(string name, ushort defaultValue)
    {
        var value = ReadString(name);
        return value is null
            ? defaultValue
            : ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static void AddIfPresent(IDictionary<string, string> values, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[name] = value;
        }
    }
}
