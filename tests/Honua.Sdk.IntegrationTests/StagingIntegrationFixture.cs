using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Wfs.Exceptions;
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
        "features-edit-roundtrip",
        "ogc-collections",
        "ogc-items",
        "ogc-item"
    ];

    private static readonly string[] ProtocolSurfaces =
    [
        "admin",
        "grpc",
        "wfs",
        "geoservices-featureserver",
        "ogc-api-features"
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

    public IHonuaFeatureServerEditClient FeatureServerEditClient => Services.GetRequiredService<IHonuaFeatureServerEditClient>();

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
        string sdkMethod,
        string requestPath,
        Func<CancellationToken, Task<string>> action,
        CancellationToken ct)
        => await RecordCheckAsync(name, () => sdkMethod, () => requestPath, action, ct).ConfigureAwait(false);

    public async Task RecordCheckAsync(
        string name,
        Func<string> sdkMethod,
        Func<string> requestPath,
        Func<CancellationToken, Task<string>> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sdkMethod);
        ArgumentNullException.ThrowIfNull(requestPath);

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
                BuildFailureDetails(sdkMethod(), requestPath(), ex));
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
        var featureServerEditsRan = WasCheckRun("features-edit-roundtrip");
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
            ServerCommit = Options.ServerCommit,
            ServerImage = Options.ServerImage,
            SeedProfile = Options.SeedProfile,
            FeatureServerEditCheckRan = featureServerEditsRan,
            ProtocolSurfaces = GetProtocolSurfaces(featureServerEditsRan),
            SdkPackages = GetSdkPackageVersions(),
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

    private IReadOnlyList<string> GetProtocolSurfaces(bool featureServerEditsRan)
    {
        if (!featureServerEditsRan)
        {
            return ProtocolSurfaces;
        }

        return [.. ProtocolSurfaces, "geoservices-featureserver-edits"];
    }

    private bool WasCheckRun(string name)
        => _results.TryGetValue(name, out var result) &&
           !string.Equals(result.Status, "not-run", StringComparison.Ordinal);

    private static IReadOnlyList<SdkPackageVersion> GetSdkPackageVersions()
        =>
        [
            CreateSdkPackageVersion("Honua.Sdk.Abstractions", typeof(Honua.Sdk.Abstractions.Features.IHonuaFeatureQueryClient).Assembly),
            CreateSdkPackageVersion("Honua.Sdk.Admin", typeof(HonuaAdminClient).Assembly),
            CreateSdkPackageVersion("Honua.Sdk.Grpc", typeof(HonuaGrpcClient).Assembly),
            CreateSdkPackageVersion("Honua.Sdk.Wfs", typeof(HonuaWfsClient).Assembly),
            CreateSdkPackageVersion("Honua.Sdk.GeoServices", typeof(HonuaFeatureServerClient).Assembly),
            CreateSdkPackageVersion("Honua.Sdk.OgcFeatures", typeof(HonuaOgcFeaturesClient).Assembly)
        ];

    private static SdkPackageVersion CreateSdkPackageVersion(string packageName, Assembly assembly)
        => new(
            packageName,
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
            assembly.GetName().Version?.ToString() ??
            "unknown");

    private static string BuildFailureDetails(string sdkMethod, string requestPath, Exception exception)
    {
        var parts = new List<string>
        {
            $"sdkMethod={sdkMethod}",
            $"requestPath={requestPath}",
            $"exception={exception.GetType().Name}",
            $"message={Summarize(exception.Message)}",
            $"status={FormatStatus(exception)}",
            $"responseBody={SummarizeResponseBody(GetResponseBody(exception))}"
        };

        AddProtocolDetails(parts, exception);
        return string.Join("; ", parts);
    }

    private static string FormatStatus(Exception exception)
    {
        if (GetHttpStatusCode(exception) is { } httpStatus)
        {
            return $"{(int)httpStatus} {httpStatus}";
        }

        if (exception is HonuaGrpcException grpcException)
        {
            return $"grpc {grpcException.StatusCode}";
        }

        return "unknown";
    }

    private static HttpStatusCode? GetHttpStatusCode(Exception exception)
        => exception switch
        {
            HonuaAdminApiException adminException => adminException.StatusCode,
            HonuaFeatureServerException featureServerException => featureServerException.StatusCode,
            HonuaOgcFeaturesException ogcException => ogcException.StatusCode,
            HonuaWfsException wfsException => wfsException.StatusCode,
            System.Net.Http.HttpRequestException { StatusCode: { } statusCode } => statusCode,
            _ => null
        };

    private static string? GetResponseBody(Exception exception)
        => exception switch
        {
            HonuaAdminApiException adminException => adminException.ResponseBody,
            HonuaFeatureServerException featureServerException => featureServerException.ResponseBody,
            HonuaOgcFeaturesException ogcException => ogcException.ResponseBody,
            HonuaWfsException wfsException => wfsException.ResponseBody,
            _ => null
        };

    private static void AddProtocolDetails(List<string> parts, Exception exception)
    {
        switch (exception)
        {
            case HonuaAdminOperationException adminOperationException
                when !string.IsNullOrWhiteSpace(adminOperationException.Operation):
                parts.Add($"operation={adminOperationException.Operation}");
                break;
            case HonuaFeatureServerException featureServerException:
                if (featureServerException.GeoServicesErrorCode.HasValue)
                {
                    parts.Add($"geoServicesErrorCode={featureServerException.GeoServicesErrorCode.Value.ToString(CultureInfo.InvariantCulture)}");
                }

                if (featureServerException.Details is { Count: > 0 })
                {
                    parts.Add($"details={Summarize(string.Join(" | ", featureServerException.Details))}");
                }

                break;
            case HonuaOgcFeaturesException ogcException:
                if (!string.IsNullOrWhiteSpace(ogcException.ProblemType))
                {
                    parts.Add($"problemType={Summarize(ogcException.ProblemType)}");
                }

                if (!string.IsNullOrWhiteSpace(ogcException.ProblemTitle))
                {
                    parts.Add($"problemTitle={Summarize(ogcException.ProblemTitle)}");
                }

                if (!string.IsNullOrWhiteSpace(ogcException.ProblemDetail))
                {
                    parts.Add($"problemDetail={Summarize(ogcException.ProblemDetail)}");
                }

                break;
            case HonuaWfsException wfsException when !string.IsNullOrWhiteSpace(wfsException.ExceptionCode):
                parts.Add($"exceptionCode={wfsException.ExceptionCode}");
                break;
        }
    }

    private static string SummarizeResponseBody(string? responseBody)
        => string.IsNullOrWhiteSpace(responseBody)
            ? "none"
            : Summarize(responseBody);

    private static string Summarize(string value, int maxLength = 300)
    {
        var normalized = string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength] + "...";
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

        public string? ServerCommit { get; init; }

        public string? ServerImage { get; init; }

        public string? SeedProfile { get; init; }

        public bool FeatureServerEditCheckRan { get; init; }

        public IReadOnlyList<string> ProtocolSurfaces { get; init; } = [];

        public IReadOnlyList<SdkPackageVersion> SdkPackages { get; init; } = [];

        public IReadOnlyList<StagingCheckResult> Checks { get; init; } = [];

        public StagingEvidenceSummary Summary { get; init; } = new();
    }

    public sealed record SdkPackageVersion(string Package, string Version);

    public sealed class StagingEvidenceSummary
    {
        public int Total { get; init; }

        public int Passed { get; init; }

        public int Failed { get; init; }

        public int NotRun { get; init; }
    }
}
