// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio.Capabilities;
using Honua.Sdk.Studio.Packages;

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
    [Fact]
    public async Task GetContentPointersAsync_DefaultDelegatesToAnImplementationOfTheDeprecatedMember()
    {
        var expected = new StudioContentItemPointers { ItemId = Guid.NewGuid() };
        IHonuaStudioPackageClient client = new LegacyPointerClient(expected);

        Assert.Same(expected, await client.GetContentPointersAsync(expected.ItemId));
    }

    [Fact]
    public async Task GetContentPointersAsync_DefaultWithoutAnyImplementation_IsNotSupported()
    {
        IHonuaStudioPackageClient client = new MinimalClient();

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetContentPointersAsync(Guid.NewGuid()));
    }

    private sealed class LegacyPointerClient(StudioContentItemPointers pointers) : MinimalClient, IHonuaStudioPackageClient
    {
        public Task<StudioContentItemPointers?> GetContentItemPointersAsync(Guid itemId, CancellationToken cancellationToken = default)
            => Task.FromResult<StudioContentItemPointers?>(pointers);
    }

    private class MinimalClient : IHonuaStudioPackageClient
    {
        public Task<StudioPackageFamilyCapabilities> GetPackageFamiliesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioPackageDraft> CreateDraftAsync(CreateStudioPackageDraftRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioPackageDraft> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioPackageDraft> UpdateDraftAsync(Guid draftId, UpdateStudioPackageDraftRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioValidationSummary> ValidateDraftAsync(Guid draftId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioPreviewPlan> PreviewPlanAsync(Guid draftId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioContentVersion> CreateContentVersionAsync(Guid draftId, SaveStudioContentVersionRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioContentVersionList> ListVersionsAsync(Guid itemId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioContentVersion> GetVersionAsync(Guid itemId, Guid versionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioVersionComparison> CompareVersionsAsync(Guid itemId, CompareStudioContentVersionsRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioPublicationRequest> CreatePublishRequestAsync(Guid itemId, Guid versionId, CreateStudioPublicationRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioPackageDraft> ReopenVersionAsync(Guid itemId, Guid versionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<StudioRollbackRequest> RollbackAsync(Guid itemId, CreateStudioRollbackRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
#pragma warning restore CS0618
