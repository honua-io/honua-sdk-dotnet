using System.Net;
using System.Text.Json;
using Honua.Sdk.Wfs.Exceptions;

namespace Honua.Sdk.IntegrationTests;

public sealed class StagingEvidenceTests
{
    [Fact]
    public async Task DisposeAsync_WritesSdkAndServerMetadata()
    {
        var evidencePath = Path.Combine(Path.GetTempPath(), $"honua-sdk-evidence-{Guid.NewGuid():N}.json");

        using var environment = new EnvironmentScope();
        environment.Set("HONUA_STAGING_BASE_URL", "https://localhost:5001");
        environment.Set("HONUA_STAGING_API_KEY", "test-key");
        environment.Set("HONUA_STAGING_SERVICE_NAME", "sdk-demo");
        environment.Set("HONUA_STAGING_LAYER_ID", "0");
        environment.Set("HONUA_STAGING_WFS_TYPENAME", "public:sdk_demo");
        environment.Set("HONUA_STAGING_OGC_COLLECTION_ID", "sdk-demo");
        environment.Set("HONUA_STAGING_EVIDENCE_PATH", evidencePath);
        environment.Set("HONUA_STAGING_RUN_ID", "run-1");
        environment.Set("HONUA_STAGING_SERVER_COMMIT", "server-sha");
        environment.Set("HONUA_STAGING_SERVER_IMAGE", "ghcr.io/honua/server:test");
        environment.Set("HONUA_STAGING_SEED_PROFILE", "sdk-readonly");

        var fixture = new StagingIntegrationFixture();
        try
        {
            await fixture.DisposeAsync();
        }
        finally
        {
            fixture.Dispose();
        }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath));
        var root = doc.RootElement;

        Assert.Equal("run-1", root.GetProperty("RunId").GetString());
        Assert.Equal("server-sha", root.GetProperty("ServerCommit").GetString());
        Assert.Equal("ghcr.io/honua/server:test", root.GetProperty("ServerImage").GetString());
        Assert.Equal("sdk-readonly", root.GetProperty("SeedProfile").GetString());
        Assert.False(root.GetProperty("FeatureServerEditCheckRan").GetBoolean());

        var packages = root.GetProperty("SdkPackages").EnumerateArray().ToArray();
        Assert.Contains(packages, package => HasPackage(package, "Honua.Sdk.Admin"));
        Assert.Contains(packages, package => HasPackage(package, "Honua.Sdk.Grpc"));
        Assert.All(packages, package => Assert.False(string.IsNullOrWhiteSpace(package.GetProperty("Version").GetString())));

        var surfaces = root.GetProperty("ProtocolSurfaces").EnumerateArray()
            .Select(surface => surface.GetString())
            .ToArray();
        Assert.Contains("grpc", surfaces);
        Assert.Contains("geoservices-featureserver", surfaces);
        Assert.DoesNotContain("geoservices-featureserver-edits", surfaces);

        var checks = root.GetProperty("Checks").EnumerateArray()
            .Select(check => check.GetProperty("Name").GetString())
            .ToArray();
        Assert.Contains("features-edit-roundtrip", checks);

        File.Delete(evidencePath);
    }

    [Fact]
    public async Task DisposeAsync_WritesFailureDiagnostics()
    {
        var evidencePath = Path.Combine(Path.GetTempPath(), $"honua-sdk-evidence-{Guid.NewGuid():N}.json");

        using var environment = new EnvironmentScope();
        environment.Set("HONUA_STAGING_BASE_URL", "https://localhost:5001");
        environment.Set("HONUA_STAGING_API_KEY", "test-key");
        environment.Set("HONUA_STAGING_SERVICE_NAME", "sdk-demo");
        environment.Set("HONUA_STAGING_LAYER_ID", "0");
        environment.Set("HONUA_STAGING_WFS_TYPENAME", "public:sdk_demo");
        environment.Set("HONUA_STAGING_OGC_COLLECTION_ID", "sdk-demo");
        environment.Set("HONUA_STAGING_EVIDENCE_PATH", evidencePath);
        environment.Set("HONUA_STAGING_RUN_ID", "run-2");

        var fixture = new StagingIntegrationFixture();
        try
        {
            var ex = await Assert.ThrowsAsync<HonuaWfsException>(() =>
                fixture.RecordCheckAsync(
                    "wfs-get-features",
                    "IHonuaWfsClient.GetFeaturesAsync",
                    "/wfs?SERVICE=WFS&VERSION=2.0.0&REQUEST=GetFeature",
                    _ => Task.FromException<string>(new HonuaWfsException(
                        HttpStatusCode.BadGateway,
                        "Gateway failed",
                        """{"error":"upstream unavailable","detail":"read timeout"}""",
                        "NoApplicableCode")),
                    CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
            await fixture.DisposeAsync();
        }
        finally
        {
            fixture.Dispose();
        }

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath));
        var root = doc.RootElement;
        var check = root.GetProperty("Checks").EnumerateArray()
            .Single(item => string.Equals(item.GetProperty("Name").GetString(), "wfs-get-features", StringComparison.Ordinal));
        var details = check.GetProperty("Details").GetString();

        Assert.Equal("fail", check.GetProperty("Status").GetString());
        Assert.NotNull(details);
        Assert.Contains("sdkMethod=IHonuaWfsClient.GetFeaturesAsync", details!);
        Assert.Contains("requestPath=/wfs?SERVICE=WFS&VERSION=2.0.0&REQUEST=GetFeature", details);
        Assert.Contains("exception=HonuaWfsException", details);
        Assert.Contains("status=502 BadGateway", details);
        Assert.Contains("""responseBody={"error":"upstream unavailable","detail":"read timeout"}""", details);
        Assert.Contains("exceptionCode=NoApplicableCode", details);
        Assert.Equal(1, root.GetProperty("Summary").GetProperty("Failed").GetInt32());

        File.Delete(evidencePath);
    }

    private static bool HasPackage(JsonElement package, string packageName)
        => string.Equals(package.GetProperty("Package").GetString(), packageName, StringComparison.Ordinal);

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = [];

        public void Set(string key, string value)
        {
            _originalValues.TryAdd(key, Environment.GetEnvironmentVariable(key));
            Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            foreach (var (key, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}
