// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests;

#pragma warning disable CA1812 // Fixture DTOs are instantiated by System.Text.Json.

public sealed class SharedSemanticContractFixtureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Lazy<ContractFixture> Fixture = new(() =>
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "sdk-contract",
            "semantic-contract.v1.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ContractFixture>(json, JsonOptions)
               ?? throw new InvalidOperationException("Unable to deserialize shared SDK semantic contract fixture.");
    });

    [Fact]
    public void ProtocolRegistry_MatchesSharedFixture()
    {
        Assert.Equal(1, Fixture.Value.SchemaVersion);
        Assert.Equal(Fixture.Value.Protocols, FeatureProtocolIds.All);
    }

    [Fact]
    public void CapabilityRegistry_MatchesSharedFixture()
    {
        Assert.Equal(Fixture.Value.Capabilities, FeatureCapabilities.All);
    }

    [Fact]
    public void DefaultCapabilitySets_MatchSharedFixture()
    {
        Assert.Equal(Fixture.Value.Protocols, Fixture.Value.DefaultCapabilities.Keys);

        foreach (var protocol in Fixture.Value.Protocols)
        {
            Assert.Equal(
                Fixture.Value.DefaultCapabilities[protocol],
                FeatureProtocolCapabilities.DefaultsFor(protocol));
        }
    }

    [Fact]
    public void ProtocolAliases_NormalizeToSharedCanonicalIds()
    {
        foreach (var (alias, canonical) in Fixture.Value.ProtocolAliases)
        {
            Assert.NotEqual(alias, canonical);
            Assert.Contains(canonical, FeatureProtocolIds.All);
            Assert.Equal(canonical, FeatureProtocolIds.Normalize(alias));
            Assert.True(FeatureProtocolIds.Matches(alias, canonical));
        }
    }

    [Fact]
    public void LanguageBindings_DocumentDotNetFacadeNames()
    {
        var bindings = Fixture.Value.LanguageBindings.ToDictionary(binding => binding.Concept);

        Assert.Equal("QueryAllAsync()", bindings["queryAll"].Dotnet);
        Assert.Equal("QueryPagesAsync()", bindings["stream"].Dotnet.Split(" or ")[^1]);
        Assert.Equal("QueryObjectIdsAsync()", bindings["queryObjectIds"].Dotnet);
        Assert.Equal("ApplyEditsAsync()", bindings["applyEdits"].Dotnet);
        Assert.Equal("ReturnGeometry", bindings["returnGeometry"].Dotnet);
        Assert.Equal("OutFields", bindings["outFields"].Dotnet);
        Assert.Equal("source.Protocol(...)", bindings["protocolEscapeHatch"].Dotnet);
    }

    [Fact]
    public void ResultScenarios_ReferenceKnownProtocolsAndCapabilities()
    {
        var protocolsWithResults = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var scenario in Fixture.Value.ResultScenarios)
        {
            protocolsWithResults.Add(scenario.Protocol);
            Assert.Contains(scenario.Protocol, FeatureProtocolIds.All);
            Assert.Matches("^[a-z0-9-]+$", scenario.Id);
            Assert.True(scenario.Query.ValueKind is JsonValueKind.Object);
            Assert.True(scenario.Result.Features.Count >= 0);

            if (scenario.SourceDescriptor is { } descriptor)
            {
                Assert.Equal(scenario.Protocol, descriptor.Protocol);
                foreach (var capability in descriptor.Capabilities)
                {
                    Assert.Contains(capability, FeatureCapabilities.All);
                }
            }

            foreach (var feature in scenario.Result.Features)
            {
                Assert.True(feature.Attributes.ValueKind is JsonValueKind.Object);
                if (feature.Geometry.HasValue)
                {
                    Assert.True(feature.Geometry.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Null);
                }
            }

            foreach (var degraded in scenario.Result.Degraded)
            {
                Assert.Contains(degraded.Capability, FeatureCapabilities.All);
                Assert.False(string.IsNullOrWhiteSpace(degraded.Reason));
                if (degraded.Protocol is { } protocol)
                {
                    Assert.Contains(protocol, FeatureProtocolIds.All);
                }
            }
        }

        Assert.Equal(
            ["geoservices-feature-service", "odata", "ogc-features", "stac", "wfs"],
            protocolsWithResults);
    }

    [Fact]
    public void UnsupportedCapabilityScenarios_ReferenceKnownProtocolsAndCapabilities()
    {
        foreach (var scenario in Fixture.Value.UnsupportedCapabilityScenarios)
        {
            Assert.Contains(scenario.Protocol, FeatureProtocolIds.All);
            Assert.Contains(scenario.RequiredCapability, FeatureCapabilities.All);
            Assert.Equal("HonuaCapabilityNotSupportedError", scenario.ExpectedError.Name);
            Assert.Equal(scenario.RequiredCapability, scenario.ExpectedError.Capability);
            Assert.Equal(scenario.Protocol, scenario.ExpectedError.Protocol);
            Assert.False(string.IsNullOrWhiteSpace(scenario.Reason));
        }
    }

    private sealed record ContractFixture
    {
        public int SchemaVersion { get; init; }

        public IReadOnlyList<LanguageBinding> LanguageBindings { get; init; } = [];

        public IReadOnlyList<string> Protocols { get; init; } = [];

        public IReadOnlyDictionary<string, string> ProtocolAliases { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public IReadOnlyList<string> Capabilities { get; init; } = [];

        public IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultCapabilities { get; init; } =
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        public IReadOnlyList<ResultScenario> ResultScenarios { get; init; } = [];

        public IReadOnlyList<UnsupportedCapabilityScenario> UnsupportedCapabilityScenarios { get; init; } = [];
    }

    private sealed record LanguageBinding
    {
        public string Concept { get; init; } = string.Empty;

        public string Dotnet { get; init; } = string.Empty;
    }

    private sealed record ResultScenario
    {
        public string Id { get; init; } = string.Empty;

        public string Protocol { get; init; } = string.Empty;

        public SourceDescriptorFixture? SourceDescriptor { get; init; }

        public JsonElement Query { get; init; }

        public ResultFixture Result { get; init; } = new();
    }

    private sealed record SourceDescriptorFixture
    {
        public string Protocol { get; init; } = string.Empty;

        public IReadOnlyList<string> Capabilities { get; init; } = [];
    }

    private sealed record ResultFixture
    {
        public IReadOnlyList<FeatureFixture> Features { get; init; } = [];

        public bool ExceededTransferLimit { get; init; }

        public IReadOnlyList<DegradedReasonFixture> Degraded { get; init; } = [];
    }

    private sealed record FeatureFixture
    {
        public JsonElement Attributes { get; init; }

        public JsonElement? Geometry { get; init; }
    }

    private sealed record DegradedReasonFixture
    {
        public string Capability { get; init; } = string.Empty;

        public string? Protocol { get; init; }

        public string Reason { get; init; } = string.Empty;
    }

    private sealed record UnsupportedCapabilityScenario
    {
        public string Protocol { get; init; } = string.Empty;

        public string RequiredCapability { get; init; } = string.Empty;

        public string Reason { get; init; } = string.Empty;

        public ExpectedError ExpectedError { get; init; } = new();
    }

    private sealed record ExpectedError
    {
        public string Name { get; init; } = string.Empty;

        public string Capability { get; init; } = string.Empty;

        public string Protocol { get; init; } = string.Empty;
    }
}
