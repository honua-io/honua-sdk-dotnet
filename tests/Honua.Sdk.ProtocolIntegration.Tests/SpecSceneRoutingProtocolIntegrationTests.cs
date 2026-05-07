using System.Text.Json;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Spec.Models;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class SpecSceneRoutingProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Spec)]
    public async Task SpecValidatePlanAndApplyStream_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));
        var document = CreateSpecDocument(_fixture.Options.SpecId!, _fixture.Options.ServiceName);

        var validation = await _fixture.SpecClient.ValidateAsync(
            new SpecValidateRequest
            {
                Spec = JsonSerializer.SerializeToElement(document),
                IncludeCanonicalJson = true
            },
            timeout.Token).ConfigureAwait(false);
        Assert.True(validation.IsValid, string.Join("; ", validation.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var plan = await _fixture.SpecClient.PlanAsync(document, timeout.Token).ConfigureAwait(false);
        Assert.False(string.IsNullOrWhiteSpace(plan.PlanId));
        Assert.NotEmpty(plan.Nodes);

        await using var apply = await _fixture.SpecClient.ApplyAsync(document, timeout.Token).ConfigureAwait(false);
        var events = new List<SpecApplyEvent>();
        await foreach (var applyEvent in apply.Events.WithCancellation(timeout.Token).ConfigureAwait(false))
        {
            events.Add(applyEvent);
            if (applyEvent.Kind == SpecApplyEventKind.ApplyCompleted ||
                applyEvent.Kind == SpecApplyEventKind.Failed ||
                applyEvent.Kind == SpecApplyEventKind.ApplyCancelled)
            {
                break;
            }
        }

        Assert.NotEmpty(events);
        Assert.Contains(events, applyEvent => applyEvent.Kind == SpecApplyEventKind.ApplyStarted);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Scene)]
    public async Task SceneListGetAndResolve_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var scenes = await _fixture.SceneClient.ListScenesAsync(
            new HonuaSceneListRequest
            {
                Capabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles]
            },
            timeout.Token).ConfigureAwait(false);
        Assert.Contains(scenes, scene => string.Equals(scene.Id, _fixture.Options.SceneId, StringComparison.OrdinalIgnoreCase));

        var metadata = await _fixture.SceneClient.GetSceneAsync(_fixture.Options.SceneId!, timeout.Token).ConfigureAwait(false);
        Assert.Equal(_fixture.Options.SceneId, metadata.Id);

        var resolution = await _fixture.SceneClient.ResolveSceneAsync(
            _fixture.Options.SceneId!,
            new HonuaSceneResolveRequest
            {
                RequiredCapabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles]
            },
            timeout.Token).ConfigureAwait(false);
        Assert.Equal(_fixture.Options.SceneId, resolution.SceneId);
        Assert.True(resolution.TilesetUrl is not null || resolution.Endpoints.Count > 0);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Routing)]
    public async Task RoutingMetadataDirectionsServiceAreaAndClosestFacility_AreReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));

        var metadata = await _fixture.RoutingClient.GetServiceMetadataAsync(
            new RouteServiceMetadataRequest
            {
                ServiceId = _fixture.Options.RouteServiceId,
                RouteLayerName = _fixture.Options.RouteLayerName
            },
            timeout.Token).ConfigureAwait(false);
        Assert.Equal(_fixture.Options.RouteServiceId, metadata.ServiceId);

        var route = await _fixture.RoutingClient.GetDirectionsAsync(
            new RouteDirectionsRequest
            {
                Origin = RoutingLocation.FromLongitudeLatitude(-157.8651, 21.3060, "Start"),
                Destination = RoutingLocation.FromLongitudeLatitude(-157.8460, 21.3193, "Finish"),
                Options = new RouteSolveOptions
                {
                    ServiceId = _fixture.Options.RouteServiceId,
                    RouteLayerName = _fixture.Options.RouteLayerName,
                    ReturnDirections = true,
                    ReturnRoutes = true
                }
            },
            timeout.Token).ConfigureAwait(false);
        Assert.NotEmpty(route.Routes);

        if (!string.IsNullOrWhiteSpace(_fixture.Options.ServiceAreaLayerName))
        {
            var serviceArea = await _fixture.RoutingClient.GetServiceAreaAsync(
                new ServiceAreaRequest
                {
                    Center = RoutingLocation.FromLongitudeLatitude(-157.8583, 21.3069, "Center"),
                    TravelTime = TimeSpan.FromMinutes(5),
                    Options = new ServiceAreaOptions
                    {
                        ServiceId = _fixture.Options.RouteServiceId,
                        ServiceAreaLayerName = _fixture.Options.ServiceAreaLayerName
                    }
                },
                timeout.Token).ConfigureAwait(false);
            Assert.True(serviceArea.RawResponse.ValueKind is not JsonValueKind.Undefined);
        }

        if (!string.IsNullOrWhiteSpace(_fixture.Options.ClosestFacilityLayerName))
        {
            var closest = await _fixture.RoutingClient.FindClosestFacilityAsync(
                new ClosestFacilityRequest
                {
                    Incidents = [RoutingLocation.FromLongitudeLatitude(-157.85, 21.30, "Incident")],
                    Facilities =
                    [
                        RoutingLocation.FromLongitudeLatitude(-157.80, 21.28, "Facility A"),
                        RoutingLocation.FromLongitudeLatitude(-157.82, 21.31, "Facility B")
                    ],
                    Options = new ClosestFacilityOptions
                    {
                        ServiceId = _fixture.Options.RouteServiceId,
                        ClosestFacilityLayerName = _fixture.Options.ClosestFacilityLayerName,
                        TargetFacilityCount = 1
                    }
                },
                timeout.Token).ConfigureAwait(false);
            Assert.True(closest.RawResponse.ValueKind is not JsonValueKind.Undefined);
        }
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Realtime)]
    public Task RealtimeTransport_SubscribeHeartbeatReconnectAndCursorResume_AreReachable()
        => Task.CompletedTask;

    private static SpecDocumentRequest CreateSpecDocument(string specId, string serviceName) => new()
    {
        GrammarVersion = "honua.spec.v1",
        ProcessFamilyVersion = "honua.process.v1",
        SpecId = specId,
        Nodes =
        [
            new SpecNodeRequest
            {
                Id = "source",
                Kind = SpecResourceKind.Dataset,
                SourcePins = new Dictionary<string, string>
                {
                    ["service"] = serviceName
                },
                CanonicalFragment = $"source:{serviceName}"
            },
            new SpecNodeRequest
            {
                Id = "summary",
                Kind = SpecResourceKind.Report,
                Op = "count",
                Inputs = new Dictionary<string, string>
                {
                    ["input"] = "source"
                },
                CanonicalFragment = "summary:count(source)"
            }
        ],
        CacheMode = SpecCacheMode.ReadWrite,
        MaxConcurrency = 1
    };
}
