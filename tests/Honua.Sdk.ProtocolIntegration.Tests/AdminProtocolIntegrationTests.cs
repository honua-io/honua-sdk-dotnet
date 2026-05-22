namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class AdminProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task AdminCompatibility_AndServiceSettings_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var compatibility = await _fixture.AdminClient.CheckCompatibilityAsync(timeout.Token).ConfigureAwait(false);
        Assert.True(
            compatibility.IsSupported,
            compatibility.UnsupportedReason ?? "Containerized server did not meet the SDK compatibility baseline.");

        var settings = await _fixture.AdminClient.GetServiceSettingsAsync(
            _fixture.Options.ServiceName,
            timeout.Token).ConfigureAwait(false);

        Assert.Equal(_fixture.Options.ServiceName, settings.ServiceName);
        Assert.Contains(settings.EnabledProtocols, protocol => string.Equals(protocol, "Grpc", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(settings.EnabledProtocols, protocol => string.Equals(protocol, "FeatureServer", StringComparison.OrdinalIgnoreCase));
    }

    [ProtocolIntegrationFact]
    public async Task Catalog_SearchAndServiceLookup_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var services = await _fixture.CatalogClient.ListServicesAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
        Assert.Contains(services, service => string.Equals(service.Name, _fixture.Options.ServiceName, StringComparison.OrdinalIgnoreCase));

        var service = await _fixture.CatalogClient.GetServiceAsync(_fixture.Options.ServiceName, timeout.Token).ConfigureAwait(false);
        Assert.NotNull(service);
        Assert.Equal(_fixture.Options.ServiceName, service.Name);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Geocoding)]
    public async Task Geocoding_ForwardReverseSuggestAndBatch_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var forward = await _fixture.GeocodingClient.ForwardGeocodeAsync(
            _fixture.Options.GeocodeText!,
            cancellationToken: timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(forward);

        var reverse = await _fixture.GeocodingClient.ReverseGeocodeAsync(
            _fixture.Options.ReverseGeocodeLatitude!.Value,
            _fixture.Options.ReverseGeocodeLongitude!.Value,
            cancellationToken: timeout.Token).ConfigureAwait(false);
        Assert.NotNull(reverse);

        var suggestions = await _fixture.GeocodingClient.SuggestAsync(
            _fixture.Options.GeocodeText!,
            cancellationToken: timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(suggestions);

        var batch = await _fixture.GeocodingClient.BatchGeocodeAsync(
            [_fixture.Options.GeocodeText!],
            cancellationToken: timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(batch);
    }
}
