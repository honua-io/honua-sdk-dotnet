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
