namespace Honua.Sdk.ProtocolIntegration.Tests;

public enum ProtocolIntegrationRequiredFixture
{
    None,
    Routing,
    Realtime,
    Geocoding,
    Spec,
    Scene,
    SpecAndScene
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ProtocolIntegrationFactAttribute : FactAttribute
{
    public ProtocolIntegrationFactAttribute(
        bool destructive = false,
        ProtocolIntegrationRequiredFixture requiredFixture = ProtocolIntegrationRequiredFixture.None)
    {
        Destructive = destructive;
        RequiredFixture = requiredFixture;
        Skip = ProtocolIntegrationOptions.GetSkipReason(destructive);
        if (Skip is not null)
        {
            return;
        }

        var options = ProtocolIntegrationOptions.Load();
        Skip = requiredFixture switch
        {
            ProtocolIntegrationRequiredFixture.Routing when !options.HasRoutingFixture =>
                "Set HONUA_PROTOCOL_ROUTE_SERVICE_ID and HONUA_PROTOCOL_ROUTE_LAYER to run routing protocol integration tests.",
            ProtocolIntegrationRequiredFixture.Realtime =>
                "Realtime server transport integration is pending the Honua Server realtime fixture.",
            ProtocolIntegrationRequiredFixture.Geocoding when !options.HasGeocodingFixture =>
                "Set HONUA_PROTOCOL_GEOCODE_TEXT, HONUA_PROTOCOL_REVERSE_GEOCODE_LATITUDE, and HONUA_PROTOCOL_REVERSE_GEOCODE_LONGITUDE to run geocoding tests.",
            ProtocolIntegrationRequiredFixture.Spec when string.IsNullOrWhiteSpace(options.SpecId) =>
                "Set HONUA_PROTOCOL_SPEC_ID to run Spec protocol integration tests.",
            ProtocolIntegrationRequiredFixture.Scene when string.IsNullOrWhiteSpace(options.SceneId) =>
                "Set HONUA_PROTOCOL_SCENE_ID to run Scenes protocol integration tests.",
            ProtocolIntegrationRequiredFixture.SpecAndScene when string.IsNullOrWhiteSpace(options.SpecId) =>
                "Set HONUA_PROTOCOL_SPEC_ID to run Spec protocol integration tests.",
            ProtocolIntegrationRequiredFixture.SpecAndScene when string.IsNullOrWhiteSpace(options.SceneId) =>
                "Set HONUA_PROTOCOL_SCENE_ID to run Scenes protocol integration tests.",
            _ => null
        };
    }

    public bool Destructive { get; }

    public ProtocolIntegrationRequiredFixture RequiredFixture { get; }
}
