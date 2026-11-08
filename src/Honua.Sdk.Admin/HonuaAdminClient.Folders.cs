// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

public sealed partial class HonuaAdminClient
{
    /// <summary>Lists Folders and returns their catalog snapshot ETag.</summary>
    public async Task<(IReadOnlyList<Folder> Folders, string? ETag)> ListFoldersAsync(string? namespaceName = null, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, FolderUri($"{ApiPrefix}/folders", namespaceName));
        var (folders, etag) = await ReadFolderResponseAsync(request, HonuaAdminJsonContext.Default.ApiResponseFolderArray, cancellationToken).ConfigureAwait(false);
        return (folders, etag);
    }

    /// <summary>Creates a Folder against the reviewed snapshot.</summary>
    public async Task<(Folder Folder, string? ETag)> CreateFolderAsync(CreateFolderRequest request, FolderMutationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = JsonContent.Create(request, HonuaAdminJsonContext.Default.CreateFolderRequest);
        using var message = CreateFolderMutationRequest(HttpMethod.Post, $"{ApiPrefix}/folders", options, content);
        return await ReadFolderResponseAsync(message, HonuaAdminJsonContext.Default.ApiResponseFolder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates a Folder against the reviewed snapshot.</summary>
    public async Task<(Folder Folder, string? ETag)> UpdateFolderAsync(string folderId, UpdateFolderRequest request, FolderMutationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = JsonContent.Create(request, HonuaAdminJsonContext.Default.UpdateFolderRequest);
        using var message = CreateFolderMutationRequest(HttpMethod.Patch, $"{ApiPrefix}/folders/{HonuaPathSegment.Escape(folderId, nameof(folderId))}", options, content);
        return await ReadFolderResponseAsync(message, HonuaAdminJsonContext.Default.ApiResponseFolder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes an empty Folder and returns the resulting snapshot ETag.</summary>
    public async Task<string?> DeleteFolderAsync(string folderId, FolderMutationOptions options, CancellationToken cancellationToken = default)
    {
        using var message = CreateFolderMutationRequest(HttpMethod.Delete, $"{ApiPrefix}/folders/{HonuaPathSegment.Escape(folderId, nameof(folderId))}", options);
        return (await SendWithETagAsync(message, cancellationToken).ConfigureAwait(false)).ETag;
    }

    /// <summary>Moves a logical Service to a Folder or Root against the reviewed snapshot.</summary>
    public async Task<string?> MoveServiceAsync(string serviceName, MoveServiceRequest request, FolderMutationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var content = JsonContent.Create(request, HonuaAdminJsonContext.Default.MoveServiceRequest);
        using var message = CreateFolderMutationRequest(HttpMethod.Put, $"{ApiPrefix}/services/{HonuaPathSegment.Escape(serviceName, nameof(serviceName))}/folder", options, content);
        return (await SendWithETagAsync(message, cancellationToken).ConfigureAwait(false)).ETag;
    }

    private HttpRequestMessage CreateFolderMutationRequest(HttpMethod method, string path, FolderMutationOptions options, HttpContent? content = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.IfMatch);
        var message = new HttpRequestMessage(method, FolderUri(path, options.Namespace)) { Content = content };
        message.Headers.TryAddWithoutValidation("If-Match", options.IfMatch);
        return message;
    }

    private Uri FolderUri(string path, string? namespaceName)
        => CreateRequestUri(namespaceName is null ? path : path + "?namespace=" + Uri.EscapeDataString(namespaceName));

    private async Task<(T Value, string? ETag)> ReadFolderResponseAsync<T>(
        HttpRequestMessage request, JsonTypeInfo<ApiResponse<T>> typeInfo, CancellationToken cancellationToken) where T : class
    {
        var (body, etag) = await SendWithETagAsync(request, cancellationToken).ConfigureAwait(false);
        var value = JsonSerializer.Deserialize(body, typeInfo)?.Data
            ?? throw new HonuaAdminOperationException("Server returned no Folder catalog value.", "FolderCatalog");
        return (value, etag);
    }
}
