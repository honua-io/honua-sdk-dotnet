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

    /// <summary>
    /// Base64-encoded canonical bytes hashed by the server to produce <see cref="ContentHash"/>.
    /// Omitted by older servers or when canonical bytes cannot be supplied for a legacy version.
    /// Use <see cref="VerifyContentHash"/> without reproducing the server's envelope serialization.
    /// </summary>
    public string? ContentHashInput { get; init; }

    /// <summary>Time when the governing publication request was accepted.</summary>
    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Active version's sealed package envelope.</summary>
    public required StudioPackageEnvelope Envelope { get; init; }

    /// <summary>Verifies the content hash against the server's canonical base64-encoded input bytes.</summary>
    /// <returns>
    /// <see langword="null"/> when the input is absent or empty;
    /// <see langword="true"/> when its lower-case hexadecimal SHA-256 matches <see cref="ContentHash"/>;
    /// <see langword="false"/> when it differs or the input is not valid base64.
    /// </returns>
    public bool? VerifyContentHash() => StudioContentHash.Verify(ContentHashInput, ContentHash);
}
