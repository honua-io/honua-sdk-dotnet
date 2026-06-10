using System.Globalization;
using FieldFormConsole;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Geometry;
using OfflineConflictConsole;
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

    [Fact]
    public async Task OfflineConflictDemo_WalksManualServerAndClientResolutionPaths()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        var summary = await OfflineConflictDemo.RunAsync(output);
        var text = output.ToString();

        // ManualReview: a conflict envelope is produced, detected, and resolved.
        Assert.Equal(1, summary.ManualReview.Conflicts);
        Assert.True(summary.ManualReview.ConflictDetected);
        Assert.Equal(1, summary.ManualReview.EditRequestCount);
        Assert.Equal(1, summary.ManualReview.JournalConflicts);
        Assert.Equal(0, summary.ManualReview.JournalSucceeded);

        // ServerWins: local edit dropped, no envelope, single edit request.
        Assert.Equal(1, summary.ServerWins.Succeeded);
        Assert.False(summary.ServerWins.ConflictDetected);
        Assert.Equal(1, summary.ServerWins.EditRequestCount);

        // ClientWins: force-write retry succeeds on the second edit request.
        Assert.Equal(1, summary.ClientWins.Succeeded);
        Assert.False(summary.ClientWins.ConflictDetected);
        Assert.Equal(2, summary.ClientWins.EditRequestCount);

        Assert.Contains("conflict detected: op=op-1", text);
        Assert.Contains("errorCode=409", text);
        Assert.Contains("resolved by reviewer: open conflicts now 0", text);
        Assert.Contains("forceWrite flags: False,True", text);
    }

    [Fact]
    public void FieldFormDemo_ReportsValidationErrorsAndCalculatedFields()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);

        var summary = FieldFormDemo.Run(output);
        var text = output.ToString();

        Assert.False(summary.InvalidIsValid);
        Assert.True(summary.InvalidErrorCount >= 3);
        Assert.True(summary.ValidIsValid);
        Assert.Equal("Leilani Kealoha", summary.CalculatedInspectorName);
        Assert.Equal("10", summary.CalculatedSampleTotal);

        Assert.Contains("First name must be at least 2 character(s).", text);
        Assert.Contains("Contact email does not match the required format.", text);
        Assert.Contains("Reason for removal is required.", text);
        Assert.Contains("validation: VALID (0 errors)", text);
        Assert.Contains("calculated inspectorName = \"Leilani Kealoha\"", text);
    }
}
