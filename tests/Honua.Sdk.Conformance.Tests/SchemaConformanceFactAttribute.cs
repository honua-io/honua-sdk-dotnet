namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// A fact that runs only when the shared conformance fixtures have been fetched
/// (<c>HONUA_CONFORMANCE_FIXTURES_DIR</c> set). Otherwise it skips with a clear
/// reason. Schema conformance needs no server — it validates the SDK's pinned
/// generated gRPC client against the same <c>geospatial.v1</c> schema release as
/// the fixtures, so it can run in the normal CI matrix as well as the live job.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SchemaConformanceFactAttribute : FactAttribute
{
    public SchemaConformanceFactAttribute()
    {
        Skip = ConformanceFixtures.GetSkipReason();
    }
}
