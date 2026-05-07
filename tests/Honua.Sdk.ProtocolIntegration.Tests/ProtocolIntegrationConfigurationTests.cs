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
        Assert.Contains("apiKey=", options.ToRedactedSummary(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.ApiKey ?? "not-present-api-key", options.ToRedactedSummary(), StringComparison.Ordinal);
        Assert.DoesNotContain(options.BearerToken ?? "not-present-bearer-token", options.ToRedactedSummary(), StringComparison.Ordinal);
    }
}
