// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Packages;

/// <summary>Request body for creating a mutable Studio package draft.</summary>
public sealed record CreateStudioPackageDraftRequest
{
    /// <summary>Optional existing content item id; omit to create a new item.</summary>
    public Guid? ItemId { get; init; }

    /// <summary>Machine-friendly package key.</summary>
    public required string PackageKey { get; init; }

    /// <summary>Workspace identifier.</summary>
    public string? WorkspaceId { get; init; }

    /// <summary>Owner principal identifier.</summary>
    public string? OwnerId { get; init; }

    /// <summary>Package envelope.</summary>
    public required StudioPackageEnvelope Envelope { get; init; }
}

/// <summary>Request body for updating a mutable Studio package draft.</summary>
public sealed record UpdateStudioPackageDraftRequest
{
    /// <summary>Machine-friendly package key.</summary>
    public required string PackageKey { get; init; }

    /// <summary>Workspace identifier.</summary>
    public string? WorkspaceId { get; init; }

    /// <summary>Owner principal identifier.</summary>
    public string? OwnerId { get; init; }

    /// <summary>Package envelope.</summary>
    public required StudioPackageEnvelope Envelope { get; init; }

    /// <summary>Expected draft generation for optimistic concurrency.</summary>
    public long? Generation { get; init; }
}

/// <summary>Request body for saving a draft as an immutable content version.</summary>
public sealed record SaveStudioContentVersionRequest
{
    /// <summary>Optional author change note.</summary>
    public string? ChangeNote { get; init; }
}

/// <summary>Request body for comparing two immutable content versions.</summary>
public sealed record CompareStudioContentVersionsRequest
{
    /// <summary>Left-side version identifier.</summary>
    public required Guid LeftVersionId { get; init; }

    /// <summary>Right-side version identifier.</summary>
    public required Guid RightVersionId { get; init; }
}

/// <summary>Request body for creating a publication request.</summary>
public sealed record CreateStudioPublicationRequest
{
    /// <summary>Optional publication intent override.</summary>
    public StudioPublicationIntent? Intent { get; init; }

    /// <summary>Optional acknowledgement for validation warnings.</summary>
    public string? WarningAcknowledgement { get; init; }
}

/// <summary>Request body for rolling a content item pointer back to an immutable version.</summary>
public sealed record CreateStudioRollbackRequest
{
    /// <summary>Version identifier selected as the rollback target.</summary>
    public required Guid TargetVersionId { get; init; }

    /// <summary>Pointer to update.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("pointer")]
    public StudioRollbackPointer Target { get; init; } = StudioRollbackPointer.Current;

    /// <summary>Optional reason supplied by the actor.</summary>
    public string? Reason { get; init; }
}
