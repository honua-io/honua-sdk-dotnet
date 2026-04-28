namespace Honua.Sdk.IntegrationTests;

public sealed class StagingConfiguredFactAttribute : FactAttribute
{
    public StagingConfiguredFactAttribute()
    {
        var missing = StagingIntegrationOptions.GetMissingEnvironmentVariables();
        if (missing.Count > 0)
        {
            Skip = $"Set staging integration environment variables to run. Missing: {string.Join(", ", missing)}.";
        }
    }
}

public sealed class StagingFeatureServerEditsFactAttribute : FactAttribute
{
    public StagingFeatureServerEditsFactAttribute()
    {
        var missing = StagingIntegrationOptions.GetMissingFeatureServerEditEnvironmentVariables();
        if (missing.Count > 0)
        {
            Skip = $"Set FeatureServer edit staging variables to run. Missing: {string.Join(", ", missing)}.";
        }
    }
}
