using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.BrowserSmoke;

internal sealed class BrowserFeatureMapSample(
    IHonuaOgcFeaturesClient features,
    IHonuaGeocodingClient geocoding,
    IBrowserGeoJsonDisplayAdapter display,
    BrowserFeatureMapSampleOptions options)
{
    private readonly IHonuaOgcFeaturesClient _features = features;
    private readonly IHonuaGeocodingClient _geocoding = geocoding;
    private readonly IBrowserGeoJsonDisplayAdapter _display = display;
    private readonly BrowserFeatureMapSampleOptions _options = options;

    public async Task<BrowserGeoJsonDisplayPayload> RunAsync(CancellationToken cancellationToken = default)
    {
        var featureCollection = await _features.GetItemsAsync(
            _options.CollectionId,
            new OgcItemsParams
            {
                Limit = _options.FeatureLimit,
                Filter = _options.Filter,
                FilterLang = string.IsNullOrWhiteSpace(_options.Filter) ? null : "cql2-text",
                Format = OgcFeaturesFormat.GeoJson,
            },
            cancellationToken).ConfigureAwait(false);

        var geocodes = await _geocoding.ForwardGeocodeAsync(
            _options.Address,
            new ForwardGeocodeOptions
            {
                MaxResults = _options.GeocodeLimit,
                SpatialReferenceWkid = 4326,
            },
            cancellationToken).ConfigureAwait(false);

        var payload = new BrowserGeoJsonDisplayPayload(
            _options.DisplayLayerId,
            featureCollection,
            geocodes.Select(ToMarker).ToArray());

        await _display.SetGeoJsonAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static BrowserGeocodeMarker ToMarker(GeocodeResult result)
        => new(result.Address, result.Latitude, result.Longitude, result.Score);
}

internal sealed record BrowserFeatureMapSampleOptions
{
    public string CollectionId { get; init; } = "parks";

    public string Address { get; init; } = "Honolulu, HI";

    public int FeatureLimit { get; init; } = 25;

    public int GeocodeLimit { get; init; } = 3;

    public string? Filter { get; init; } = "status = 'open'";

    public string DisplayLayerId { get; init; } = "honua-browser-sample";
}

internal sealed record BrowserGeoJsonDisplayPayload(
    string LayerId,
    OgcFeatureCollection FeatureCollection,
    IReadOnlyList<BrowserGeocodeMarker> GeocodeMarkers);

internal sealed record BrowserGeocodeMarker(
    string Address,
    double Latitude,
    double Longitude,
    double Score);

internal interface IBrowserGeoJsonDisplayAdapter
{
    ValueTask SetGeoJsonAsync(BrowserGeoJsonDisplayPayload payload, CancellationToken cancellationToken = default);
}

internal sealed class NoopBrowserGeoJsonDisplayAdapter : IBrowserGeoJsonDisplayAdapter
{
    public BrowserGeoJsonDisplayPayload? LastPayload { get; private set; }

    public ValueTask SetGeoJsonAsync(BrowserGeoJsonDisplayPayload payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastPayload = payload;
        return ValueTask.CompletedTask;
    }
}
