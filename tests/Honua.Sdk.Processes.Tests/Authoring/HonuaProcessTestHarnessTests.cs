// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using Honua.Sdk.Processes.Authoring;

namespace Honua.Sdk.Processes.Tests.Authoring;

public sealed class HonuaProcessTestHarnessTests
{
    // A sample authored process: doubles a distance value and reports progress + an artifact.
    private sealed class DoubleDistanceExecutor : IHonuaProcessExecutor
    {
        public string ProcessId => "sample.double-distance";

        public async Task<HonuaProcessJobResult> ExecuteAsync(
            HonuaProcessJobInput job,
            IHonuaProcessExecutionContext context,
            CancellationToken cancellationToken)
        {
            await context.ReportProgressAsync(0, "starting", cancellationToken);
            await context.AppendLogAsync($"executing {job.ProcessId}", cancellationToken);

            if (!double.TryParse(job.GetRequired("distance"), NumberStyles.Float, CultureInfo.InvariantCulture, out var distance))
            {
                return HonuaProcessJobResult.Failure("distance is not a number");
            }

            var doubled = (distance * 2).ToString(CultureInfo.InvariantCulture);
            await context.PublishArtifactAsync($"data:text/plain,{doubled}", cancellationToken);
            await context.ReportProgressAsync(100, "done", cancellationToken);

            return HonuaProcessJobResult.Success(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["result"] = doubled
            });
        }
    }

    private static HonuaProcessDefinition SampleDefinition() =>
        HonuaProcessAuthoring.DefineProcess("sample.double-distance")
            .WithTitle("Double Distance")
            .AddInput("distance", HonuaProcessParameterValueType.FloatingPoint, p => p.Required())
            .AddOutput("result", HonuaProcessArtifactKind.Scalar)
            .Build();

    [Fact]
    public async Task RunAsync_ExecutesAndCapturesContext()
    {
        var harness = new HonuaProcessTestHarness(new DoubleDistanceExecutor(), SampleDefinition());

        var run = await harness.RunAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = "21"
        });

        Assert.True(run.Succeeded);
        Assert.Equal("42", run.Result.Outputs["result"]);
        Assert.Equal(2, run.Progress.Count);
        Assert.Equal(100, run.Progress[^1].PercentComplete);
        Assert.Single(run.Artifacts);
        Assert.Contains("data:text/plain,42", run.Artifacts[0], StringComparison.Ordinal);
        Assert.Contains(run.Logs, log => log.Contains("sample.double-distance", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_ValidatesRequiredInputsAgainstDefinition()
    {
        var harness = new HonuaProcessTestHarness(new DoubleDistanceExecutor(), SampleDefinition());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.RunAsync(new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Contains("distance", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SurfacesExecutorFailure()
    {
        var harness = new HonuaProcessTestHarness(new DoubleDistanceExecutor());

        var run = await harness.RunAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["distance"] = "not-a-number"
        });

        Assert.False(run.Succeeded);
        Assert.Equal(HonuaProcessJobState.Failed, run.Result.State);
        Assert.Equal("distance is not a number", run.Result.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_AcceptsInputsFromAuthoredPlanStep()
    {
        var plan = HonuaProcessAuthoring.DefinePlan("plan-1")
            .AddGeoprocessStep("double", "sample.double-distance", s => s.WithInput("distance", 5))
            .Build();

        var harness = new HonuaProcessTestHarness(new DoubleDistanceExecutor(), SampleDefinition());
        var run = await harness.RunAsync(plan.Steps[0]);

        Assert.True(run.Succeeded);
        Assert.Equal("10", run.Result.Outputs["result"]);
    }

    [Fact]
    public void Constructor_RejectsMismatchedDefinition()
    {
        var otherDefinition = HonuaProcessAuthoring.DefineProcess("other.process").Build();

        Assert.Throws<ArgumentException>(
            () => new HonuaProcessTestHarness(new DoubleDistanceExecutor(), otherDefinition));
    }
}
