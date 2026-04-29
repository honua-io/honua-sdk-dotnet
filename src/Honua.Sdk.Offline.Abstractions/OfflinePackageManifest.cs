// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>
/// Serializable manifest for a local offline package.
/// </summary>
public sealed record OfflinePackageManifest
{
    /// <summary>Stable package identifier owned by the application.</summary>
    public required string PackageId { get; init; }

    /// <summary>Human-readable package name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Version or revision of the manifest format or package definition.</summary>
    public string? Version { get; init; }

    /// <summary>Time when the manifest was produced.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Feature sources included in the offline package.</summary>
    public IReadOnlyList<OfflineSourceDescriptor> Sources { get; init; } = [];

    /// <summary>Application metadata that should travel with the manifest.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Offline download scope for a single protocol-backed feature source.
/// </summary>
public sealed record OfflineSourceDescriptor
{
    /// <summary>Application-level source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>SDK source descriptor that identifies the provider and locator.</summary>
    public required SourceDescriptor Source { get; init; }

    /// <summary>Provider filter expression used to bound the offline source.</summary>
    public string? Where { get; init; }

    /// <summary>Filter expression language used by <see cref="Where"/>.</summary>
    public FeatureFilterLanguage FilterLanguage { get; init; } = FeatureFilterLanguage.ProviderDefault;

    /// <summary>Optional spatial extent used to bound the offline source.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Fields to include in locally cached feature records.</summary>
    public IReadOnlyList<string> OutFields { get; init; } = [];

    /// <summary>Whether geometry should be included in pulled feature records.</summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>Maximum number of features to pull for this source.</summary>
    public int? MaxFeatureCount { get; init; }

    /// <summary>Preferred provider page size for download planning.</summary>
    public int? PageSize { get; init; }

    /// <summary>Provider sync token from the previous successful pull.</summary>
    public string? LastSyncToken { get; init; }

    /// <summary>Creates a provider-neutral query request for the source.</summary>
    /// <param name="lastSyncToken">Last sync token selected for this pull.</param>
    /// <returns>A bounded feature query request.</returns>
    public FeatureQueryRequest ToQueryRequest(string? lastSyncToken = null)
    {
        _ = lastSyncToken;
        return new FeatureQueryRequest
        {
            Source = Source.ToFeatureSource(),
            Filter = Where,
            FilterLanguage = FilterLanguage,
            OutFields = OutFields.Count == 0 ? null : OutFields,
            ReturnGeometry = ReturnGeometry,
            Limit = CalculateInitialLimit(),
            Bbox = Extent,
        };
    }

    private int? CalculateInitialLimit()
    {
        if (PageSize is null)
        {
            return MaxFeatureCount;
        }

        return MaxFeatureCount is null ? PageSize.Value : Math.Min(PageSize.Value, MaxFeatureCount.Value);
    }
}
