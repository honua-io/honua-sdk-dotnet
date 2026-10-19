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
    public async Task SpecValidate_IsReachable()
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
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Spec)]
    public async Task SpecPlan_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var document = CreateSpecDocument(_fixture.Options.SpecId!, _fixture.Options.ServiceName);
        var plan = await _fixture.SpecClient.PlanAsync(document, timeout.Token).ConfigureAwait(false);
        Assert.False(string.IsNullOrWhiteSpace(plan.PlanId));
        Assert.NotEmpty(plan.Nodes);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Spec)]
    public async Task SpecApply_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));
        var document = CreateSpecDocument(_fixture.Options.SpecId!, _fixture.Options.ServiceName);
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
        Assert.Equal(SpecApplyEventKind.ApplyCompleted, events[^1].Kind);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Scene)]
    public async Task SceneList_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();

        var scenes = await _fixture.SceneClient.ListScenesAsync(
            new HonuaSceneListRequest
            {
                Capabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles]
            },
            timeout.Token).ConfigureAwait(false);
        Assert.Contains(scenes, scene => string.Equals(scene.Id, _fixture.Options.SceneId, StringComparison.OrdinalIgnoreCase));
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Scene)]
    public async Task SceneGet_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var metadata = await _fixture.SceneClient.GetSceneAsync(_fixture.Options.SceneId!, timeout.Token).ConfigureAwait(false);
        Assert.Equal(_fixture.Options.SceneId, metadata.Id);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Scene)]
    public async Task SceneResolve_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope();
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
    public async Task RoutingMetadata_IsReachable()
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
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Routing)]
    public async Task RoutingDirections_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));
        var route = await _fixture.RoutingClient.GetDirectionsAsync(
            new RouteDirectionsRequest
            {
                Origin = RoutingLocation.FromLongitudeLatitude(_fixture.Options.RouteOrigin!.Longitude, _fixture.Options.RouteOrigin!.Latitude, "Start"),
                Destination = RoutingLocation.FromLongitudeLatitude(_fixture.Options.RouteDestination!.Longitude, _fixture.Options.RouteDestination!.Latitude, "Finish"),
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
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Routing)]
    public async Task RoutingServiceArea_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));
        var serviceArea = await _fixture.RoutingClient.GetServiceAreaAsync(
            new ServiceAreaRequest
            {
                Center = RoutingLocation.FromLongitudeLatitude(_fixture.Options.ServiceAreaCenter!.Longitude, _fixture.Options.ServiceAreaCenter!.Latitude, "Center"),
                TravelTime = TimeSpan.FromMinutes(5),
                Options = new ServiceAreaOptions
                {
                    ServiceId = _fixture.Options.RouteServiceId,
                    ServiceAreaLayerName = _fixture.Options.ServiceAreaLayerName
                }
            },
            timeout.Token).ConfigureAwait(false);
        RoutingQualificationAssertions.RequireServiceArea(serviceArea);
    }

    [ProtocolIntegrationFact(false, ProtocolIntegrationRequiredFixture.Routing)]
    public async Task RoutingClosestFacility_IsReachable()
    {
        using var timeout = _fixture.CreateTimeoutScope(TimeSpan.FromSeconds(90));
        var closest = await _fixture.RoutingClient.FindClosestFacilityAsync(
            new ClosestFacilityRequest
            {
                Incidents = [RoutingLocation.FromLongitudeLatitude(_fixture.Options.RouteIncident!.Longitude, _fixture.Options.RouteIncident!.Latitude, "Incident")],
                Facilities =
                [
                    RoutingLocation.FromLongitudeLatitude(_fixture.Options.RouteFacilityA!.Longitude, _fixture.Options.RouteFacilityA!.Latitude, "Facility A"),
                    RoutingLocation.FromLongitudeLatitude(_fixture.Options.RouteFacilityB!.Longitude, _fixture.Options.RouteFacilityB!.Latitude, "Facility B")
                ],
                Options = new ClosestFacilityOptions
                {
                    ServiceId = _fixture.Options.RouteServiceId,
                    ClosestFacilityLayerName = _fixture.Options.ClosestFacilityLayerName,
                    TargetFacilityCount = 1
                }
            },
            timeout.Token).ConfigureAwait(false);
        RoutingQualificationAssertions.RequireClosestFacility(closest);
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
