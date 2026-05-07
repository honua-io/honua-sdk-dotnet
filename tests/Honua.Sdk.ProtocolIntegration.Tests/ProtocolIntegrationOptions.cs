using System.Globalization;

namespace Honua.Sdk.ProtocolIntegration.Tests;

public sealed record ProtocolIntegrationOptions
{
    public bool Enabled { get; init; }

    public Uri? ExternalBaseUri { get; init; }

    public string? ServerImage { get; init; }

    public ushort ServerPort { get; init; } = 8080;

    public string ServerScheme { get; init; } = "http";

    public string ServerHealthPath { get; init; } = "/health";

    public string? ApiKey { get; init; }

    public string? BearerToken { get; init; }

    public string ServiceName { get; init; } = "sdk_integration";

    public int LayerId { get; init; }

    public string WfsTypeName { get; init; } = "public:sdk_integration_points";

    public string OgcCollectionId { get; init; } = "sdk_integration_points";

    public string? SceneId { get; init; }

    public string? SpecId { get; init; }

    public string? RouteServiceId { get; init; }

    public string? RouteLayerName { get; init; }

    public string? ServiceAreaLayerName { get; init; }

    public string? ClosestFacilityLayerName { get; init; }

    public string? GeocodeText { get; init; }

    public double? ReverseGeocodeLatitude { get; init; }

    public double? ReverseGeocodeLongitude { get; init; }

    public bool DestructiveEnabled { get; init; }

    public string? SeedProfile { get; init; }

    public string? FeatureServerEditAddAttributesJson { get; init; }

    public string? FeatureServerEditUpdateAttributesJson { get; init; }

    public string? FeatureServerEditGeometryJson { get; init; }

    public bool HasServerTarget =>
        ExternalBaseUri is not null || !string.IsNullOrWhiteSpace(ServerImage);

    public bool HasRoutingFixture =>
        !string.IsNullOrWhiteSpace(RouteServiceId) &&
        !string.IsNullOrWhiteSpace(RouteLayerName);

    public bool HasGeocodingFixture =>
        !string.IsNullOrWhiteSpace(GeocodeText) &&
        ReverseGeocodeLatitude.HasValue &&
        ReverseGeocodeLongitude.HasValue;

    public static ProtocolIntegrationOptions Load()
    {
        var enabled = ReadBoolean("HONUA_PROTOCOL_INTEGRATION");
        if (!enabled)
        {
            return new ProtocolIntegrationOptions();
        }

        return new ProtocolIntegrationOptions
        {
            Enabled = true,
            ExternalBaseUri = ReadUri("HONUA_PROTOCOL_EXTERNAL_BASE_URL"),
            ServerImage = ReadString("HONUA_PROTOCOL_SERVER_IMAGE"),
            ServerPort = ReadUShort("HONUA_PROTOCOL_SERVER_PORT", 8080),
            ServerScheme = ReadString("HONUA_PROTOCOL_SERVER_SCHEME") ?? "http",
            ServerHealthPath = ReadString("HONUA_PROTOCOL_SERVER_HEALTH_PATH") ?? "/health",
            ApiKey = ReadString("HONUA_PROTOCOL_API_KEY"),
            BearerToken = ReadString("HONUA_PROTOCOL_BEARER_TOKEN"),
            ServiceName = ReadString("HONUA_PROTOCOL_SERVICE_NAME") ?? "sdk_integration",
            LayerId = ReadInt("HONUA_PROTOCOL_LAYER_ID", 0),
            WfsTypeName = ReadString("HONUA_PROTOCOL_WFS_TYPENAME") ?? "public:sdk_integration_points",
            OgcCollectionId = ReadString("HONUA_PROTOCOL_OGC_COLLECTION_ID") ?? "sdk_integration_points",
            SceneId = ReadString("HONUA_PROTOCOL_SCENE_ID"),
            SpecId = ReadString("HONUA_PROTOCOL_SPEC_ID"),
            RouteServiceId = ReadString("HONUA_PROTOCOL_ROUTE_SERVICE_ID"),
            RouteLayerName = ReadString("HONUA_PROTOCOL_ROUTE_LAYER"),
            ServiceAreaLayerName = ReadString("HONUA_PROTOCOL_SERVICE_AREA_LAYER"),
            ClosestFacilityLayerName = ReadString("HONUA_PROTOCOL_CLOSEST_FACILITY_LAYER"),
            GeocodeText = ReadString("HONUA_PROTOCOL_GEOCODE_TEXT"),
            ReverseGeocodeLatitude = ReadDouble("HONUA_PROTOCOL_REVERSE_GEOCODE_LATITUDE"),
            ReverseGeocodeLongitude = ReadDouble("HONUA_PROTOCOL_REVERSE_GEOCODE_LONGITUDE"),
            DestructiveEnabled = ReadBoolean("HONUA_PROTOCOL_DESTRUCTIVE"),
            SeedProfile = ReadString("HONUA_PROTOCOL_SEED_PROFILE"),
            FeatureServerEditAddAttributesJson = ReadString("HONUA_PROTOCOL_FEATURESERVER_EDIT_ADD_ATTRIBUTES_JSON"),
            FeatureServerEditUpdateAttributesJson = ReadString("HONUA_PROTOCOL_FEATURESERVER_EDIT_UPDATE_ATTRIBUTES_JSON"),
            FeatureServerEditGeometryJson = ReadString("HONUA_PROTOCOL_FEATURESERVER_EDIT_GEOMETRY_JSON")
        };
    }

    public static string? GetSkipReason(bool destructive = false)
    {
        var options = Load();
        if (!options.Enabled)
        {
            return "Set HONUA_PROTOCOL_INTEGRATION=true to run Testcontainers protocol integration tests.";
        }

        if (!options.HasServerTarget)
        {
            return "Set HONUA_PROTOCOL_SERVER_IMAGE or HONUA_PROTOCOL_EXTERNAL_BASE_URL to run protocol integration tests.";
        }

        if (destructive && !options.DestructiveEnabled)
        {
            return "Set HONUA_PROTOCOL_DESTRUCTIVE=true to run mutating protocol integration tests.";
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

    public string ToRedactedSummary()
        => string.Join(
            "; ",
            [
                $"enabled={Enabled}",
                $"serverImage={(string.IsNullOrWhiteSpace(ServerImage) ? "none" : ServerImage)}",
                $"externalBaseUrl={(ExternalBaseUri is null ? "none" : ExternalBaseUri)}",
                $"service={ServiceName}",
                $"layerId={LayerId.ToString(CultureInfo.InvariantCulture)}",
                $"wfsType={WfsTypeName}",
                $"ogcCollection={OgcCollectionId}",
                $"scene={(string.IsNullOrWhiteSpace(SceneId) ? "none" : SceneId)}",
                $"spec={(string.IsNullOrWhiteSpace(SpecId) ? "none" : SpecId)}",
                $"routing={(HasRoutingFixture ? RouteServiceId : "none")}",
                $"geocoding={(HasGeocodingFixture ? "configured" : "none")}",
                $"destructive={DestructiveEnabled}",
                $"apiKey={(string.IsNullOrWhiteSpace(ApiKey) ? "none" : "configured")}",
                $"bearer={(string.IsNullOrWhiteSpace(BearerToken) ? "none" : "configured")}"
            ]);

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

    private static double? ReadDouble(string name)
    {
        var value = ReadString(name);
        return value is null
            ? null
            : double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
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
