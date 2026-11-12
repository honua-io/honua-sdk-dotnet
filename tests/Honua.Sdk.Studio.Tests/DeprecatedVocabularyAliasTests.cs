// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio.Capabilities;

namespace Honua.Sdk.Studio.Tests;

#pragma warning disable CS0618 // These tests pin the deprecated aliases to their replacements.
public sealed class DeprecatedVocabularyAliasTests
{
    [Fact]
    public void HasPackageFamily_ForwardsToSupportsContentKind()
    {
        var manifest = new CapabilityManifest
        {
            Packages = new CapabilityManifestPackages
            {
                Families = [new CapabilityPackageFamily { Id = "map", Supported = true }],
            },
        };

        Assert.True(manifest.SupportsContentKind("map"));
        Assert.True(manifest.HasPackageFamily("map"));
        Assert.False(manifest.HasPackageFamily("app"));
    }

    [Fact]
    public void ResultPackageAliases_ShareStorageAndKeepWireNames()
    {
        var package = new HonuaAnalysisResultPackage
        {
            ResultPackageId = "result-1",
            Status = HonuaGeoprocessingWorkflowStatus.Completed,
            Summary = new HonuaResultSummary { Title = "done" },
            Provenance = new HonuaProvenanceRecord { Sources = [], ProcessDefinitions = [] },
            MapPackageId = "map-1",
            AppPackageId = "app-1",
        };

        Assert.Equal("map-1", package.MapId);
        Assert.Equal("app-1", package.AppId);

        var json = JsonSerializer.Serialize(package, StudioJsonContext.Default.HonuaAnalysisResultPackage);
        Assert.Single(json.Split("\"mapPackageId\":\"map-1\"")[1..]);
        Assert.Single(json.Split("\"appPackageId\":\"app-1\"")[1..]);
        Assert.DoesNotContain("\"mapId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MapEmbedAlias_SharesStorageAndKeepsRequiredWireName()
    {
        var section = new HonuaMapEmbedSection { Caption = "Parcels", MapPackageUri = "honua://map-packages/m1" };
        Assert.Equal("honua://map-packages/m1", section.MapUri);

        var json = JsonSerializer.Serialize(section, StudioJsonContext.Default.HonuaMapEmbedSection);
        Assert.Single(json.Split("\"mapPackageUri\":")[1..]);
        Assert.DoesNotContain("\"mapUri\"", json, StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            "{\"caption\":\"Parcels\"}", StudioJsonContext.Default.HonuaMapEmbedSection));
    }
}
#pragma warning restore CS0618
