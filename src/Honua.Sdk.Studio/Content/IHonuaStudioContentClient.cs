// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Content;

/// <summary>Manages Studio content capabilities, drafts, versions, and publications across content kinds.</summary>
public interface IHonuaStudioContentClient
{
    /// <summary>Reads the active immutable artifact served by a governed publication route.</summary>
    Task<StudioPublishedArtifact> GetPublishedArtifactAsync(string route, CancellationToken cancellationToken = default)
        => Task.FromException<StudioPublishedArtifact>(new NotSupportedException("This client does not support published artifact reads."));

    /// <summary>Lists Studio content kind capability descriptors for Console authoring.</summary>
    Task<StudioContentKindCapabilities> GetContentKindsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a mutable Studio content draft.</summary>
    Task<StudioContentDraft> CreateDraftAsync(
        CreateStudioContentDraftRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a mutable Studio content draft.</summary>
    Task<StudioContentDraft> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a mutable Studio content draft using optimistic generation checks.</summary>
    Task<StudioContentDraft> UpdateDraftAsync(
        Guid draftId,
        UpdateStudioContentDraftRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a mutable Studio content draft.</summary>
    Task DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Validates a mutable Studio content draft.</summary>
    Task<StudioValidationSummary> ValidateDraftAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Creates a preview plan for a mutable Studio content draft.</summary>
    Task<StudioPreviewPlan> PreviewPlanAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Saves a draft as an immutable content version.</summary>
    Task<StudioContentVersion> CreateContentVersionAsync(
        Guid draftId,
        SaveStudioContentVersionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists immutable Versions of Studio content.</summary>
    Task<StudioContentVersionList> ListVersionsAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Gets a single immutable content version.</summary>
    Task<StudioContentVersion> GetVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    /// <summary>Compares two immutable content versions.</summary>
    Task<StudioVersionComparison> CompareVersionsAsync(
        Guid itemId,
        CompareStudioContentVersionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a publication request for an immutable content version.</summary>
    Task<StudioPublicationRequest> CreatePublishRequestAsync(
        Guid itemId,
        Guid versionId,
        CreateStudioPublicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Reads exact current and released Version pointers for visible Studio content, or null only when a complete visible listing omits it.</summary>
    Task<StudioContentPointers?> GetContentPointersAsync(Guid itemId, CancellationToken cancellationToken = default)
        => Task.FromException<StudioContentPointers?>(new NotSupportedException("This client does not support Studio content pointer reads."));

    /// <summary>Submits publication and distinguishes a persisted request from a pending approval operation.</summary>
    async Task<StudioPublicationSubmission> SubmitPublishRequestAsync(
        Guid itemId,
        Guid versionId,
        CreateStudioPublicationRequest request,
        CancellationToken cancellationToken = default)
        => StudioPublicationSubmission.FromPublication(
            await CreatePublishRequestAsync(itemId, versionId, request, cancellationToken).ConfigureAwait(false));

    /// <summary>Reopens an immutable content version as a new mutable draft.</summary>
    Task<StudioContentDraft> ReopenVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    /// <summary>Rolls a Studio content pointer back to an immutable Version.</summary>
    Task<StudioRollbackRequest> RollbackAsync(
        Guid itemId,
        CreateStudioRollbackRequest request,
        CancellationToken cancellationToken = default);
}
