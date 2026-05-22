// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Shared feature attachment contract implemented by attachment-capable Honua clients.
/// </summary>
public interface IHonuaFeatureAttachmentClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider attachment capabilities exposed by this client.
    /// </summary>
    FeatureAttachmentCapabilities AttachmentCapabilities { get; }

    /// <summary>
    /// Lists attachments for one feature.
    /// </summary>
    /// <param name="request">Provider-neutral attachment list request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attachment metadata.</returns>
    Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
        FeatureAttachmentListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads one attachment.
    /// </summary>
    /// <param name="request">Provider-neutral attachment download request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Downloaded attachment content. Dispose the returned content stream when finished.</returns>
    Task<FeatureAttachmentContent> DownloadAttachmentAsync(
        FeatureAttachmentDownloadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds an attachment to one feature.
    /// </summary>
    /// <param name="request">Provider-neutral attachment add request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attachment operation result.</returns>
    Task<FeatureAttachmentResult> AddAttachmentAsync(
        FeatureAttachmentAddRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates one attachment.
    /// </summary>
    /// <param name="request">Provider-neutral attachment update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attachment operation result.</returns>
    Task<FeatureAttachmentResult> UpdateAttachmentAsync(
        FeatureAttachmentUpdateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes one attachment.
    /// </summary>
    /// <param name="request">Provider-neutral attachment delete request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Attachment operation result.</returns>
    Task<FeatureAttachmentResult> DeleteAttachmentAsync(
        FeatureAttachmentDeleteRequest request,
        CancellationToken cancellationToken = default);
}
