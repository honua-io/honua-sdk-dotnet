using System.Diagnostics;
using System.Text.Json;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Wfs.Extensions;

namespace Honua.Sdk.IntegrationTests;

[CollectionDefinition("StagingIntegration", DisableParallelization = true)]
public sealed class StagingIntegrationCollection : ICollectionFixture<StagingIntegrationFixture>
{
}

public sealed class StagingIntegrationFixture : IAsyncLifetime, IDisposable
{
    private static readonly string[] KnownChecks =
    [
        "admin-compatibility",
        "admin-service-settings",
        "grpc-query",
        "wfs-capabilities",
        "wfs-get-features",
        "features-service-info",
        "features-query",
        "ogc-collections",
        "ogc-items",
        "ogc-item"
    ];

    private readonly Dictionary<string, StagingCheckResult> _results =
        KnownChecks.ToDictionary(name => name, StagingCheckResult.NotRun);

    public StagingIntegrationFixture()
    {
        Options = StagingIntegrationOptions.LoadFromEnvironment();

        var services = new ServiceCollection();
        services.AddHonuaAdmin(options =>
        {
            options.BaseAddress = Options.BaseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaGrpc(options =>
        {
            options.Address = Options.BaseUri.ToString();
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaWfs(options =>
        {
            options.BaseAddress = Options.BaseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = Options.BaseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = Options.BaseUri;
            options.ApiKey = Options.ApiKey;
            options.BearerToken = Options.BearerToken;
        });

        Services = services.BuildServiceProvider();
    }

    public StagingIntegrationOptions Options { get; }

    public ServiceProvider Services { get; }

    public IHonuaAdminClient AdminClient => Services.GetRequiredService<IHonuaAdminClient>();

    public IHonuaGrpcClient GrpcClient => Services.GetRequiredService<IHonuaGrpcClient>();

    public IHonuaWfsClient WfsClient => Services.GetRequiredService<IHonuaWfsClient>();

    public IHonuaFeatureServerClient FeatureServerClient => Services.GetRequiredService<IHonuaFeatureServerClient>();

    public IHonuaOgcFeaturesClient OgcFeaturesClient => Services.GetRequiredService<IHonuaOgcFeaturesClient>();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await WriteEvidenceAsync().ConfigureAwait(false);
    }

    public CancellationTokenSource CreateTimeoutScope(TimeSpan? timeout = null)
        => new(timeout ?? TimeSpan.FromSeconds(45));

    public async Task RecordCheckAsync(
        string name,
        Func<CancellationToken, Task<string>> action,
        CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var detail = await action(ct).ConfigureAwait(false);
            _results[name] = new StagingCheckResult(
                name,
                "pass",
                startedAt,
                stopwatch.ElapsedMilliseconds,
                detail);
        }
        catch (Exception ex)
        {
            _results[name] = new StagingCheckResult(
                name,
                "fail",
                startedAt,
                stopwatch.ElapsedMilliseconds,
                $"{ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        Services.Dispose();
    }

    private async Task WriteEvidenceAsync()
    {
        if (string.IsNullOrWhiteSpace(Options.EvidencePath))
        {
            return;
        }

        var evidenceDirectory = Path.GetDirectoryName(Options.EvidencePath);
        if (!string.IsNullOrWhiteSpace(evidenceDirectory))
        {
            Directory.CreateDirectory(evidenceDirectory);
        }

        var checks = KnownChecks.Select(name => _results[name]).ToArray();
        var report = new StagingEvidenceReport
        {
            SchemaVersion = "1.0",
            RunId = Options.RunId,
            RunDateUtc = DateTimeOffset.UtcNow,
            Environment = "staging",
            BaseUrl = Options.BaseUri.ToString(),
            ServiceName = Options.ServiceName,
            LayerId = Options.LayerId,
            WfsTypeName = Options.WfsTypeName,
            OgcCollectionId = Options.OgcCollectionId,
            Checks = checks,
            Summary = new StagingEvidenceSummary
            {
                Total = checks.Length,
                Passed = checks.Count(check => string.Equals(check.Status, "pass", StringComparison.Ordinal)),
                Failed = checks.Count(check => string.Equals(check.Status, "fail", StringComparison.Ordinal)),
                NotRun = checks.Count(check => string.Equals(check.Status, "not-run", StringComparison.Ordinal))
            }
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(Options.EvidencePath, json).ConfigureAwait(false);
    }

    public sealed record StagingCheckResult(
        string Name,
        string Status,
        DateTimeOffset StartedAtUtc,
        long DurationMs,
        string Details)
    {
        public static StagingCheckResult NotRun(string name)
            => new(
                name,
                "not-run",
                DateTimeOffset.UtcNow,
                0,
                "Check did not execute.");
    }

    public sealed class StagingEvidenceReport
    {
        public string SchemaVersion { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;

        public DateTimeOffset RunDateUtc { get; init; }

        public string Environment { get; init; } = string.Empty;

        public string BaseUrl { get; init; } = string.Empty;

        public string ServiceName { get; init; } = string.Empty;

        public int LayerId { get; init; }

        public string WfsTypeName { get; init; } = string.Empty;

        public string OgcCollectionId { get; init; } = string.Empty;

        public IReadOnlyList<StagingCheckResult> Checks { get; init; } = [];

        public StagingEvidenceSummary Summary { get; init; } = new();
    }

    public sealed class StagingEvidenceSummary
    {
        public int Total { get; init; }

        public int Passed { get; init; }

        public int Failed { get; init; }

        public int NotRun { get; init; }
    }
}
