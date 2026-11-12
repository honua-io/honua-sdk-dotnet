// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio.Capabilities;
using Honua.Sdk.Studio.Packages;

namespace Honua.Sdk.Studio.Tests;

public sealed class RenamedVocabularyWireTests
{
    [Fact]
    public void SupportsContentKind_MatchesOnlySupportedKinds()
    {
        var manifest = new CapabilityManifest
        {
            Packages = new CapabilityManifestPackages
            {
                Families = [new CapabilityPackageFamily { Id = "map", Supported = true }],
            },
        };

        Assert.True(manifest.SupportsContentKind("map"));
        Assert.False(manifest.SupportsContentKind("app"));
    }

    [Fact]
    public void ResultPackageIds_KeepServerWireNames()
    {
        var package = new HonuaAnalysisResultPackage
        {
            ResultPackageId = "result-1",
            Status = HonuaGeoprocessingWorkflowStatus.Completed,
            Summary = new HonuaResultSummary { Title = "done" },
            Provenance = new HonuaProvenanceRecord { Sources = [], ProcessDefinitions = [] },
            MapId = "map-1",
            AppId = "app-1",
        };

        var json = JsonSerializer.Serialize(package, StudioJsonContext.Default.HonuaAnalysisResultPackage);
        Assert.Contains("\"mapPackageId\":\"map-1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"appPackageId\":\"app-1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"mapId\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"appId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MapEmbedUri_KeepsRequiredServerWireName()
    {
        var section = new HonuaMapEmbedSection { Caption = "Parcels", MapUri = "honua://maps/m1" };

        var json = JsonSerializer.Serialize(section, StudioJsonContext.Default.HonuaMapEmbedSection);
        Assert.Contains("\"mapPackageUri\":\"honua://maps/m1\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"mapUri\"", json, StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(
            "{\"caption\":\"Parcels\"}", StudioJsonContext.Default.HonuaMapEmbedSection));
    }

    [Fact]
    public async Task GetContentPointersAsync_DefaultWithoutAnImplementation_IsNotSupported()
    {
        IHonuaStudioPackageClient client = new MinimalClient();

        await Assert.ThrowsAsync<NotSupportedException>(() => client.GetContentPointersAsync(Guid.NewGuid()));
    }

    private sealed class MinimalClient : IHonuaStudioPackageClient
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
