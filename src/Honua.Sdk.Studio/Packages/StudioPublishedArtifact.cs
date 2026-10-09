// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Packages;

/// <summary>The active immutable package resolved from a governed Studio publication route.</summary>
public sealed class StudioPublishedArtifact
{
    /// <summary>Publication route used to resolve the artifact.</summary>
    public required string Route { get; init; }

    /// <summary>Publication visibility (personal, team, organization or public).</summary>
    public string? Visibility { get; init; }

    /// <summary>Accepted publication request identifier.</summary>
    public required Guid PublicationId { get; init; }

    /// <summary>Content item identifier.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Active immutable version identifier.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>Active version number.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>Content item package key.</summary>
    public required string PackageKey { get; init; }

    /// <summary>Package family.</summary>
    public required StudioPackageFamily Family { get; init; }

    /// <summary>Active version's content hash.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Time when the governing publication request was accepted.</summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Active version's sealed package envelope.</summary>
    public required StudioPackageEnvelope Envelope { get; init; }
}
