namespace Honua.Sdk.IntegrationTests;

public sealed class StagingIntegrationOptions
{
    private const string BaseUrlKey = "HONUA_STAGING_BASE_URL";
    private const string ApiKeyKey = "HONUA_STAGING_API_KEY";
    private const string BearerTokenKey = "HONUA_STAGING_BEARER_TOKEN";
    private const string ServiceNameKey = "HONUA_STAGING_SERVICE_NAME";
    private const string LayerIdKey = "HONUA_STAGING_LAYER_ID";
    private const string WfsTypeNameKey = "HONUA_STAGING_WFS_TYPENAME";
    private const string OgcCollectionIdKey = "HONUA_STAGING_OGC_COLLECTION_ID";
    private const string EvidencePathKey = "HONUA_STAGING_EVIDENCE_PATH";
    private const string RunIdKey = "HONUA_STAGING_RUN_ID";
    private const string ServerCommitKey = "HONUA_STAGING_SERVER_COMMIT";
    private const string ServerImageKey = "HONUA_STAGING_SERVER_IMAGE";
    private const string SeedProfileKey = "HONUA_STAGING_SEED_PROFILE";

    public Uri BaseUri { get; init; } = new("https://localhost");

    public string? ApiKey { get; init; }

    public string? BearerToken { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public int LayerId { get; init; }

    public string WfsTypeName { get; init; } = string.Empty;

    public string OgcCollectionId { get; init; } = string.Empty;

    public string? EvidencePath { get; init; }

    public string RunId { get; init; } = string.Empty;

    public string? ServerCommit { get; init; }

    public string? ServerImage { get; init; }

    public string? SeedProfile { get; init; }

    public static IReadOnlyList<string> GetMissingEnvironmentVariables()
    {
        var missing = new List<string>();

        RequireValue(BaseUrlKey, missing);
        RequireValue(ServiceNameKey, missing);
        RequireValue(LayerIdKey, missing);
        RequireValue(WfsTypeNameKey, missing);
        RequireValue(OgcCollectionIdKey, missing);

        if (string.IsNullOrWhiteSpace(Read(ApiKeyKey)) &&
            string.IsNullOrWhiteSpace(Read(BearerTokenKey)))
        {
            missing.Add($"{ApiKeyKey} or {BearerTokenKey}");
        }

        return missing;
    }

    public static StagingIntegrationOptions LoadFromEnvironment()
    {
        var missing = GetMissingEnvironmentVariables();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing staging integration environment variables: {string.Join(", ", missing)}.");
        }

        var baseUrl = ReadRequired(BaseUrlKey);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException(
                $"{BaseUrlKey} must be an absolute HTTP or HTTPS URI. Value: '{baseUrl}'.");
        }

        if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{BaseUrlKey} must use the http or https scheme.");
        }

        var layerIdValue = ReadRequired(LayerIdKey);
        if (!int.TryParse(layerIdValue, out var layerId) || layerId < 0)
        {
            throw new InvalidOperationException(
                $"{LayerIdKey} must be a non-negative integer. Value: '{layerIdValue}'.");
        }

        return new StagingIntegrationOptions
        {
            BaseUri = baseUri,
            ApiKey = Read(ApiKeyKey),
            BearerToken = Read(BearerTokenKey),
            ServiceName = ReadRequired(ServiceNameKey),
            LayerId = layerId,
            WfsTypeName = ReadRequired(WfsTypeNameKey),
            OgcCollectionId = ReadRequired(OgcCollectionIdKey),
            EvidencePath = Read(EvidencePathKey),
            RunId = Read(RunIdKey) ?? DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture),
            ServerCommit = Read(ServerCommitKey),
            ServerImage = Read(ServerImageKey),
            SeedProfile = Read(SeedProfileKey)
        };
    }

    private static string? Read(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ReadRequired(string key)
        => Read(key) ?? throw new InvalidOperationException($"{key} must be set.");

    private static void RequireValue(string key, ICollection<string> missing)
    {
        if (Read(key) is null)
        {
            missing.Add(key);
        }
    }
}
