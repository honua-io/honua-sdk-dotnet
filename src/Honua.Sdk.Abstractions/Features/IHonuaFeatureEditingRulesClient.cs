// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Optional advanced editing-rule contract for providers that expose validation metadata and edit sessions.
/// </summary>
public interface IHonuaFeatureEditingRulesClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider edit capabilities exposed by this client.
    /// </summary>
    FeatureEditCapabilities EditCapabilities { get; }

    /// <summary>
    /// Gets structured editing-rule metadata for a source.
    /// </summary>
    /// <param name="request">Editing-rule metadata request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Editing-rule metadata advertised by the provider.</returns>
    Task<FeatureEditingRulesMetadata> GetEditingRulesAsync(
        FeatureEditingRulesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates edits without necessarily committing them.
    /// </summary>
    /// <param name="request">Validation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation findings from client-safe rules or the provider.</returns>
    Task<FeatureEditValidationResponse> ValidateEditsAsync(
        FeatureEditValidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a branch or version-aware edit session.
    /// </summary>
    /// <param name="request">Edit session start request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Edit session context for subsequent validation or apply requests.</returns>
    Task<FeatureEditSession> StartEditSessionAsync(
        FeatureEditSessionStartRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits a branch or version-aware edit session.
    /// </summary>
    /// <param name="request">Edit session completion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the provider commits the session.</returns>
    Task CommitEditSessionAsync(
        FeatureEditSessionCompleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a branch or version-aware edit session.
    /// </summary>
    /// <param name="request">Edit session completion request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the provider rolls back the session.</returns>
    Task RollbackEditSessionAsync(
        FeatureEditSessionCompleteRequest request,
        CancellationToken cancellationToken = default);
}
