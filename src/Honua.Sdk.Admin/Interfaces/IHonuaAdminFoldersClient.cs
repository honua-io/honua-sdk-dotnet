// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>Manages catalog Folders and logical Service membership.</summary>
public interface IHonuaAdminFoldersClient
{
    /// <summary>Lists Folders and returns their catalog snapshot ETag.</summary>
    Task<(IReadOnlyList<Folder> Folders, string? ETag)> ListFoldersAsync(string? namespaceName = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use HonuaAdminClient for Folder management.");
    /// <summary>Creates a Folder against the reviewed snapshot.</summary>
    Task<(Folder Folder, string? ETag)> CreateFolderAsync(CreateFolderRequest request, FolderMutationOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use HonuaAdminClient for Folder management.");
    /// <summary>Updates a Folder against the reviewed snapshot.</summary>
    Task<(Folder Folder, string? ETag)> UpdateFolderAsync(string folderId, UpdateFolderRequest request, FolderMutationOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use HonuaAdminClient for Folder management.");
    /// <summary>Deletes an empty Folder and returns the resulting snapshot ETag.</summary>
    Task<string?> DeleteFolderAsync(string folderId, FolderMutationOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use HonuaAdminClient for Folder management.");
    /// <summary>Moves a logical Service to a Folder or Root against the reviewed snapshot.</summary>
    Task<string?> MoveServiceAsync(string serviceName, MoveServiceRequest request, FolderMutationOptions options, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Use HonuaAdminClient for Folder management.");
}
