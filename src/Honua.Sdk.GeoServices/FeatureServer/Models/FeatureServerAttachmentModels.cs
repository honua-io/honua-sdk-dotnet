// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// FeatureServer attachment metadata response.
/// </summary>
public sealed class FeatureServerAttachmentQueryResponse
{
    /// <summary>Attachments associated with the requested feature.</summary>
    [JsonPropertyName("attachmentInfos")]
    public IReadOnlyList<FeatureServerAttachmentInfo> AttachmentInfos { get; init; } = [];
}

/// <summary>
/// FeatureServer attachment metadata.
/// </summary>
public sealed class FeatureServerAttachmentInfo
{
    /// <summary>Provider attachment identifier.</summary>
    [JsonPropertyName("id")]
    public long? Id { get; init; }

    /// <summary>Parent feature object ID, when returned by the server.</summary>
    [JsonPropertyName("parentObjectId")]
    public long? ParentObjectId { get; init; }

    /// <summary>Provider global ID, when available.</summary>
    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    /// <summary>Attachment file name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Attachment content type.</summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    /// <summary>Attachment size in bytes.</summary>
    [JsonPropertyName("size")]
    public long? Size { get; init; }

    /// <summary>Provider keywords or tags.</summary>
    [JsonPropertyName("keywords")]
    public string? Keywords { get; init; }

    /// <summary>Provider URL for direct attachment access.</summary>
    [JsonPropertyName("url")]
    public Uri? Url { get; init; }
}

/// <summary>
/// FeatureServer attachment edit response.
/// </summary>
public sealed class FeatureServerAttachmentEditResponse
{
    /// <summary>Add attachment result.</summary>
    [JsonPropertyName("addAttachmentResult")]
    public FeatureServerAttachmentEditResult? AddAttachmentResult { get; init; }

    /// <summary>Update attachment result.</summary>
    [JsonPropertyName("updateAttachmentResult")]
    public FeatureServerAttachmentEditResult? UpdateAttachmentResult { get; init; }

    /// <summary>Delete attachment results.</summary>
    [JsonPropertyName("deleteAttachmentResults")]
    public IReadOnlyList<FeatureServerAttachmentEditResult>? DeleteAttachmentResults { get; init; }
}

/// <summary>
/// The outcome of a single FeatureServer attachment edit operation.
/// </summary>
public sealed class FeatureServerAttachmentEditResult
{
    /// <summary>Provider attachment identifier.</summary>
    [JsonPropertyName("objectId")]
    public long? ObjectId { get; init; }

    /// <summary>Provider global ID, when available.</summary>
    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    /// <summary>Whether the operation succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Error details when the operation failed.</summary>
    [JsonPropertyName("error")]
    public FeatureServerEditError? Error { get; init; }
}
