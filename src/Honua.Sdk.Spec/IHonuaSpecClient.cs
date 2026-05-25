// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Spec.Models;

namespace Honua.Sdk.Spec;

/// <summary>
/// Client interface for the Honua spec workspace REST API.
/// </summary>
public interface IHonuaSpecClient
{
    /// <summary>
    /// Validates spec DSL text or a canonical JSON spec document.
    /// </summary>
    /// <param name="request">Validation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation diagnostics and optional canonical JSON.</returns>
    Task<SpecValidateResponse> ValidateAsync(SpecValidateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compiles a canonical spec document into a plan.
    /// </summary>
    /// <param name="request">Spec document request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Plan response.</returns>
    Task<SpecPlanResponse> PlanAsync(SpecDocumentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an apply run and streams apply events.
    /// </summary>
    /// <param name="request">Spec document request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Disposable apply token and event stream.</returns>
    Task<SpecApplyStream> ApplyAsync(SpecDocumentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an in-flight apply run by apply token.
    /// </summary>
    /// <param name="applyToken">Opaque apply token returned by <see cref="ApplyAsync"/> or the server header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cancellation response.</returns>
    Task<SpecCancelResponse> CancelAsync(string applyToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a cached artifact by content hash.
    /// </summary>
    /// <param name="hash">Content hash (cache key) of the artifact.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The artifact bytes, content type, and echoed content hash.</returns>
    Task<HonuaSpecArtifact> GetArtifactAsync(string hash, CancellationToken cancellationToken = default);
}
