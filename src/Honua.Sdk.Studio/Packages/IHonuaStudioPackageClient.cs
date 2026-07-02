// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Packages;

/// <summary>
/// Typed client for the Console Studio package lifecycle
/// (<c>/api/v1/studio/*</c>): family capability discovery, draft CRUD, validate,
/// preview-plan, content-version, publish-request, reopen, and rollback across
/// every in-scope package family (query, map, analysis, dashboard, report, form,
/// app, workflow, gp, etl). The family discriminant travels on the package
/// envelope, so one client serves every family.
/// </summary>
public interface IHonuaStudioPackageClient
{
    /// <summary>Lists Studio package family capability descriptors for Console authoring.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPackageFamilyCapabilities> GetPackageFamiliesAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a mutable Studio package draft.</summary>
    /// <param name="request">Draft creation request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPackageDraft> CreateDraftAsync(
        CreateStudioPackageDraftRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a mutable Studio package draft.</summary>
    /// <param name="draftId">Draft identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPackageDraft> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Replaces a mutable Studio package draft using optimistic generation checks.</summary>
    /// <param name="draftId">Draft identifier.</param>
    /// <param name="request">Draft update request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPackageDraft> UpdateDraftAsync(
        Guid draftId,
        UpdateStudioPackageDraftRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a mutable Studio package draft.</summary>
    /// <param name="draftId">Draft identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Validates a mutable Studio package draft.</summary>
    /// <param name="draftId">Draft identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioValidationSummary> ValidateDraftAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Creates a preview plan for a mutable Studio package draft.</summary>
    /// <param name="draftId">Draft identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPreviewPlan> PreviewPlanAsync(Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>Saves a draft as an immutable content version.</summary>
    /// <param name="draftId">Draft identifier.</param>
    /// <param name="request">Content-version save request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioContentVersion> CreateContentVersionAsync(
        Guid draftId,
        SaveStudioContentVersionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists immutable content versions for a content item.</summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioContentVersionList> ListVersionsAsync(Guid itemId, CancellationToken cancellationToken = default);

    /// <summary>Gets a single immutable content version.</summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="versionId">Version identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioContentVersion> GetVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    /// <summary>Compares two immutable content versions.</summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="request">Comparison request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioVersionComparison> CompareVersionsAsync(
        Guid itemId,
        CompareStudioContentVersionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a publication request for an immutable content version.</summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="versionId">Version identifier.</param>
    /// <param name="request">Publication request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPublicationRequest> CreatePublishRequestAsync(
        Guid itemId,
        Guid versionId,
        CreateStudioPublicationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Reopens an immutable content version as a new mutable draft.</summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="versionId">Version identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioPackageDraft> ReopenVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    /// <summary>Rolls a content item pointer back to an immutable version.</summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="request">Rollback request.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<StudioRollbackRequest> RollbackAsync(
        Guid itemId,
        CreateStudioRollbackRequest request,
        CancellationToken cancellationToken = default);
}
