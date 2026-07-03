// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Internal;

namespace Honua.Sdk.Studio.Packages;

/// <summary>
/// HTTP client for the Console Studio package lifecycle endpoints.
/// </summary>
public sealed class HonuaStudioPackageClient : IHonuaStudioPackageClient
{
    private const string BasePath = "/api/v1/studio";
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioPackageClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaStudioPackageClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public Task<StudioPackageFamilyCapabilities> GetPackageFamiliesAsync(CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/package-families",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioPackageFamilyCapabilities,
            "GetPackageFamilies",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioPackageDraft> CreateDraftAsync(
        CreateStudioPackageDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"{BasePath}/package-drafts",
            JsonBody(request, StudioPackageJsonContext.Default.CreateStudioPackageDraftRequest),
            StudioPackageJsonContext.Default.StudioApiResponseStudioPackageDraft,
            "CreateDraft",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<StudioPackageDraft> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/package-drafts/{draftId}",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioPackageDraft,
            "GetDraft",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioPackageDraft> UpdateDraftAsync(
        Guid draftId,
        UpdateStudioPackageDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Put,
            $"{BasePath}/package-drafts/{draftId}",
            JsonBody(request, StudioPackageJsonContext.Default.UpdateStudioPackageDraftRequest),
            StudioPackageJsonContext.Default.StudioApiResponseStudioPackageDraft,
            "UpdateDraft",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteDraftAsync(Guid draftId, CancellationToken cancellationToken = default)
    {
        using var response = await _http
            .DeleteAsync(new Uri($"{BasePath}/package-drafts/{draftId}", UriKind.RelativeOrAbsolute), cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw StudioHttpResponseReader.CreateApiException(response.StatusCode, body);
        }
    }

    /// <inheritdoc />
    public Task<StudioValidationSummary> ValidateDraftAsync(Guid draftId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post,
            $"{BasePath}/package-drafts/{draftId}/validate",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioValidationSummary,
            "ValidateDraft",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioPreviewPlan> PreviewPlanAsync(Guid draftId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post,
            $"{BasePath}/package-drafts/{draftId}/preview-plan",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioPreviewPlan,
            "PreviewPlan",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioContentVersion> CreateContentVersionAsync(
        Guid draftId,
        SaveStudioContentVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"{BasePath}/package-drafts/{draftId}/content-versions",
            JsonBody(request, StudioPackageJsonContext.Default.SaveStudioContentVersionRequest),
            StudioPackageJsonContext.Default.StudioApiResponseStudioContentVersion,
            "CreateContentVersion",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<StudioContentVersionList> ListVersionsAsync(Guid itemId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/content-items/{itemId}/versions",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioContentVersionList,
            "ListVersions",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioContentVersion> GetVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/content-items/{itemId}/versions/{versionId}",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioContentVersion,
            "GetVersion",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioVersionComparison> CompareVersionsAsync(
        Guid itemId,
        CompareStudioContentVersionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"{BasePath}/content-items/{itemId}/version-comparisons",
            JsonBody(request, StudioPackageJsonContext.Default.CompareStudioContentVersionsRequest),
            StudioPackageJsonContext.Default.StudioApiResponseStudioVersionComparison,
            "CompareVersions",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<StudioPublicationRequest> CreatePublishRequestAsync(
        Guid itemId,
        Guid versionId,
        CreateStudioPublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"{BasePath}/content-items/{itemId}/versions/{versionId}/publish-requests",
            JsonBody(request, StudioPackageJsonContext.Default.CreateStudioPublicationRequest),
            StudioPackageJsonContext.Default.StudioApiResponseStudioPublicationRequest,
            "CreatePublishRequest",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<StudioPackageDraft> ReopenVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post,
            $"{BasePath}/content-items/{itemId}/versions/{versionId}/reopen",
            content: null,
            StudioPackageJsonContext.Default.StudioApiResponseStudioPackageDraft,
            "ReopenVersion",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioRollbackRequest> RollbackAsync(
        Guid itemId,
        CreateStudioRollbackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"{BasePath}/content-items/{itemId}/rollback-requests",
            JsonBody(request, StudioPackageJsonContext.Default.CreateStudioRollbackRequest),
            StudioPackageJsonContext.Default.StudioApiResponseStudioRollbackRequest,
            "Rollback",
            cancellationToken);
    }

    private async Task<TData> SendAsync<TData>(
        HttpMethod method,
        string path,
        HttpContent? content,
        JsonTypeInfo<StudioApiResponse<TData>> typeInfo,
        string operation,
        CancellationToken cancellationToken)
        where TData : class
    {
        using var message = new HttpRequestMessage(method, new Uri(path, UriKind.RelativeOrAbsolute));
        if (content is not null)
        {
            message.Content = content;
        }

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var envelope = await StudioHttpResponseReader
            .ReadAsync(response, typeInfo, operation, cancellationToken)
            .ConfigureAwait(false);

        return envelope.Data
            ?? throw new HonuaStudioContractException(
                $"Server returned a Studio '{operation}' response without a data payload.",
                operation);
    }

    private static StringContent JsonBody<T>(T value, JsonTypeInfo<T> typeInfo)
        => new(JsonSerializer.Serialize(value, typeInfo), Encoding.UTF8, "application/json");
}
