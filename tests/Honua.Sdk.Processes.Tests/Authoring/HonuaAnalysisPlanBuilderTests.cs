// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Processes.Authoring;

namespace Honua.Sdk.Processes.Tests.Authoring;

public sealed class HonuaAnalysisPlanBuilderTests
{
    [Fact]
    public void BuildExecuteRequest_SerializesCanonicalPlanShape()
    {
        var request = HonuaProcessAuthoring.DefinePlan("plan-1")
            .WithWorkflowFamily("analyze")
            .WithOutputs("featureLayer")
            .AddGeoprocessStep("buffer", "geometry.buffer", step => step
                .WithInput("wkb", "AAAA")
                .WithInput("srid", 4326)
                .WithInput("distance", 25.5))
            .BuildExecuteRequest();

        var json = JsonSerializer.Serialize(request, ProcessesJsonContext.Default.HonuaProcessExecuteRequest);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("document", root.GetProperty("response").GetString());
        var plan = root.GetProperty("inputs").GetProperty("plan");
        Assert.Equal("plan-1", plan.GetProperty("planId").GetString());
        Assert.Equal("analyze", plan.GetProperty("workflowFamily").GetString());
        Assert.Equal("featureLayer", plan.GetProperty("outputs")[0].GetString());

        var step = plan.GetProperty("steps")[0];
        Assert.Equal("buffer", step.GetProperty("stepId").GetString());
        Assert.Equal("geoprocess", step.GetProperty("kind").GetString());
        Assert.Equal("geometry.buffer", step.GetProperty("processId").GetString());
        Assert.Equal("AAAA", step.GetProperty("inputs").GetProperty("wkb").GetString());
        Assert.Equal("4326", step.GetProperty("inputs").GetProperty("srid").GetString());
        Assert.Equal("25.5", step.GetProperty("inputs").GetProperty("distance").GetString());
    }

    [Fact]
    public void Build_PreservesStepDependencies()
    {
        var plan = HonuaProcessAuthoring.DefinePlan("plan-dag")
            .AddStep("query", "queryFeatures", s => s.WithInput("layerId", "parcels"))
            .AddGeoprocessStep("buffer", "geometry.buffer", s => s
                .WithInput("distance", "100")
                .DependsOn("query"))
            .Build();

        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal("query", plan.Steps[1].DependsOn.Single());
    }

    [Fact]
    public void Build_RejectsUnknownDependency()
    {
        var builder = HonuaProcessAuthoring.DefinePlan("plan-bad")
            .AddGeoprocessStep("buffer", "geometry.buffer", s => s.DependsOn("missing"));

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddStep_RejectsDuplicateStepIds()
    {
        var builder = HonuaProcessAuthoring.DefinePlan("plan-dup")
            .AddStep("s", "queryFeatures");

        Assert.Throws<InvalidOperationException>(() => builder.AddStep("s", "geoprocess"));
    }

    [Fact]
    public void Build_RejectsSelfDependency()
    {
        var builder = HonuaProcessAuthoring.DefinePlan("plan-self")
            .AddGeoprocessStep("s", "geometry.buffer", step => step.DependsOn("s"));

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }
}
