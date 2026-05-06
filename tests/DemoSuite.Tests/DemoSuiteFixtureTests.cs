using System.Globalization;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Geometry;
using RealtimeWorker;
using RoutingGeofenceConsole;
using Xunit;

namespace DemoSuite.Tests;

public sealed class DemoSuiteFixtureTests
{
    [Fact]
    public async Task RealtimeWorkerSimulation_RejectsDuplicateAndStaleEvents()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        var summary = await RealtimeWorkerSimulation.RunAsync(output);
        var text = output.ToString();

        Assert.Equal(4, summary.AcceptedCount);
        Assert.Equal(2, summary.RejectedCount);
        Assert.Equal(0, summary.ActiveIncidentCount);
        Assert.Equal(1, summary.ClosedIncidentCount);
        Assert.Equal(4, summary.LastSequenceNumber);
        Assert.Equal("resume-4", summary.ResumeToken);
        Assert.Contains(FeatureStreamBufferWriteDecision.DuplicateSequence, summary.Decisions);
        Assert.Contains(FeatureStreamBufferWriteDecision.StaleSequence, summary.Decisions);
        Assert.Contains("#2 Update incident-42 duplicate-sequence", text);
        Assert.Contains("active=0 closed=1 lastSequence=4 resume=resume-4", text);
    }

    [Fact]
    public async Task RoutingGeofenceDemo_UsesSimulatedRouteAndDeterministicTransitions()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        var summary = await RoutingGeofenceDemo.RunAsync(
            output,
            new SimulatedRoutingClient(),
            "simulated");
        var text = output.ToString();

        Assert.Equal(1, summary.RouteCount);
        Assert.Equal(2, summary.DirectionCount);
        Assert.True(summary.RouteDistanceMeters.HasValue);
        Assert.Equal(4200, summary.RouteDistanceMeters.Value);
        Assert.Equal(TimeSpan.FromMinutes(9.5), summary.RouteTime);
        Assert.Equal(
            [
                HonuaGeofenceTransition.Approached,
                HonuaGeofenceTransition.Entered,
                HonuaGeofenceTransition.Exited,
                HonuaGeofenceTransition.Departed
            ],
            summary.GeofenceTransitions);
        Assert.Contains("Honolulu Harbor to Dispatch Yard distance=4200m", text);
        Assert.Contains("12:00:30 truck-7 Outside Departed distance=15m", text);
    }
}
