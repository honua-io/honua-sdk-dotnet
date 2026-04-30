// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Abstractions.UtilityNetworks;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class UtilityNetworkContractTests
{
    [Fact]
    public void TraceRequest_ModelsNamedConfigurationsTerminalsAndBarriers()
    {
        var startingElement = ElementReference("switch-1", terminalId: "load");
        var barrierElement = ElementReference("fuse-9");

        var request = new UtilityNetworkTraceRequest
        {
            Source = new UtilityNetworkSource
            {
                ServiceId = "electric",
                NetworkId = "distribution",
                VersionName = "sde.DEFAULT",
                FeatureSource = new FeatureSource { ServiceId = "electric", LayerId = 0 },
            },
            NamedConfigurationId = "primary-upstream",
            StartingPoints =
            [
                new UtilityNetworkTraceStartingPoint
                {
                    Element = startingElement,
                    TerminalId = "load",
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["phase"] = JsonValue("\"A\""),
                    },
                },
            ],
            Barriers =
            [
                new UtilityNetworkTraceBarrier
                {
                    Element = barrierElement,
                    Kind = UtilityNetworkTraceBarrierKind.Filter,
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["reason"] = JsonValue("\"open-switch\""),
                    },
                },
            ],
            Configuration = new UtilityNetworkTraceConfiguration
            {
                TraceType = UtilityNetworkTraceType.Upstream,
                DomainNetwork = "ElectricDistribution",
                Tier = "MediumVoltage",
                ValidateConsistency = true,
                TraversabilityConditions =
                [
                    new UtilityNetworkTraceCondition
                    {
                        Name = "phase",
                        Operator = UtilityNetworkTraceConditionOperator.Equal,
                        Value = JsonValue("\"A\""),
                    },
                ],
                OutputNetworkAttributes = ["phase", "status"],
            },
            ReturnGeometry = true,
        };

        Assert.Equal("primary-upstream", request.NamedConfigurationId);
        Assert.Equal("load", request.StartingPoints[0].TerminalId);
        Assert.Equal(UtilityNetworkTraceBarrierKind.Filter, request.Barriers[0].Kind);
        var configuration = Assert.IsType<UtilityNetworkTraceConfiguration>(request.Configuration);
        Assert.Equal("MediumVoltage", configuration.Tier);
        Assert.True(configuration.ValidateConsistency);
        Assert.True(request.ReturnAssociations);
        Assert.True(request.ReturnGeometry);
    }

    [Fact]
    public async Task Interface_ModelsTraceWorkflowsAndResultData()
    {
        var client = new FakeUtilityNetworkTraceClient();
        var request = new UtilityNetworkTraceRequest
        {
            Source = new UtilityNetworkSource
            {
                ServiceId = "electric",
                NetworkId = "distribution",
            },
            NamedConfigurationId = "primary-upstream",
            StartingPoints =
            [
                new UtilityNetworkTraceStartingPoint
                {
                    Element = ElementReference("switch-1", terminalId: "load"),
                },
            ],
        };

        var configurations = await client.GetTraceConfigurationsAsync(new UtilityNetworkTraceConfigurationQuery
        {
            Source = request.Source,
            TraceType = UtilityNetworkTraceType.Upstream,
            DomainNetwork = "ElectricDistribution",
            Tier = "MediumVoltage",
        });
        var connected = await client.TraceConnectedAsync(request);
        var upstream = await client.TraceUpstreamAsync(request);
        var downstream = await client.TraceDownstreamAsync(request);
        var subnetwork = await client.TraceSubnetworkAsync(request);

        Assert.Equal("fake-utility-network", client.ProviderName);
        Assert.True(client.TraceCapabilities.SupportsNamedTraceConfigurations);
        Assert.True(client.TraceCapabilities.SupportsTerminals);
        Assert.Single(configurations);
        Assert.Equal("primary-upstream", configurations[0].ConfigurationId);
        Assert.Equal(UtilityNetworkTraceType.Connected, connected.TraceType);
        Assert.Equal(UtilityNetworkTraceType.Upstream, upstream.TraceType);
        Assert.Equal(UtilityNetworkTraceType.Downstream, downstream.TraceType);
        Assert.Equal(UtilityNetworkTraceType.Subnetwork, subnetwork.TraceType);
        Assert.True(upstream.Succeeded);
        Assert.Single(upstream.Elements);
        Assert.Single(upstream.Associations);
        Assert.Single(upstream.Terminals);
        Assert.Single(subnetwork.Subnetworks);
        Assert.Equal("ElectricDistribution", subnetwork.Subnetworks[0].DomainNetwork);
    }

    private static UtilityNetworkElementReference ElementReference(string elementId, string? terminalId = null)
        => new()
        {
            ElementId = elementId,
            NetworkSourceId = "devices",
            NetworkSourceName = "Electric Device",
            GlobalId = $"{elementId}-global",
            TerminalId = terminalId,
            FeatureSource = new FeatureSource { ServiceId = "electric", LayerId = 0 },
        };

    private static JsonElement JsonValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static UtilityNetworkTraceResult Result(UtilityNetworkTraceType traceType)
    {
        var from = ElementReference("switch-1", terminalId: "load");
        var to = ElementReference("transformer-1", terminalId: "source");

        return new UtilityNetworkTraceResult
        {
            TraceType = traceType,
            TraceId = $"trace-{traceType}",
            ConfigurationId = "primary-upstream",
            Succeeded = true,
            Elements =
            [
                new UtilityNetworkElement
                {
                    ElementId = from.ElementId,
                    Kind = UtilityNetworkElementKind.Device,
                    NetworkSourceId = from.NetworkSourceId,
                    NetworkSourceName = from.NetworkSourceName,
                    GlobalId = from.GlobalId,
                    TerminalId = from.TerminalId,
                    TerminalName = "Load",
                    FeatureSource = from.FeatureSource,
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["phase"] = JsonValue("\"A\""),
                    },
                },
            ],
            Associations =
            [
                new UtilityNetworkAssociation
                {
                    AssociationId = "assoc-1",
                    Kind = UtilityNetworkAssociationKind.Connectivity,
                    FromElement = from,
                    ToElement = to,
                },
            ],
            Terminals =
            [
                new UtilityNetworkTerminal
                {
                    TerminalId = "load",
                    Name = "Load",
                    Element = from,
                    IsDefault = true,
                },
            ],
            Subnetworks =
            [
                new UtilityNetworkSubnetworkResult
                {
                    SubnetworkName = "Feeder-1",
                    DomainNetwork = "ElectricDistribution",
                    Tier = "MediumVoltage",
                    Controllers = [to],
                },
            ],
            Messages =
            [
                new UtilityNetworkTraceMessage
                {
                    Message = "Trace completed.",
                },
            ],
        };
    }

    private sealed class FakeUtilityNetworkTraceClient : IHonuaUtilityNetworkTraceClient
    {
        public string ProviderName => "fake-utility-network";

        public UtilityNetworkTraceCapabilities TraceCapabilities { get; } = new()
        {
            SupportsConnectedTrace = true,
            SupportsUpstreamTrace = true,
            SupportsDownstreamTrace = true,
            SupportsSubnetworkTrace = true,
            SupportsNamedTraceConfigurations = true,
            SupportsTerminals = true,
            SupportsAssociations = true,
            SupportsBarriers = true,
            SupportsResultGeometry = true,
            NativeSurface = "test",
        };

        public Task<IReadOnlyList<UtilityNetworkNamedTraceConfiguration>> GetTraceConfigurationsAsync(
            UtilityNetworkTraceConfigurationQuery request,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UtilityNetworkNamedTraceConfiguration>>(
            [
                new UtilityNetworkNamedTraceConfiguration
                {
                    ConfigurationId = "primary-upstream",
                    Name = "Primary upstream",
                    TraceType = UtilityNetworkTraceType.Upstream,
                    IsDefault = true,
                    Configuration = new UtilityNetworkTraceConfiguration
                    {
                        TraceType = UtilityNetworkTraceType.Upstream,
                        DomainNetwork = request.DomainNetwork,
                        Tier = request.Tier,
                    },
                },
            ]);

        public Task<UtilityNetworkTraceResult> TraceConnectedAsync(
            UtilityNetworkTraceRequest request,
            CancellationToken ct = default)
            => Task.FromResult(Result(UtilityNetworkTraceType.Connected));

        public Task<UtilityNetworkTraceResult> TraceUpstreamAsync(
            UtilityNetworkTraceRequest request,
            CancellationToken ct = default)
            => Task.FromResult(Result(UtilityNetworkTraceType.Upstream));

        public Task<UtilityNetworkTraceResult> TraceDownstreamAsync(
            UtilityNetworkTraceRequest request,
            CancellationToken ct = default)
            => Task.FromResult(Result(UtilityNetworkTraceType.Downstream));

        public Task<UtilityNetworkTraceResult> TraceSubnetworkAsync(
            UtilityNetworkTraceRequest request,
            CancellationToken ct = default)
            => Task.FromResult(Result(UtilityNetworkTraceType.Subnetwork));
    }
}
