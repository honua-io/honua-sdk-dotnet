// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>A catalog Folder with its stable identity and historical names.</summary>
public sealed class Folder
{
    /// <summary>The Folder identity and display metadata.</summary>
    [JsonPropertyName("metadata")]
    public FolderMetadata Metadata { get; init; } = new();
    /// <summary>Historical public names retained for saved connections.</summary>
    [JsonPropertyName("previousNames")]
    public IReadOnlyList<string> PreviousNames { get; init; } = [];
    /// <summary>Whether the Folder is retired.</summary>
    [JsonPropertyName("deleted")]
    public bool Deleted { get; init; }
}

/// <summary>Identity and display metadata for a catalog Folder.</summary>
public sealed class FolderMetadata
{
    /// <summary>The immutable Folder identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
    /// <summary>The single-segment public Folder name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
    /// <summary>Optional display text independent of the public name.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
    /// <summary>The owning tenant when the server supplies one.</summary>
    [JsonPropertyName("tenant")]
    public string? Tenant { get; init; }
}

/// <summary>The name and optional title for a new Folder.</summary>
public sealed class CreateFolderRequest
{
    /// <summary>The single-segment public Folder name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    /// <summary>Optional display text.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>Changes a Folder name or title while preserving omitted fields.</summary>
public sealed class UpdateFolderRequest
{
    /// <summary>A new single-segment name, or null to preserve the name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    /// <summary>An explicit null clears the title; the default value preserves it.</summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public OptionalString Title { get; init; }
}

/// <summary>The destination Folder for a logical Service.</summary>
public sealed class MoveServiceRequest
{
    /// <summary>The destination Folder identifier, or explicit null for Root.</summary>
    [JsonPropertyName("folderId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public required string? FolderId { get; init; }
}

/// <summary>Catalog concurrency and optional namespace scope for a Folder mutation.</summary>
public sealed class FolderMutationOptions
{
    /// <summary>The catalog snapshot ETag the caller reviewed.</summary>
    public required string IfMatch { get; init; }
    /// <summary>The supplied namespace; null leaves the operation unscoped.</summary>
    public string? Namespace { get; init; }
}
