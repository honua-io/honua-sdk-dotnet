namespace Honua.Sdk.ProtocolIntegration.Tests;

public sealed class ProtocolIntegrationConfigurationTests
{
    [Fact]
    public void Options_LoadsDefaultsAndRedactsCredentials()
    {
        var options = ProtocolIntegrationOptions.Load();

        Assert.False(string.IsNullOrWhiteSpace(options.ServiceName));
        Assert.False(string.IsNullOrWhiteSpace(options.WfsTypeName));
        Assert.False(string.IsNullOrWhiteSpace(options.OgcCollectionId));
        Assert.False(options.FeatureServerEditsSupported);
        Assert.Contains("apiKey=", options.ToRedactedSummary(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.ApiKey ?? "not-present-api-key", options.ToRedactedSummary(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.BearerToken ?? "not-present-bearer-token", options.ToRedactedSummary(), StringComparison.Ordinal);
    }

    [Fact]
    public void Options_IgnoresMalformedOptionalValuesWhenDisabled()
    {
        using var environment = new EnvironmentVariableScope(
            ("HONUA_PROTOCOL_INTEGRATION", null),
            ("HONUA_PROTOCOL_EXTERNAL_BASE_URL", "not a valid uri"),
            ("HONUA_PROTOCOL_SERVER_PORT", "not-a-port"),
            ("HONUA_PROTOCOL_LAYER_ID", "not-an-int"),
            ("HONUA_PROTOCOL_REVERSE_GEOCODE_LATITUDE", "not-a-latitude"),
            ("HONUA_PROTOCOL_REVERSE_GEOCODE_LONGITUDE", "not-a-longitude"));

        var options = ProtocolIntegrationOptions.Load();

        Assert.False(options.Enabled);
        Assert.Null(options.ExternalBaseUri);
        Assert.Equal((ushort)8080, options.ServerPort);
        Assert.Equal(0, options.LayerId);
        Assert.Null(options.ReverseGeocodeLatitude);
        Assert.Null(options.ReverseGeocodeLongitude);
    }

    [Fact]
    public void FeatureServerEdits_RunOnlyWhenTheGovernedEntitlementFlagIsSet()
    {
        using (new EnvironmentVariableScope(
            ("HONUA_PROTOCOL_INTEGRATION", "true"),
            ("HONUA_PROTOCOL_EXTERNAL_BASE_URL", "http://127.0.0.1:8080"),
            ("HONUA_PROTOCOL_DESTRUCTIVE", "true"),
            ("HONUA_PROTOCOL_FEATURESERVER_EDITS_SUPPORTED", null)))
        {
            var skipped = new ProtocolIntegrationFactAttribute(
                true,
                ProtocolIntegrationRequiredFixture.FeatureServerEditEntitlement);
            Assert.Contains("honua-sdk-dotnet#308", skipped.Skip, StringComparison.Ordinal);
        }

        using (new EnvironmentVariableScope(
            ("HONUA_PROTOCOL_INTEGRATION", "true"),
            ("HONUA_PROTOCOL_EXTERNAL_BASE_URL", "http://127.0.0.1:8080"),
            ("HONUA_PROTOCOL_DESTRUCTIVE", "true"),
            ("HONUA_PROTOCOL_FEATURESERVER_EDITS_SUPPORTED", "true")))
        {
            var running = new ProtocolIntegrationFactAttribute(
                true,
                ProtocolIntegrationRequiredFixture.FeatureServerEditEntitlement);
            Assert.Null(running.Skip);
        }
    }

    [Fact]
    public void Routing_RequiresEveryOperationFixture()
    {
        var configured = new ProtocolIntegrationOptions
        {
            RouteServiceId = "routing",
            RouteLayerName = "route",
            ServiceAreaLayerName = "service-area",
            ClosestFacilityLayerName = "closest-facility",
            RouteOrigin = new(0, 0),
            RouteDestination = new(0, 0),
            ServiceAreaCenter = new(0, 0),
            RouteIncident = new(0, 0),
            RouteFacilityA = new(0, 0),
            RouteFacilityB = new(0, 0)
        };
        Assert.True(configured.HasRoutingFixture);
        Assert.False((configured with { RouteOrigin = null }).HasRoutingFixture);
        Assert.False((configured with { RouteDestination = null }).HasRoutingFixture);
        Assert.False((configured with { ServiceAreaCenter = null }).HasRoutingFixture);
        Assert.False((configured with { RouteIncident = null }).HasRoutingFixture);
        Assert.False((configured with { RouteFacilityA = null }).HasRoutingFixture);
        Assert.False((configured with { RouteFacilityB = null }).HasRoutingFixture);
        Assert.False((configured with { RouteOrigin = new(double.NaN, 0) }).HasRoutingFixture);
        Assert.False((configured with { RouteDestination = new(181, 0) }).HasRoutingFixture);
        Assert.False((configured with { ServiceAreaCenter = new(0, -91) }).HasRoutingFixture);
        Assert.False((configured with { RouteServiceId = null }).HasRoutingFixture);
        Assert.False((configured with { RouteLayerName = null }).HasRoutingFixture);
        Assert.False((configured with { ServiceAreaLayerName = null }).HasRoutingFixture);
        Assert.False((configured with { ClosestFacilityLayerName = null }).HasRoutingFixture);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly IReadOnlyList<(string Name, string? Value)> _previousValues;

        public EnvironmentVariableScope(params (string Name, string? Value)[] values)
        {
            _previousValues = values
                .Select(value => (value.Name, Environment.GetEnvironmentVariable(value.Name)))
                .ToArray();

            foreach (var (name, value) in values)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previousValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
