// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Packages;

/// <summary>Typed client for the Console Studio content lifecycle (<c>/api/v1/studio/*</c>): per-kind capability discovery, Draft CRUD, validate, preview-plan, Version save, release request, reopen, and rollback across every in-scope Studio content kind (Query, Map, Analysis, Dashboard, Report, Form, App, Workflow, GP, ETL). The kind discriminant travels on the content envelope, so one client serves every kind.</summary>
public interface IHonuaStudioPackageClient
{
    /// <summary>Reads the active immutable artifact served by a governed publication route.</summary>
    /// <param name="route">A governed route, with optional leading slash and multiple path segments; not a publication URL.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The publication identity, content hash and sealed package envelope.</returns>
    /// <remarks>HTTP errors, including inaccessible or missing publications, are propagated.</remarks>
    Task<StudioPublishedArtifact> GetPublishedArtifactAsync(string route, CancellationToken cancellationToken = default)
        => Task.FromException<StudioPublishedArtifact>(new NotSupportedException("This client does not support published artifact reads."));

    /// <summary>Lists Studio content kind capability descriptors for Console authoring.</summary>
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
#pragma warning disable CS0618 // The default delegates to the deprecated member so existing implementations keep working.
    Task<StudioContentItemPointers?> GetContentPointersAsync(Guid itemId, CancellationToken cancellationToken = default)
        => GetContentItemPointersAsync(itemId, cancellationToken);
#pragma warning restore CS0618

    /// <summary>Use <see cref="GetContentPointersAsync"/>.</summary>
    [Obsolete("Use GetContentPointersAsync. Removed in the next major release.")]
    Task<StudioContentItemPointers?> GetContentItemPointersAsync(Guid itemId, CancellationToken cancellationToken = default)
        => Task.FromException<StudioContentItemPointers?>(new NotSupportedException("This client does not support Studio content pointer reads."));

    /// <summary>Submits publication and distinguishes a persisted request from a pending approval operation.</summary>
    async Task<StudioPublicationSubmission> SubmitPublishRequestAsync(
        Guid itemId,
        Guid versionId,
        CreateStudioPublicationRequest request,
        CancellationToken cancellationToken = default)
        => StudioPublicationSubmission.FromPublication(
            await CreatePublishRequestAsync(itemId, versionId, request, cancellationToken).ConfigureAwait(false));

    /// <summary>Reopens an immutable content version as a new mutable draft.</summary>
    Task<StudioPackageDraft> ReopenVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default);

    /// <summary>Rolls a Studio content pointer back to an immutable Version.</summary>
    Task<StudioRollbackRequest> RollbackAsync(
        Guid itemId,
        CreateStudioRollbackRequest request,
        CancellationToken cancellationToken = default);
}
