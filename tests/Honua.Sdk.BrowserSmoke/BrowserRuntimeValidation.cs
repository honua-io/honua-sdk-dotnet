using System.Globalization;
using System.Net.Http.Headers;
using System.Xml;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.Wfs;

namespace Honua.Sdk.BrowserSmoke;

public sealed class BrowserRuntimeValidationService
{
    private const string CorsProbeHeaderName = "X-Honua-Browser-Smoke";

    public async Task<BrowserRuntimeValidationReport> RunAsync(
        BrowserRuntimeValidationOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return BrowserRuntimeValidationReport.NotRun("Runtime validation was not requested.");
        }

        if (options.ConfigurationError is not null)
        {
            return BrowserRuntimeValidationReport.Failed(
                [BrowserRuntimeValidationCheck.Failed("configuration", options.ConfigurationError)]);
        }

        if (options.BaseUri is null)
        {
            return BrowserRuntimeValidationReport.Failed(
                [BrowserRuntimeValidationCheck.Failed("configuration", "A live baseUrl query parameter is required.")]);
        }

        using var http = new HttpClient
        {
            BaseAddress = options.BaseUri,
            Timeout = options.Timeout
        };

        ConfigureHeaders(http, options);

        var checks = new List<BrowserRuntimeValidationCheck>();
        var ogc = new HonuaOgcFeaturesClient(http);
        var geocoding = new HonuaGeocodingClient(http);
        var featureServer = new HonuaFeatureServerClient(http);
        var wfs = new HonuaWfsClient(http);

        await RunCheckAsync(
            checks,
            "ogc-features",
            async token =>
            {
                var response = await ogc.GetItemsAsync(
                    options.CollectionId,
                    new OgcItemsParams
                    {
                        Limit = options.FeatureLimit,
                        Format = OgcFeaturesFormat.GeoJson,
                    },
                    token).ConfigureAwait(false);
                var count = response.Features?.Count ?? response.NumberReturned ?? 0;
                return FormattableString.Invariant($"{count} feature(s) returned from {options.CollectionId}.");
            },
            ct).ConfigureAwait(false);

        await RunCheckAsync(
            checks,
            "geocoding",
            async token =>
            {
                var results = await geocoding.ForwardGeocodeAsync(
                    options.Address,
                    new ForwardGeocodeOptions
                    {
                        MaxResults = options.GeocodeLimit,
                        SpatialReferenceWkid = 4326,
                    },
                    token).ConfigureAwait(false);
                return FormattableString.Invariant($"{results.Count} candidate(s) returned for {options.Address}.");
            },
            ct).ConfigureAwait(false);

        await RunCheckAsync(
            checks,
            "geoservices-featureserver",
            async token =>
            {
                var layer = await featureServer.GetLayerInfoAsync(
                    options.ServiceName,
                    options.LayerId,
                    token).ConfigureAwait(false);
                return FormattableString.Invariant($"Layer {layer.Id} metadata returned for {options.ServiceName}.");
            },
            ct).ConfigureAwait(false);

        await RunCheckAsync(
            checks,
            "wfs",
            async token =>
            {
                var capabilities = await wfs.GetCapabilitiesAsync(token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(options.WfsTypeName) &&
                    !capabilities.FeatureTypes.Any(featureType =>
                        string.Equals(featureType.Name, options.WfsTypeName, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        FormattableString.Invariant($"WFS type {options.WfsTypeName} was not advertised."));
                }

                return FormattableString.Invariant(
                    $"WFS {capabilities.Version} returned {capabilities.FeatureTypes.Count} feature type(s).");
            },
            ct).ConfigureAwait(false);

        return checks.Any(check => check.Status == BrowserRuntimeValidationCheckStatus.Failed)
            ? BrowserRuntimeValidationReport.Failed(checks)
            : BrowserRuntimeValidationReport.Passed(checks);
    }

