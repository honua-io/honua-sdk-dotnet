// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Processes.Authoring;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes.Tests.Authoring;

public sealed class HonuaProcessBuilderTests
{
    [Fact]
    public void Build_PopulatesDefinitionFields()
    {
        var definition = HonuaProcessAuthoring.DefineProcess("geometry.buffer")
            .WithTitle("Buffer")
            .WithDescription("Creates a polygon at a specified distance around each input geometry.")
            .WithVersion("1.0.0")
            .WithCategory("geometry")
            .AddInput("wkb", HonuaProcessParameterValueType.Wkb, p => p
                .WithDisplayName("Input Geometry")
                .Required())
            .AddInput("srid", HonuaProcessParameterValueType.Srid, p => p
                .WithDisplayName("Spatial Reference")
                .Required())
            .AddInput("distance", HonuaProcessParameterValueType.FloatingPoint, p => p
                .WithDisplayName("Buffer Distance")
                .Required())
            .AddInput("geodesic", HonuaProcessParameterValueType.Flag, p => p
                .WithDisplayName("Geodesic")
                .WithDefault("false"))
            .AddOutput("outputFeatureLayer", HonuaProcessArtifactKind.FeatureLayer, o => o
                .WithDisplayName("Output Feature Layer"))
            .Build();

        Assert.Equal("geometry.buffer", definition.ProcessId);
        Assert.Equal("Buffer", definition.Title);
        Assert.Equal("geometry", definition.Category);
        Assert.Equal(4, definition.Parameters.Count);
        Assert.Single(definition.Outputs);
        Assert.Equal("wkb", definition.Parameters[0].Name);
        Assert.True(definition.Parameters[0].Required);
        Assert.False(definition.Parameters[3].Required);
        Assert.Equal("false", definition.Parameters[3].DefaultValue);
    }

    [Fact]
    public void ToOgcDescription_RoundTripsThroughProcessesJsonContext()
    {
        var definition = HonuaProcessAuthoring.DefineProcess("geometry.buffer")
            .WithTitle("Buffer")
            .WithDescription("Creates a polygon at a specified distance around each input geometry.")
            .AddInput("wkb", HonuaProcessParameterValueType.Wkb, p => p.WithDisplayName("Input Geometry").Required())
            .AddInput("srid", HonuaProcessParameterValueType.Srid, p => p.WithDisplayName("Spatial Reference").Required())
            .AddInput("distance", HonuaProcessParameterValueType.FloatingPoint, p => p.WithDisplayName("Buffer Distance").Required())
            .AddOutput("outputFeatureLayer", HonuaProcessArtifactKind.FeatureLayer, o => o.WithDisplayName("Output Feature Layer"))
            .Build();

        var ogc = definition.ToOgcDescription();
        var json = JsonSerializer.Serialize(ogc, ProcessesJsonContext.Default.HonuaProcessDescription);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("geometry.buffer", root.GetProperty("id").GetString());
        Assert.Equal("Buffer", root.GetProperty("title").GetString());
        Assert.Equal("1.0.0", root.GetProperty("version").GetString());
        Assert.Equal("async-execute", root.GetProperty("jobControlOptions")[0].GetString());
        Assert.Equal("value", root.GetProperty("outputTransmission")[0].GetString());

        var wkb = root.GetProperty("inputs").GetProperty("wkb");
        Assert.Equal("Input Geometry", wkb.GetProperty("title").GetString());
        Assert.Equal("string", wkb.GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal("application/wkb", wkb.GetProperty("schema").GetProperty("contentMediaType").GetString());

        Assert.Equal("integer", root.GetProperty("inputs").GetProperty("srid").GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal("number", root.GetProperty("inputs").GetProperty("distance").GetProperty("schema").GetProperty("type").GetString());
        Assert.Equal("object", root.GetProperty("outputs").GetProperty("outputFeatureLayer").GetProperty("schema").GetProperty("type").GetString());
    }

    [Fact]
    public void ToOgcDescription_DeserializesBackToEquivalentDescription()
    {
        var definition = HonuaProcessAuthoring.DefineProcess("vector.dissolve")
            .WithTitle("Dissolve")
            .AddInput("layerId", HonuaProcessParameterValueType.LayerId, p => p.Required())
            .AddInput("field", HonuaProcessParameterValueType.Text)
            .AddOutput("result", HonuaProcessArtifactKind.FeatureLayer)
            .Build();

        var json = JsonSerializer.Serialize(definition.ToOgcDescription(), ProcessesJsonContext.Default.HonuaProcessDescription);
        var restored = JsonSerializer.Deserialize(json, ProcessesJsonContext.Default.HonuaProcessDescription);

        Assert.NotNull(restored);
        Assert.Equal("vector.dissolve", restored!.Id);
        Assert.Equal("Dissolve", restored.Title);
        Assert.True(restored.Inputs.ContainsKey("layerId"));
        Assert.True(restored.Inputs.ContainsKey("field"));
        Assert.True(restored.Outputs.ContainsKey("result"));
    }

    [Fact]
    public void AddInput_RejectsDuplicateNames()
    {
        var builder = HonuaProcessAuthoring.DefineProcess("p")
            .AddInput("a", HonuaProcessParameterValueType.Text);

        Assert.Throws<InvalidOperationException>(() => builder.AddInput("a", HonuaProcessParameterValueType.Text));
    }

    [Fact]
    public void DefineProcess_RejectsBlankId()
    {
        Assert.Throws<ArgumentException>(() => HonuaProcessAuthoring.DefineProcess(" "));
    }

    [Theory]
    [InlineData(HonuaProcessParameterValueType.Text, "string", null)]
    [InlineData(HonuaProcessParameterValueType.WholeNumber, "integer", null)]
    [InlineData(HonuaProcessParameterValueType.FloatingPoint, "number", null)]
    [InlineData(HonuaProcessParameterValueType.Flag, "boolean", null)]
    [InlineData(HonuaProcessParameterValueType.Wkb, "string", "application/wkb")]
    [InlineData(HonuaProcessParameterValueType.WkbArray, "array", "application/wkb")]
    [InlineData(HonuaProcessParameterValueType.Srid, "integer", null)]
    [InlineData(HonuaProcessParameterValueType.LayerId, "string", null)]
    public void Parameter_ProjectsExpectedSchema(
        HonuaProcessParameterValueType valueType,
        string expectedType,
        string? expectedMediaType)
    {
        var schema = new HonuaProcessParameter { Name = "x", ValueType = valueType }.ToSchema();

        Assert.Equal(expectedType, schema.Type);
        Assert.Equal(expectedMediaType, schema.ContentMediaType);
    }
}
