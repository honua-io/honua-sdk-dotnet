// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Provider edit capabilities exposed through the shared feature edit abstraction.
/// </summary>
public sealed record FeatureEditCapabilities
{
    /// <summary>Whether the provider supports adding features.</summary>
    public bool SupportsAdds { get; init; }

    /// <summary>Whether the provider supports updating features.</summary>
    public bool SupportsUpdates { get; init; }

    /// <summary>Whether the provider supports deleting features.</summary>
    public bool SupportsDeletes { get; init; }

    /// <summary>Whether the provider supports rollback-on-failure edit batches.</summary>
    public bool SupportsRollbackOnFailure { get; init; }

    /// <summary>Native protocol surface used by the provider, when useful for diagnostics.</summary>
    public string? NativeSurface { get; init; }
}

/// <summary>
/// Provider-neutral edit request for add, update, and delete operations.
/// </summary>
public sealed record FeatureEditRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Features to add.</summary>
    public IReadOnlyList<FeatureEditFeature> Adds { get; init; } = [];

    /// <summary>Features to update. Providers that use object IDs require each update to include an ID.</summary>
    public IReadOnlyList<FeatureEditFeature> Updates { get; init; } = [];

    /// <summary>String feature identifiers to delete.</summary>
    public IReadOnlyList<string> DeleteIds { get; init; } = [];

    /// <summary>Numeric object identifiers to delete.</summary>
    public IReadOnlyList<long> DeleteObjectIds { get; init; } = [];

    /// <summary>Whether the provider should roll back all edits if any individual edit fails.</summary>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>Whether the provider should force writes that would otherwise be rejected by conflict detection.</summary>
    public bool ForceWrite { get; init; }
}

/// <summary>
/// Provider-neutral feature payload used by add and update edit operations.
/// </summary>
public sealed record FeatureEditFeature
{
    /// <summary>Provider feature identifier, when available.</summary>
    public string? Id { get; init; }

    /// <summary>Provider object identifier, when available.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Feature attributes/properties as JSON values.</summary>
    public IReadOnlyDictionary<string, JsonElement> Attributes { get; init; } =
        new Dictionary<string, JsonElement>();

    /// <summary>Feature geometry as a provider-native JSON object, when supplied.</summary>
    public JsonElement? Geometry { get; init; }
}

/// <summary>
/// Provider-neutral response from applying a batch of feature edits.
/// </summary>
public sealed record FeatureEditResponse
{
    /// <summary>Provider name that handled the edit request.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Results for add operations.</summary>
    public IReadOnlyList<FeatureEditResult> AddResults { get; init; } = [];

    /// <summary>Results for update operations.</summary>
    public IReadOnlyList<FeatureEditResult> UpdateResults { get; init; } = [];

    /// <summary>Results for delete operations.</summary>
    public IReadOnlyList<FeatureEditResult> DeleteResults { get; init; } = [];

    /// <summary>Top-level error when the provider rejects the whole edit batch.</summary>
    public FeatureEditError? Error { get; init; }

    /// <summary>Whether the whole edit batch and all reported item results succeeded.</summary>
    public bool Succeeded =>
        Error is null &&
        AddResults.All(result => result.Succeeded) &&
        UpdateResults.All(result => result.Succeeded) &&
        DeleteResults.All(result => result.Succeeded);
}

/// <summary>
/// Provider-neutral outcome of a single feature edit operation.
/// </summary>
public sealed record FeatureEditResult
{
    /// <summary>Provider feature identifier, when available.</summary>
    public string? Id { get; init; }

    /// <summary>Provider object identifier, when available.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Whether the individual edit operation succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Error details when the individual edit operation failed.</summary>
    public FeatureEditError? Error { get; init; }
}

/// <summary>
/// Provider-neutral edit error details.
/// </summary>
public sealed record FeatureEditError
{
    /// <summary>Provider-specific error code, when available.</summary>
    public int? Code { get; init; }

    /// <summary>Error message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Additional provider-specific error details, when available.</summary>
    public string? Details { get; init; }
}