    private static void ConfigureHeaders(HttpClient http, BrowserRuntimeValidationOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BearerToken))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.BearerToken);
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            http.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", options.ApiKey);
        }

        if (options.SendCorsProbeHeader)
        {
            http.DefaultRequestHeaders.TryAddWithoutValidation(CorsProbeHeaderName, "true");
        }
    }

    private static async Task RunCheckAsync(
        ICollection<BrowserRuntimeValidationCheck> checks,
        string name,
        Func<CancellationToken, Task<string>> operation,
        CancellationToken ct)
    {
        try
        {
            var detail = await operation(ct).ConfigureAwait(false);
            checks.Add(BrowserRuntimeValidationCheck.Passed(name, detail));
        }
        catch (HttpRequestException ex)
        {
            checks.Add(BrowserRuntimeValidationCheck.Failed(name, ex.Message));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            checks.Add(BrowserRuntimeValidationCheck.Failed(name, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            checks.Add(BrowserRuntimeValidationCheck.Failed(name, ex.Message));
        }
        catch (System.Text.Json.JsonException ex)
        {
            checks.Add(BrowserRuntimeValidationCheck.Failed(name, ex.Message));
        }
        catch (XmlException ex)
        {
            checks.Add(BrowserRuntimeValidationCheck.Failed(name, ex.Message));
        }
    }
}

public sealed record BrowserRuntimeValidationOptions
{
    public bool Enabled { get; init; }

    public Uri? BaseUri { get; init; }

    public string? ConfigurationError { get; init; }

    public string CollectionId { get; init; } = "parks";

    public string ServiceName { get; init; } = "sdk-demo";

    public int LayerId { get; init; }

    public string WfsTypeName { get; init; } = "parcels";

    public string Address { get; init; } = "Honolulu, HI";

    public int FeatureLimit { get; init; } = 5;

    public int GeocodeLimit { get; init; } = 3;

    public string? BearerToken { get; init; }

    public string? ApiKey { get; init; }

    public bool SendCorsProbeHeader { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public static BrowserRuntimeValidationOptions FromUri(Uri navigationUri)
    {
        ArgumentNullException.ThrowIfNull(navigationUri);

        var query = ParseQuery(navigationUri.Query);
        var enabled = IsEnabled(Get(query, "live"));
        var baseUrl = Get(query, "baseUrl");
        Uri? baseUri = null;
        string? configurationError = null;

        if (enabled)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                configurationError = "The baseUrl query parameter is required when live=1.";
            }
            else if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out baseUri) ||
                     !IsHttpUri(baseUri))
            {
                configurationError = "The baseUrl query parameter must be an absolute HTTP or HTTPS URI.";
            }
        }

        return new BrowserRuntimeValidationOptions
        {
            Enabled = enabled,
            BaseUri = baseUri,
            ConfigurationError = configurationError,
            CollectionId = Get(query, "collectionId") ?? "parks",
            ServiceName = Get(query, "serviceName") ?? "sdk-demo",
            LayerId = ParseInt(Get(query, "layerId"), 0),
            WfsTypeName = Get(query, "wfsTypeName") ?? "parcels",
            Address = Get(query, "address") ?? "Honolulu, HI",
            FeatureLimit = ParsePositiveInt(Get(query, "featureLimit"), 5),
            GeocodeLimit = ParsePositiveInt(Get(query, "geocodeLimit"), 3),
            BearerToken = Get(query, "bearerToken"),
            ApiKey = Get(query, "apiKey"),
            SendCorsProbeHeader = IsEnabled(Get(query, "corsProbe")),
            Timeout = TimeSpan.FromSeconds(ParsePositiveInt(Get(query, "timeoutSeconds"), 30)),
        };
    }

    private static IReadOnlyDictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.Length > 0 && query[0] == '?' ? query[1..] : query;
        if (trimmed.Length == 0)
        {
            return result;
        }

        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            var key = Decode(pair[0]);
            if (key.Length == 0)
            {
                continue;
            }

            result[key] = pair.Length == 2 ? Decode(pair[1]) : string.Empty;
        }

        return result;
    }

    private static string Decode(string value)
        => Uri.UnescapeDataString(value.Replace('+', ' '));

    private static string? Get(IReadOnlyDictionary<string, string> query, string key)
        => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static bool IsEnabled(string? value)
        => value is not null &&
           (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "on", StringComparison.OrdinalIgnoreCase));

    private static bool IsHttpUri(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string? value, int fallback)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static int ParsePositiveInt(string? value, int fallback)
    {
        var parsed = ParseInt(value, fallback);
        return parsed > 0 ? parsed : fallback;
    }
}

public sealed record BrowserRuntimeValidationReport(
    BrowserRuntimeValidationStatus Status,
    string Summary,
    IReadOnlyList<BrowserRuntimeValidationCheck> Checks)
{
    public static BrowserRuntimeValidationReport NotRun(string summary)
        => new(BrowserRuntimeValidationStatus.NotRun, summary, []);

    public static BrowserRuntimeValidationReport Running()
        => new(BrowserRuntimeValidationStatus.Running, "Runtime validation is running.", []);

    public static BrowserRuntimeValidationReport Passed(IReadOnlyList<BrowserRuntimeValidationCheck> checks)
        => new(BrowserRuntimeValidationStatus.Passed, "Browser runtime validation passed.", checks);

    public static BrowserRuntimeValidationReport Failed(IReadOnlyList<BrowserRuntimeValidationCheck> checks)
        => new(BrowserRuntimeValidationStatus.Failed, "Browser runtime validation failed.", checks);
}

public sealed record BrowserRuntimeValidationCheck(
    string Name,
    BrowserRuntimeValidationCheckStatus Status,
    string Detail)
{
    public static BrowserRuntimeValidationCheck Passed(string name, string detail)
        => new(name, BrowserRuntimeValidationCheckStatus.Passed, detail);

    public static BrowserRuntimeValidationCheck Failed(string name, string detail)
        => new(name, BrowserRuntimeValidationCheckStatus.Failed, detail);
}

public enum BrowserRuntimeValidationStatus
{
    NotRun = 0,
    Running = 1,
    Passed = 2,
    Failed = 3
}

public enum BrowserRuntimeValidationCheckStatus
{
    Skipped = 0,
    Passed = 1,
    Failed = 2
}
