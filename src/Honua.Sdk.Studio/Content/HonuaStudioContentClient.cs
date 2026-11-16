// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Internal;

namespace Honua.Sdk.Studio.Content;

/// <summary>HTTP client for the Console Studio content lifecycle endpoints.</summary>
public sealed class HonuaStudioContentClient : IHonuaStudioContentClient
{
    private const string BasePath = "/api/v1/studio";
    private readonly HttpClient _http;

    /// <summary>Initializes a new instance of the <see cref="HonuaStudioContentClient"/> class.</summary>
    public HonuaStudioContentClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<StudioPublishedArtifact> GetPublishedArtifactAsync(
        string route,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        var routeKey = route[0] == '/' ? route : "/" + route;
        var segments = routeKey[1..].Split('/');
        if (route.Contains('\\', StringComparison.Ordinal)
            || (!route.AsSpan().StartsWith("/", StringComparison.Ordinal) && Uri.TryCreate(route, UriKind.Absolute, out _))
            || segments.Any(static segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Supply a publication route key without traversal segments, not a URL.", nameof(route));
        }

        var path = $"{BasePath}/published/" + string.Join('/', segments.Select(Uri.EscapeDataString));
        using var response = await _http.GetAsync(
            new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        var envelope = await StudioHttpResponseReader.ReadAsync(
            response, StudioContentJsonContext.Default.StudioApiResponseStudioPublishedArtifact,
            "GetPublishedArtifact", cancellationToken).ConfigureAwait(false);
        if (!envelope.Success || envelope.Data is null)
        {
            throw new HonuaStudioContractException("Published artifact response was unsuccessful or missing data.", "GetPublishedArtifact");
        }

        var artifact = envelope.Data;
        if (!string.Equals(artifact.Route, routeKey, StringComparison.Ordinal)
            || artifact.PublicationId == Guid.Empty || artifact.ItemId == Guid.Empty || artifact.VersionId == Guid.Empty
            || string.IsNullOrWhiteSpace(artifact.ContentHash) || string.IsNullOrWhiteSpace(artifact.PackageKey)
            || artifact.VersionNumber < 1 || artifact.PublishedAt == default
            || !Enum.IsDefined(artifact.Family) || artifact.Envelope is null
            || artifact.Family != artifact.Envelope.Family || string.IsNullOrWhiteSpace(artifact.Envelope.SchemaVersion))
        {
            throw new HonuaStudioContractException("Published artifact does not identify the requested publication.", "GetPublishedArtifact");
        }

        return artifact;
    }

    /// <inheritdoc />
    public Task<StudioContentKindCapabilities> GetContentKindsAsync(CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/package-families",
            content: null,
            StudioContentJsonContext.Default.StudioApiResponseStudioContentKindCapabilities,
            "GetContentKinds",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioContentDraft> CreateDraftAsync(
        CreateStudioContentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Post,
            $"{BasePath}/package-drafts",
            JsonBody(request, StudioContentJsonContext.Default.CreateStudioContentDraftRequest),
            StudioContentJsonContext.Default.StudioApiResponseStudioContentDraft,
            "CreateDraft",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<StudioContentDraft> GetDraftAsync(Guid draftId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/package-drafts/{draftId}",
            content: null,
            StudioContentJsonContext.Default.StudioApiResponseStudioContentDraft,
            "GetDraft",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioContentDraft> UpdateDraftAsync(
        Guid draftId,
        UpdateStudioContentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendAsync(
            HttpMethod.Put,
            $"{BasePath}/package-drafts/{draftId}",
            JsonBody(request, StudioContentJsonContext.Default.UpdateStudioContentDraftRequest),
            StudioContentJsonContext.Default.StudioApiResponseStudioContentDraft,
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
            StudioContentJsonContext.Default.StudioApiResponseStudioValidationSummary,
            "ValidateDraft",
            cancellationToken);

    /// <inheritdoc />
    public Task<StudioPreviewPlan> PreviewPlanAsync(Guid draftId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post,
            $"{BasePath}/package-drafts/{draftId}/preview-plan",
            content: null,
            StudioContentJsonContext.Default.StudioApiResponseStudioPreviewPlan,
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
            JsonBody(request, StudioContentJsonContext.Default.SaveStudioContentVersionRequest),
            StudioContentJsonContext.Default.StudioApiResponseStudioContentVersion,
            "CreateContentVersion",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<StudioContentVersionList> ListVersionsAsync(Guid itemId, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Get,
            $"{BasePath}/content-items/{itemId}/versions",
            content: null,
            StudioContentJsonContext.Default.StudioApiResponseStudioContentVersionList,
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
            StudioContentJsonContext.Default.StudioApiResponseStudioContentVersion,
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
            JsonBody(request, StudioContentJsonContext.Default.CompareStudioContentVersionsRequest),
            StudioContentJsonContext.Default.StudioApiResponseStudioVersionComparison,
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
            JsonBody(request, StudioContentJsonContext.Default.CreateStudioPublicationRequest),
            StudioContentJsonContext.Default.StudioApiResponseStudioPublicationRequest,
            "CreatePublishRequest",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StudioContentPointers?> GetContentPointersAsync(
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("A Studio content identifier is required.", nameof(itemId));
        }

        const string operation = "GetContentPointers";
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);
        var seenItems = new HashSet<Guid>();
        long largestTotal = 0;
        string? cursor = null;
        for (var page = 0; page < 100; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = $"{BasePath}/content-items?limit=100";
            if (cursor is not null)
            {
                path += "&cursor=" + Uri.EscapeDataString(cursor);
            }

            using var message = new HttpRequestMessage(HttpMethod.Get, path);
            using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
            var envelope = await StudioHttpResponseReader.ReadAsync(response,
                StudioContentJsonContext.Default.StudioContentPointerEnvelope, operation, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK || !envelope.Success || envelope.Data?.Items is not { } items
                || envelope.Data.Total is not { } total || total < 0 || items.Count > total
                || items.Any(item => item is null || item.ItemId == Guid.Empty
                    || item.CurrentVersionId == Guid.Empty || item.PublishedVersionId == Guid.Empty))
            {
                throw new HonuaStudioContractException("Studio content listing did not contain a valid pointer page.", operation);
            }

            largestTotal = Math.Max(largestTotal, total);
            foreach (var item in items)
            {
                if (!seenItems.Add(item.ItemId))
                {
                    throw new HonuaStudioContractException("Studio content listing repeated an item identity.", operation);
                }
            }

            var match = items.FirstOrDefault(item => item.ItemId == itemId);
            if (match is not null)
            {
                return match;
            }

            cursor = envelope.Data.NextCursor;
            if (string.IsNullOrWhiteSpace(cursor))
            {
                if (seenItems.Count != largestTotal)
                {
                    throw new HonuaStudioContractException("Studio content listing ended before its advertised total was observed; item presence is unknown.", operation);
                }

                return null;
            }

            if (!seenCursors.Add(cursor))
            {
                throw new HonuaStudioContractException("Studio content listing repeated a pagination cursor.", operation);
            }
        }

        throw new HonuaStudioContractException("Studio content listing exceeded the 100-page scan limit; item presence is unknown.", operation);
    }

    /// <inheritdoc />
    public async Task<StudioPublicationSubmission> SubmitPublishRequestAsync(
        Guid itemId,
        Guid versionId,
        CreateStudioPublicationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        const string operation = "SubmitPublishRequest";
        using var message = new HttpRequestMessage(HttpMethod.Post,
            $"{BasePath}/content-items/{itemId}/versions/{versionId}/publish-requests")
        {
            Content = JsonBody(request, StudioContentJsonContext.Default.CreateStudioPublicationRequest)
        };
        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var envelope = await StudioHttpResponseReader.ReadAsync(response,
            StudioContentJsonContext.Default.StudioPublicationSubmissionEnvelope, operation, cancellationToken).ConfigureAwait(false);
        var data = envelope.Data;
        if (!envelope.Success || data.ValueKind != JsonValueKind.Object)
        {
            throw new HonuaStudioContractException("Publication submission response is missing a successful data payload.", operation);
        }

        try
        {
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                // A governance operation is not a persisted publication request. Reject ambiguous shapes.
                if (data.TryGetProperty("requestId", out _) || data.TryGetProperty("versionId", out _))
                {
                    throw new HonuaStudioContractException("Approval response also contains publication identity.", operation);
                }

                if (data.TryGetProperty("resourceIds", out var resources) && resources.ValueKind != JsonValueKind.Object)
                {
                    throw new HonuaStudioContractException("Approval response resource identities must be an object when supplied.", operation);
                }

                var pending = data.Deserialize(StudioContentJsonContext.Default.HonuaOperationHandle)
                    ?? throw new HonuaStudioContractException("Approval response has no operation.", operation);
                if (!string.Equals(pending.OperationId, "studio.content.create-publication-request", StringComparison.Ordinal)
                    || !MatchesResource(pending.ResourceIds, "itemId", itemId)
                    || !MatchesResource(pending.ResourceIds, "versionId", versionId))
                {
                    throw new HonuaStudioContractException("Approval response identifies another operation or content version.", operation);
                }

                return StudioPublicationSubmission.AwaitingApproval(pending);
            }

            if (response.StatusCode != HttpStatusCode.Created
                || data.TryGetProperty("operationInstanceId", out _)
                || data.TryGetProperty("proposalId", out _))
            {
                throw new HonuaStudioContractException("Publication submission returned an unexpected status or ambiguous payload.", operation);
            }

            var publication = data.Deserialize(StudioContentJsonContext.Default.StudioPublicationRequest)
                ?? throw new HonuaStudioContractException("Publication response has no publication request.", operation);
            if (publication.RequestId == Guid.Empty || publication.ItemId != itemId || publication.VersionId != versionId
                || !Enum.IsDefined(publication.Status))
            {
                throw new HonuaStudioContractException("Publication response does not identify the requested content version.", operation);
            }

            return StudioPublicationSubmission.FromPublication(publication);
        }
        catch (JsonException ex)
        {
            throw new HonuaStudioContractException("Publication submission payload did not match its response status.", operation, responseBody: null, innerException: ex);
        }
        catch (ArgumentException ex)
        {
            throw new HonuaStudioContractException("Approval response did not identify a pending approval operation.", operation, responseBody: null, innerException: ex);
        }
    }

    // Decision handles currently omit resource IDs; validate any target identities supplied
    // by later server versions without requiring execution-only metadata before approval.
    private static bool MatchesResource(IReadOnlyDictionary<string, string> resources, string key, Guid expected)
        => !resources.TryGetValue(key, out var value)
            || (Guid.TryParse(value, out var actual) && actual == expected);

    /// <inheritdoc />
    public Task<StudioContentDraft> ReopenVersionAsync(
        Guid itemId,
        Guid versionId,
        CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post,
            $"{BasePath}/content-items/{itemId}/versions/{versionId}/reopen",
            content: null,
            StudioContentJsonContext.Default.StudioApiResponseStudioContentDraft,
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
            JsonBody(request, StudioContentJsonContext.Default.CreateStudioRollbackRequest),
            StudioContentJsonContext.Default.StudioApiResponseStudioRollbackRequest,
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
