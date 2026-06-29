// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// HTTP client implementation for the Honua Admin REST API.
/// </summary>
public sealed class HonuaAdminClient : IHonuaAdminClient
{
    private const string ApiPrefix = "/api/v1/admin";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaAdminClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaAdminClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    // ── Services ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<ServiceSummary[]>(
            $"{ApiPrefix}/services/",
            HonuaAdminJsonContext.Default.ApiResponseServiceSummaryArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<ServiceSettingsResponse> GetServiceSettingsAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<ServiceSettingsResponse>(
            $"{ApiPrefix}/services/{Uri.EscapeDataString(serviceName)}/settings",
            HonuaAdminJsonContext.Default.ApiResponseServiceSettingsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null service settings.", "GetServiceSettings");
    }

    /// <inheritdoc />
    public async Task<ServiceSettingsResponse> UpdateProtocolsAsync(string serviceName, IReadOnlyList<string> protocols, CancellationToken cancellationToken = default)
    {
        var body = new UpdateProtocolsRequest { EnabledProtocols = [.. protocols] };
        var data = await PutAsync<ServiceSettingsResponse>(
            $"{ApiPrefix}/services/{Uri.EscapeDataString(serviceName)}/protocols",
            body,
            HonuaAdminJsonContext.Default.UpdateProtocolsRequest,
            HonuaAdminJsonContext.Default.ApiResponseServiceSettingsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateProtocols");
    }

    /// <inheritdoc />
    public async Task<ServiceSettingsResponse> UpdateMapServerSettingsAsync(string serviceName, UpdateMapServerSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<ServiceSettingsResponse>(
            $"{ApiPrefix}/services/{Uri.EscapeDataString(serviceName)}/mapserver",
            request,
            HonuaAdminJsonContext.Default.UpdateMapServerSettingsRequest,
            HonuaAdminJsonContext.Default.ApiResponseServiceSettingsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateMapServerSettings");
    }

    /// <inheritdoc />
    public async Task<ServiceSettingsResponse> UpdateAccessPolicyAsync(string serviceName, UpdateAccessPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<ServiceSettingsResponse>(
            $"{ApiPrefix}/services/{Uri.EscapeDataString(serviceName)}/access-policy",
            request,
            HonuaAdminJsonContext.Default.UpdateAccessPolicyRequest,
            HonuaAdminJsonContext.Default.ApiResponseServiceSettingsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateAccessPolicy");
    }

    /// <inheritdoc />
    public async Task<ServiceSettingsResponse> UpdateTimeInfoAsync(string serviceName, UpdateTimeInfoRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<ServiceSettingsResponse>(
            $"{ApiPrefix}/services/{Uri.EscapeDataString(serviceName)}/timeinfo",
            request,
            HonuaAdminJsonContext.Default.UpdateTimeInfoRequest,
            HonuaAdminJsonContext.Default.ApiResponseServiceSettingsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateTimeInfo");
    }

    /// <inheritdoc />
    public async Task<LayerMetadataResponse> UpdateLayerMetadataAsync(string serviceName, int layerId, UpdateLayerMetadataRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<LayerMetadataResponse>(
            $"{ApiPrefix}/services/{Uri.EscapeDataString(serviceName)}/layers/{layerId}/metadata",
            request,
            HonuaAdminJsonContext.Default.UpdateLayerMetadataRequest,
            HonuaAdminJsonContext.Default.ApiResponseLayerMetadataResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateLayerMetadata");
    }

    // ── Metadata Resources ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<MetadataResource>> ListMetadataResourcesAsync(string? kind = null, string? ns = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("kind", kind), ("namespace", ns));
        var data = await GetAsync<MetadataResource[]>(
            $"{ApiPrefix}/metadata/resources{query}",
            HonuaAdminJsonContext.Default.ApiResponseMetadataResourceArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<(MetadataResource Resource, string? ETag)> GetMetadataResourceAsync(string kind, string ns, string name, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiPrefix}/metadata/resources/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(ns)}/{Uri.EscapeDataString(name)}";
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.ApiResponseMetadataResource);
        var resource = envelope?.Data ?? throw new HonuaAdminOperationException("Server returned null metadata resource.", "GetMetadataResource");

        return (resource, GetETag(response));
    }

    /// <inheritdoc />
    public async Task<MetadataResource> CreateMetadataResourceAsync(MetadataResource resource, CancellationToken cancellationToken = default)
        => (await CreateMetadataResourceWithResponseAsync(resource, cancellationToken).ConfigureAwait(false)).Resource;

    /// <inheritdoc />
    public Task<MetadataResourceResponse> CreateMetadataResourceWithResponseAsync(MetadataResource resource, CancellationToken cancellationToken = default)
        => SendMetadataResourceAsync(
            $"{ApiPrefix}/metadata/resources",
            HttpMethod.Post,
            resource,
            ifMatch: null,
            operation: "CreateMetadataResource",
            cancellationToken);

    /// <inheritdoc />
    public async Task<MetadataResource> UpdateMetadataResourceAsync(string kind, string ns, string name, MetadataResource resource, string? ifMatch = null, CancellationToken cancellationToken = default)
        => (await UpdateMetadataResourceWithResponseAsync(kind, ns, name, resource, ifMatch, cancellationToken).ConfigureAwait(false)).Resource;

    /// <inheritdoc />
    public Task<MetadataResourceResponse> UpdateMetadataResourceWithResponseAsync(string kind, string ns, string name, MetadataResource resource, string? ifMatch = null, CancellationToken cancellationToken = default)
        => SendMetadataResourceAsync(
            $"{ApiPrefix}/metadata/resources/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(ns)}/{Uri.EscapeDataString(name)}",
            HttpMethod.Put,
            resource,
            ifMatch,
            operation: "UpdateMetadataResource",
            cancellationToken);

    /// <inheritdoc />
    public async Task DeleteMetadataResourceAsync(string kind, string ns, string name, string? ifMatch = null, CancellationToken cancellationToken = default)
    {
        var url = $"{ApiPrefix}/metadata/resources/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(ns)}/{Uri.EscapeDataString(name)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);

        if (!string.IsNullOrEmpty(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);
    }

    private async Task<MetadataResourceResponse> SendMetadataResourceAsync(
        string url,
        HttpMethod method,
        MetadataResource resource,
        string? ifMatch,
        string operation,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(resource, HonuaAdminJsonContext.Default.MetadataResource);
        using var request = new HttpRequestMessage(method, CreateRequestUri(url)) { Content = content };

        if (!string.IsNullOrEmpty(ifMatch))
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.ApiResponseMetadataResource);
        var responseResource = envelope?.Data ?? throw new HonuaAdminOperationException("Server returned null response.", operation);
        return new MetadataResourceResponse
        {
            Resource = responseResource,
            ETag = GetETag(response)
        };
    }

    // ── Manifests ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<AdminVersionResponse> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<AdminVersionResponse>(
            $"{ApiPrefix}/version",
            HonuaAdminJsonContext.Default.ApiResponseAdminVersionResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null version response.", "GetVersion");
    }

    /// <inheritdoc />
    public async Task<AdminCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<AdminCapabilitiesResponse>(
            $"{ApiPrefix}/capabilities",
            HonuaAdminJsonContext.Default.ApiResponseAdminCapabilitiesResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null capabilities response.", "GetCapabilities");
    }

    /// <inheritdoc />
    public async Task<ServerCompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken = default)
    {
        var capabilities = await GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        return HonuaAdminCompatibility.Evaluate(capabilities);
    }

    /// <inheritdoc />
    public async Task<MetadataManifest> GetManifestAsync(string? ns = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("namespace", ns));
        var data = await GetAsync<MetadataManifest>(
            $"{ApiPrefix}/manifest{query}",
            HonuaAdminJsonContext.Default.ApiResponseMetadataManifest,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null manifest.", "GetManifest");
    }

    /// <inheritdoc />
    public async Task<ManifestApplyResult> ApplyManifestAsync(ManifestApplyRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<ManifestApplyResult>(
            $"{ApiPrefix}/manifest/apply",
            request,
            HonuaAdminJsonContext.Default.ManifestApplyRequest,
            HonuaAdminJsonContext.Default.ApiResponseManifestApplyResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null apply result.", "ApplyManifest");
    }

    // ── Connections ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecureConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<SecureConnectionSummary[]>(
            $"{ApiPrefix}/connections/",
            HonuaAdminJsonContext.Default.ApiResponseSecureConnectionSummaryArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<SecureConnectionDetail> GetConnectionAsync(string id, CancellationToken cancellationToken = default)
    {
        var connectionId = NormalizeSecureConnectionId(id, nameof(id));
        var data = await GetAsync<SecureConnectionDetail>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(connectionId)}",
            HonuaAdminJsonContext.Default.ApiResponseSecureConnectionDetail,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null connection.", "GetConnection");
    }

    /// <inheritdoc />
    public async Task<SecureConnectionSummary> CreateConnectionAsync(CreateSecureConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<SecureConnectionSummary>(
            $"{ApiPrefix}/connections/",
            request,
            HonuaAdminJsonContext.Default.CreateSecureConnectionRequest,
            HonuaAdminJsonContext.Default.ApiResponseSecureConnectionSummary,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "CreateConnection");
    }

    /// <inheritdoc />
    public async Task<ConnectionTestResult> TestDraftConnectionAsync(CreateSecureConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<ConnectionTestResult>(
            $"{ApiPrefix}/connections/test",
            request,
            HonuaAdminJsonContext.Default.CreateSecureConnectionRequest,
            HonuaAdminJsonContext.Default.ApiResponseConnectionTestResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null test result.", "TestDraftConnection");
    }

    /// <inheritdoc />
    public async Task<SecureConnectionSummary> UpdateConnectionAsync(string id, UpdateSecureConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var connectionId = NormalizeSecureConnectionId(id, nameof(id));
        var data = await PutAsync<SecureConnectionSummary>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(connectionId)}",
            request,
            HonuaAdminJsonContext.Default.UpdateSecureConnectionRequest,
            HonuaAdminJsonContext.Default.ApiResponseSecureConnectionSummary,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateConnection");
    }

    /// <inheritdoc />
    public async Task<ConnectionTestResult> TestConnectionAsync(string id, CancellationToken cancellationToken = default)
    {
        var connectionId = NormalizeSecureConnectionId(id, nameof(id));
        var data = await PostAsync<ConnectionTestResult>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(connectionId)}/test",
            (object?)null,
            HonuaAdminJsonContext.Default.ApiResponseConnectionTestResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null test result.", "TestConnection");
    }

    /// <inheritdoc />
    public async Task DeleteConnectionAsync(string id, CancellationToken cancellationToken = default)
    {
        var connectionId = NormalizeSecureConnectionId(id, nameof(id));
        using var response = await _http.DeleteAsync(
            CreateRequestUri($"{ApiPrefix}/connections/{Uri.EscapeDataString(connectionId)}"), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);
    }

    /// <inheritdoc />
    public async Task<EncryptionValidationResult> ValidateEncryptionAsync(CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<EncryptionValidationResult>(
            $"{ApiPrefix}/connections/encryption/validate",
            (object?)null,
            HonuaAdminJsonContext.Default.ApiResponseEncryptionValidationResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null validation result.", "ValidateEncryption");
    }

    /// <inheritdoc />
    public async Task<KeyRotationResult> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<KeyRotationResult>(
            $"{ApiPrefix}/connections/encryption/rotate-key",
            (object?)null,
            HonuaAdminJsonContext.Default.ApiResponseKeyRotationResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null rotation result.", "RotateEncryptionKey");
    }

    // ── Layers ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishedLayerSummary>> ListLayersAsync(string connectionId, string? serviceName = null, CancellationToken cancellationToken = default)
    {
        var normalizedConnectionId = NormalizeSecureConnectionId(connectionId, nameof(connectionId));
        var query = BuildQuery(("serviceName", serviceName));
        var data = await GetAsync<PublishedLayerSummary[]>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(normalizedConnectionId)}/layers/{query}",
            HonuaAdminJsonContext.Default.ApiResponsePublishedLayerSummaryArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<PublishedLayerSummary> PublishLayerAsync(string connectionId, PublishLayerRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedConnectionId = NormalizeSecureConnectionId(connectionId, nameof(connectionId));
        var data = await PostAsync<PublishedLayerSummary>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(normalizedConnectionId)}/layers/",
            request,
            HonuaAdminJsonContext.Default.PublishLayerRequest,
            HonuaAdminJsonContext.Default.ApiResponsePublishedLayerSummary,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "PublishLayer");
    }

    /// <inheritdoc />
    public async Task<PublishedLayerSummary> SetLayerEnabledAsync(string connectionId, int layerId, bool enabled, string? serviceName = null, CancellationToken cancellationToken = default)
    {
        var normalizedConnectionId = NormalizeSecureConnectionId(connectionId, nameof(connectionId));
        var query = BuildQuery(("serviceName", serviceName));
        var body = new LayerEnabledRequest { Enabled = enabled };
        var data = await PutAsync<PublishedLayerSummary>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(normalizedConnectionId)}/layers/{layerId}/enabled{query}",
            body,
            HonuaAdminJsonContext.Default.LayerEnabledRequest,
            HonuaAdminJsonContext.Default.ApiResponsePublishedLayerSummary,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "SetLayerEnabled");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PublishedLayerSummary>> SetServiceLayersEnabledAsync(string connectionId, bool enabled, string? serviceName = null, CancellationToken cancellationToken = default)
    {
        var normalizedConnectionId = NormalizeSecureConnectionId(connectionId, nameof(connectionId));
        var query = BuildQuery(("serviceName", serviceName));
        var body = new LayerEnabledRequest { Enabled = enabled };
        var data = await PutAsync<PublishedLayerSummary[]>(
            $"{ApiPrefix}/connections/{Uri.EscapeDataString(normalizedConnectionId)}/layers/enabled{query}",
            body,
            HonuaAdminJsonContext.Default.LayerEnabledRequest,
            HonuaAdminJsonContext.Default.ApiResponsePublishedLayerSummaryArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    // ── Discovery ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TableDiscoveryResponse> DiscoverTablesAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        var normalizedConnectionId = NormalizeSecureConnectionId(connectionId, nameof(connectionId));
        var url = $"{ApiPrefix}/connections/{Uri.EscapeDataString(normalizedConnectionId)}/tables";
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        // Table discovery returns TableDiscoveryResponse directly (not wrapped in ApiResponse)
        var result = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.TableDiscoveryResponse);
        return result ?? throw new HonuaAdminOperationException("Server returned null discovery response.", "DiscoverTables");
    }

    // ── Migration Toolkit ────────────────────────────────────────────────

    /// <summary>
    /// Scans a supported source environment and returns the migration source inventory artifact.
    /// </summary>
    /// <param name="request">The migration inventory scan request.</param>
    /// <param name="exportJson">When true, requests the server's JSON attachment form with <c>export=json</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration source inventory artifact returned by the server.</returns>
    public async Task<MigrationSourceInventoryArtifact> ScanMigrationSourceAsync(
        MigrationInventoryScanRequest request,
        bool exportJson = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = exportJson ? BuildQuery(("export", "json")) : string.Empty;
        var data = await PostRawAsync(
            $"{ApiPrefix}/import/scan{query}",
            request,
            HonuaAdminJsonContext.Default.MigrationInventoryScanRequest,
            HonuaAdminJsonContext.Default.MigrationSourceInventoryArtifact,
            cancellationToken).ConfigureAwait(false);

        return data ?? throw new HonuaAdminOperationException("Server returned null migration inventory artifact.", "ScanMigrationSource");
    }

    // ── Styles ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<LayerStyleResponse> GetLayerStyleAsync(int layerId, CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<LayerStyleResponse>(
            $"{ApiPrefix}/metadata/layers/{layerId}/style",
            HonuaAdminJsonContext.Default.ApiResponseLayerStyleResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null style response.", "GetLayerStyle");
    }

    /// <inheritdoc />
    public async Task<LayerStyleResponse> UpdateLayerStyleAsync(int layerId, LayerStyleUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<LayerStyleResponse>(
            $"{ApiPrefix}/metadata/layers/{layerId}/style",
            request,
            HonuaAdminJsonContext.Default.LayerStyleUpdateRequest,
            HonuaAdminJsonContext.Default.ApiResponseLayerStyleResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null response.", "UpdateLayerStyle");
    }

    // ── Config ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<JsonElement> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(CreateRequestUri($"{ApiPrefix}/config"), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    // ── Identity ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<OidcProviderResponse>> ListOidcProvidersAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<OidcProviderResponse[]>(
            $"{ApiPrefix}/oidc/providers",
            HonuaAdminJsonContext.Default.ApiResponseOidcProviderResponseArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<OidcProviderResponse?> GetOidcProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            CreateRequestUri($"{ApiPrefix}/oidc/providers/{providerId:D}"), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.ApiResponseOidcProviderResponse);
        return envelope?.Data;
    }

    /// <inheritdoc />
    public async Task<OidcProviderResponse> CreateOidcProviderAsync(CreateOidcProviderRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<OidcProviderResponse>(
            $"{ApiPrefix}/oidc/providers",
            request,
            HonuaAdminJsonContext.Default.CreateOidcProviderRequest,
            HonuaAdminJsonContext.Default.ApiResponseOidcProviderResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null provider.", "CreateOidcProvider");
    }

    /// <inheritdoc />
    public async Task<OidcProviderResponse> UpdateOidcProviderAsync(Guid providerId, UpdateOidcProviderRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<OidcProviderResponse>(
            $"{ApiPrefix}/oidc/providers/{providerId:D}",
            request,
            HonuaAdminJsonContext.Default.UpdateOidcProviderRequest,
            HonuaAdminJsonContext.Default.ApiResponseOidcProviderResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null provider.", "UpdateOidcProvider");
    }

    /// <inheritdoc />
    public async Task DeleteOidcProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync(
            CreateRequestUri($"{ApiPrefix}/oidc/providers/{providerId:D}"), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);
    }

    /// <inheritdoc />
    public async Task<OidcProviderTestResponse> TestOidcProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<OidcProviderTestResponse>(
            $"{ApiPrefix}/oidc/providers/{providerId:D}/test",
            (object?)null,
            HonuaAdminJsonContext.Default.ApiResponseOidcProviderTestResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null provider test result.", "TestOidcProvider");
    }

    /// <inheritdoc />
    public async Task<IdentityProvidersResponse> GetIdentityProvidersAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<IdentityProvidersResponse>(
            $"{ApiPrefix}/identity/providers",
            HonuaAdminJsonContext.Default.ApiResponseIdentityProvidersResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? new IdentityProvidersResponse();
    }

    /// <inheritdoc />
    public async Task<IdentityProviderTestResult> TestIdentityProviderAsync(string providerType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new ArgumentException("Provider type must be supplied.", nameof(providerType));
        }

        var data = await GetAsync<IdentityProviderTestResult>(
            $"{ApiPrefix}/identity/providers/{Uri.EscapeDataString(providerType)}/test",
            HonuaAdminJsonContext.Default.ApiResponseIdentityProviderTestResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null provider test result.", "TestIdentityProvider");
    }

    // ── RBAC ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<RoleResponse[]>(
            $"{ApiPrefix}/roles/",
            HonuaAdminJsonContext.Default.ApiResponseRoleResponseArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<RoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(
            CreateRequestUri($"{ApiPrefix}/roles/{roleId:D}"), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.ApiResponseRoleResponse);
        return envelope?.Data;
    }

    /// <inheritdoc />
    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<RoleResponse>(
            $"{ApiPrefix}/roles/",
            request,
            HonuaAdminJsonContext.Default.CreateRoleRequest,
            HonuaAdminJsonContext.Default.ApiResponseRoleResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null role.", "CreateRole");
    }

    /// <inheritdoc />
    public async Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<RoleResponse>(
            $"{ApiPrefix}/roles/{roleId:D}",
            request,
            HonuaAdminJsonContext.Default.UpdateRoleRequest,
            HonuaAdminJsonContext.Default.ApiResponseRoleResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null role.", "UpdateRole");
    }

    /// <inheritdoc />
    public Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => DeleteEnvelopeAsync($"{ApiPrefix}/roles/{roleId:D}", cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermissionGrantResponse>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<PermissionGrantResponse[]>(
            $"{ApiPrefix}/roles/{roleId:D}/permissions",
            HonuaAdminJsonContext.Default.ApiResponsePermissionGrantResponseArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermissionGrantResponse>> SetRolePermissionsAsync(Guid roleId, SetPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<PermissionGrantResponse[]>(
            $"{ApiPrefix}/roles/{roleId:D}/permissions",
            request,
            HonuaAdminJsonContext.Default.SetPermissionsRequest,
            HonuaAdminJsonContext.Default.ApiResponsePermissionGrantResponseArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<UserListResponse> ListUsersAsync(UserListQuery? query = null, CancellationToken cancellationToken = default)
    {
        var queryString = query is null
            ? string.Empty
            : BuildQuery(
                ("source", query.Source),
                ("role", query.Role),
                ("active", query.Active.HasValue ? (query.Active.Value ? "true" : "false") : null),
                ("limit", query.Limit?.ToString(CultureInfo.InvariantCulture)),
                ("offset", query.Offset?.ToString(CultureInfo.InvariantCulture)));
        var data = await GetAsync<UserListResponse>(
            $"{ApiPrefix}/users/{queryString}",
            HonuaAdminJsonContext.Default.ApiResponseUserListResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? new UserListResponse();
    }

    /// <inheritdoc />
    public async Task<UserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID must be supplied.", nameof(userId));
        }

        using var response = await _http.GetAsync(
            CreateRequestUri($"{ApiPrefix}/users/{Uri.EscapeDataString(userId)}"), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.ApiResponseUserResponse);
        return envelope?.Data;
    }

    /// <inheritdoc />
    public async Task<UserResponse> UpdateUserRolesAsync(string userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID must be supplied.", nameof(userId));
        }

        var data = await PutAsync<UserResponse>(
            $"{ApiPrefix}/users/{Uri.EscapeDataString(userId)}/roles",
            request,
            HonuaAdminJsonContext.Default.UpdateUserRolesRequest,
            HonuaAdminJsonContext.Default.ApiResponseUserResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null user.", "UpdateUserRoles");
    }

    /// <inheritdoc />
    public Task DeprovisionUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID must be supplied.", nameof(userId));
        }

        return DeleteEnvelopeAsync($"{ApiPrefix}/users/{Uri.EscapeDataString(userId)}", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EffectivePermissionsResponse> GetEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID must be supplied.", nameof(userId));
        }

        var data = await GetAsync<EffectivePermissionsResponse>(
            $"{ApiPrefix}/users/{Uri.EscapeDataString(userId)}/effective-permissions",
            HonuaAdminJsonContext.Default.ApiResponseEffectivePermissionsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null effective permissions.", "GetEffectivePermissions");
    }

    // ── License ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<LicenseStatusResponse> GetLicenseStatusAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<LicenseStatusResponse>(
            $"{ApiPrefix}/license",
            HonuaAdminJsonContext.Default.ApiResponseLicenseStatusResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null license status.", "GetLicenseStatus");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LicenseEntitlement>> GetLicenseEntitlementsAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<LicenseEntitlement[]>(
            $"{ApiPrefix}/license/entitlements",
            HonuaAdminJsonContext.Default.ApiResponseLicenseEntitlementArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<LicenseStatusResponse> UploadLicenseAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(HttpMethod.Post, CreateRequestUri($"{ApiPrefix}/license"))
        {
            Content = content
        };

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.ApiResponseLicenseStatusResponse);
        return envelope?.Data ?? throw new HonuaAdminOperationException("Server returned null license status.", "UploadLicense");
    }

    // ── Raster import (write/output) ──────────────────────────────────────

    /// <inheritdoc />
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "MultipartFormDataContent owns and disposes child HttpContent instances.")]
    public async Task<RasterImportResult> ImportRasterAsync(RasterImportRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        using var form = new MultipartFormDataContent();

        // The caller retains ownership of the content stream; wrap it so disposing the multipart
        // form (and its StreamContent child) does not dispose the caller's stream.
        var rasterContent = new StreamContent(new Honua.Sdk.Abstractions.Http.NonDisposingStream(request.Content));
        rasterContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType);
        form.Add(rasterContent, "file", request.FileName);

        form.Add(new StringContent(request.LayerId.ToString(CultureInfo.InvariantCulture)), "layerId");
        form.Add(new StringContent(request.Name), "name");

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            form.Add(new StringContent(request.Description), "description");
        }

        if (request.Srid is { } srid)
        {
            form.Add(new StringContent(srid.ToString(CultureInfo.InvariantCulture)), "srid");
        }

        if (request.AcquisitionDate is { } acquisitionDate)
        {
            form.Add(new StringContent(acquisitionDate.ToString("O", CultureInfo.InvariantCulture)), "acquisitionDate");
        }

        if (request.TileZoomLevels is { Count: > 0 } zoomLevels)
        {
            form.Add(
                new StringContent(string.Join(",", zoomLevels.Select(z => z.ToString(CultureInfo.InvariantCulture)))),
                "tileZoomLevels");
        }

        // World-file and projection sidecars are uploaded as file parts; the server routes them by
        // extension (.wld/.pgw/.jgw/.tfw for world files, .prj for projections).
        var stem = Path.GetFileNameWithoutExtension(request.FileName);
        if (!string.IsNullOrEmpty(request.WorldFileContent))
        {
            form.Add(new StringContent(request.WorldFileContent), "worldFile", $"{stem}.wld");
        }

        if (!string.IsNullOrEmpty(request.ProjectionContent))
        {
            form.Add(new StringContent(request.ProjectionContent), "projection", $"{stem}.prj");
        }

        using var response = await _http.PostAsync(
            CreateRequestUri($"{ApiPrefix}/import/raster/"), form, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return JsonSerializer.Deserialize(body, HonuaAdminJsonContext.Default.RasterImportResult)
            ?? throw new HonuaAdminOperationException("Server returned null raster import result.", "ImportRaster");
    }

    /// <inheritdoc />
    public async Task<RasterFormatsResponse> GetSupportedRasterFormatsAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetRawAsync<RasterFormatsResponse>(
            $"{ApiPrefix}/import/raster/formats",
            HonuaAdminJsonContext.Default.RasterFormatsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null raster formats response.", "GetSupportedRasterFormats");
    }

    // ── Observability ────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecentError>> GetRecentErrorsAsync(int? limit = null, CancellationToken cancellationToken = default)
        => (await GetRecentErrorsResponseAsync(limit, cancellationToken).ConfigureAwait(false)).Errors;

    /// <inheritdoc />
    public async Task<RecentErrorsResponse> GetRecentErrorsResponseAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("limit", limit?.ToString(CultureInfo.InvariantCulture)));
        var data = await GetRawAsync<RecentErrorsResponse>(
            $"{ApiPrefix}/observability/errors{query}",
            HonuaAdminJsonContext.Default.RecentErrorsResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null recent errors response.", "GetRecentErrors");
    }

    /// <inheritdoc />
    public async Task<TelemetryStatus> GetTelemetryStatusAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetRawAsync<TelemetryStatus>(
            $"{ApiPrefix}/observability/telemetry",
            HonuaAdminJsonContext.Default.TelemetryStatus,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null telemetry status.", "GetTelemetryStatus");
    }

    /// <inheritdoc />
    public async Task<MigrationStatus> GetMigrationStatusAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetRawAsync<MigrationStatus>(
            $"{ApiPrefix}/observability/migrations",
            HonuaAdminJsonContext.Default.MigrationStatus,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null migration status.", "GetMigrationStatus");
    }

    // ── Alerts and Streams ──────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<AlertZoneResponse>> ListAlertZonesAsync(string? serviceId = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("serviceId", serviceId));
        var data = await GetAsync<AlertZoneResponse[]>(
            $"{ApiPrefix}/alerts/zones{query}",
            HonuaAdminJsonContext.Default.ApiResponseAlertZoneResponseArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<AlertZoneResponse> CreateAlertZoneAsync(AlertZoneRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<AlertZoneResponse>(
            $"{ApiPrefix}/alerts/zones",
            request,
            HonuaAdminJsonContext.Default.AlertZoneRequest,
            HonuaAdminJsonContext.Default.ApiResponseAlertZoneResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null alert zone.", "CreateAlertZone");
    }

    /// <inheritdoc />
    public async Task<AlertZoneResponse> UpdateAlertZoneAsync(long zoneId, AlertZoneRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<AlertZoneResponse>(
            $"{ApiPrefix}/alerts/zones/{zoneId}",
            request,
            HonuaAdminJsonContext.Default.AlertZoneRequest,
            HonuaAdminJsonContext.Default.ApiResponseAlertZoneResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null alert zone.", "UpdateAlertZone");
    }

    /// <inheritdoc />
    public Task DeleteAlertZoneAsync(long zoneId, CancellationToken cancellationToken = default)
        => DeleteEnvelopeAsync($"{ApiPrefix}/alerts/zones/{zoneId}", cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AlertRuleResponse>> ListAlertRulesAsync(string? serviceId = null, int? layerId = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("serviceId", serviceId),
            ("layerId", layerId?.ToString(CultureInfo.InvariantCulture)));
        var data = await GetAsync<AlertRuleResponse[]>(
            $"{ApiPrefix}/alerts/rules{query}",
            HonuaAdminJsonContext.Default.ApiResponseAlertRuleResponseArray,
            cancellationToken).ConfigureAwait(false);
        return data ?? [];
    }

    /// <inheritdoc />
    public async Task<AlertRuleResponse> CreateAlertRuleAsync(AlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostAsync<AlertRuleResponse>(
            $"{ApiPrefix}/alerts/rules",
            request,
            HonuaAdminJsonContext.Default.AlertRuleRequest,
            HonuaAdminJsonContext.Default.ApiResponseAlertRuleResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null alert rule.", "CreateAlertRule");
    }

    /// <inheritdoc />
    public async Task<AlertRuleResponse> UpdateAlertRuleAsync(long ruleId, AlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PutAsync<AlertRuleResponse>(
            $"{ApiPrefix}/alerts/rules/{ruleId}",
            request,
            HonuaAdminJsonContext.Default.AlertRuleRequest,
            HonuaAdminJsonContext.Default.ApiResponseAlertRuleResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null alert rule.", "UpdateAlertRule");
    }

    /// <inheritdoc />
    public Task DeleteAlertRuleAsync(long ruleId, CancellationToken cancellationToken = default)
        => DeleteEnvelopeAsync($"{ApiPrefix}/alerts/rules/{ruleId}", cancellationToken);

    /// <inheritdoc />
    public async Task<FeatureEventReplayResponse> ReplayFeatureEventsAsync(FeatureEventReplayQuery? query = null, CancellationToken cancellationToken = default)
    {
        var queryString = query is null
            ? string.Empty
            : BuildQuery(
                ("cursor", query.Cursor?.ToString(CultureInfo.InvariantCulture)),
                ("from", query.From?.ToString("O", CultureInfo.InvariantCulture)),
                ("to", query.To?.ToString("O", CultureInfo.InvariantCulture)),
                ("limit", query.Limit?.ToString(CultureInfo.InvariantCulture)));
        var data = await GetRawAsync<FeatureEventReplayResponse>(
            $"{ApiPrefix}/feature-events/replay{queryString}",
            HonuaAdminJsonContext.Default.FeatureEventReplayResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null feature-event replay response.", "ReplayFeatureEvents");
    }

    /// <inheritdoc />
    public async Task<SubscriberListResponse> ListStreamingSubscribersAsync(CancellationToken cancellationToken = default)
    {
        var data = await GetAsync<SubscriberListResponse>(
            $"{ApiPrefix}/operations/streaming/subscribers",
            HonuaAdminJsonContext.Default.ApiResponseSubscriberListResponse,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null subscriber list.", "ListStreamingSubscribers");
    }

    /// <inheritdoc />
    public Task DisconnectStreamingSubscriberAsync(Guid subscriberId, CancellationToken cancellationToken = default)
        => DeleteEnvelopeAsync($"{ApiPrefix}/operations/streaming/subscribers/{subscriberId:D}", cancellationToken);

    // ── Deploy Control ──────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DeployPreflightResult> GetDeployPreflightAsync(CancellationToken cancellationToken = default)
        => await GetDeployPreflightAsync(includeDiagnostics: false, cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<DeployPreflightResult> GetDeployPreflightAsync(bool includeDiagnostics, CancellationToken cancellationToken = default)
    {
        var query = includeDiagnostics ? BuildQuery(("includeDiagnostics", "true")) : string.Empty;
        var data = await GetRawAsync<DeployPreflightResult>(
            $"{ApiPrefix}/deploy/preflight{query}",
            HonuaAdminJsonContext.Default.DeployPreflightResult,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null preflight result.", "GetDeployPreflight");
    }

    /// <inheritdoc />
    public async Task<DeployPlan> CreateDeployPlanAsync(CreateDeployPlanRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostRawAsync(
            $"{ApiPrefix}/deploy/plan",
            request,
            HonuaAdminJsonContext.Default.CreateDeployPlanRequest,
            HonuaAdminJsonContext.Default.DeployPlan,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null deploy plan.", "CreateDeployPlan");
    }

    /// <inheritdoc />
    public async Task<DeployOperation> CreateDeployOperationAsync(CreateDeployOperationRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostRawAsync(
            $"{ApiPrefix}/deploy/operations",
            request,
            HonuaAdminJsonContext.Default.CreateDeployOperationRequest,
            HonuaAdminJsonContext.Default.DeployOperation,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null deploy operation.", "CreateDeployOperation");
    }

    /// <inheritdoc />
    public async Task<DeployOperation> GetDeployOperationAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var data = await GetRawAsync<DeployOperation>(
            $"{ApiPrefix}/deploy/operations/{Uri.EscapeDataString(operationId)}",
            HonuaAdminJsonContext.Default.DeployOperation,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null deploy operation.", "GetDeployOperation");
    }

    /// <inheritdoc />
    public async Task<DeployOperation> SubmitDeployOperationAsync(string operationId, CancellationToken cancellationToken = default)
        => await SubmitDeployOperationAsync(operationId, new SubmitDeployOperationRequest(), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<DeployOperation> SubmitDeployOperationAsync(string operationId, SubmitDeployOperationRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostRawAsync<DeployOperation>(
            $"{ApiPrefix}/deploy/operations/{Uri.EscapeDataString(operationId)}/submit",
            request,
            HonuaAdminJsonContext.Default.SubmitDeployOperationRequest,
            HonuaAdminJsonContext.Default.DeployOperation,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null deploy operation.", "SubmitDeployOperation");
    }

    /// <inheritdoc />
    public async Task<DeployOperation> RollbackDeployOperationAsync(string operationId, CancellationToken cancellationToken = default)
        => await RollbackDeployOperationAsync(operationId, new RollbackDeployOperationRequest(), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<DeployOperation> RollbackDeployOperationAsync(string operationId, RollbackDeployOperationRequest request, CancellationToken cancellationToken = default)
    {
        var data = await PostRawAsync<DeployOperation>(
            $"{ApiPrefix}/deploy/operations/{Uri.EscapeDataString(operationId)}/rollback",
            request,
            HonuaAdminJsonContext.Default.RollbackDeployOperationRequest,
            HonuaAdminJsonContext.Default.DeployOperation,
            cancellationToken).ConfigureAwait(false);
        return data ?? throw new HonuaAdminOperationException("Server returned null deploy operation.", "RollbackDeployOperation");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<T?> GetAsync<T>(
        string url,
        JsonTypeInfo<ApiResponse<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, typeInfo);
        return envelope is not null ? envelope.Data : default;
    }

    private async Task<T?> GetRawAsync<T>(
        string url,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return JsonSerializer.Deserialize(body, typeInfo);
    }

    private async Task<T?> PostAsync<T>(
        string url,
        object? requestBody,
        JsonTypeInfo<ApiResponse<T>> responseTypeInfo,
        CancellationToken cancellationToken)
    {
        HttpContent content;
        if (requestBody is not null)
        {
            content = new StringContent(
                JsonSerializer.Serialize(requestBody, HonuaAdminJsonContext.Default.Options),
                Encoding.UTF8,
                "application/json");
        }
        else
        {
            content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using (content)
        {
            using var response = await _http.PostAsync(CreateRequestUri(url), content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, body).ConfigureAwait(false);
            EnsureEnvelopeSucceeded(response, body);

            var envelope = JsonSerializer.Deserialize(body, responseTypeInfo);
            return envelope is not null ? envelope.Data : default;
        }
    }

    private async Task<TResponse?> PostAsync<TResponse>(
        string url,
        object requestBody,
        JsonTypeInfo requestTypeInfo,
        JsonTypeInfo<ApiResponse<TResponse>> responseTypeInfo,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(requestBody, requestTypeInfo);
        using var response = await _http.PostAsync(CreateRequestUri(url), content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, responseTypeInfo);
        return envelope is not null ? envelope.Data : default;
    }

    private async Task<TResponse?> PostRawAsync<TResponse>(
        string url,
        object? requestBody,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
    {
        HttpContent content;
        if (requestBody is not null)
        {
            content = new StringContent(
                JsonSerializer.Serialize(requestBody, HonuaAdminJsonContext.Default.Options),
                Encoding.UTF8,
                "application/json");
        }
        else
        {
            content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using (content)
        {
            using var response = await _http.PostAsync(CreateRequestUri(url), content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            await EnsureSuccessAsync(response, body).ConfigureAwait(false);

            return JsonSerializer.Deserialize(body, responseTypeInfo);
        }
    }

    private async Task<TResponse?> PostRawAsync<TResponse>(
        string url,
        object requestBody,
        JsonTypeInfo requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(requestBody, requestTypeInfo);
        using var response = await _http.PostAsync(CreateRequestUri(url), content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return JsonSerializer.Deserialize(body, responseTypeInfo);
    }

    private async Task<TResponse?> PutAsync<TResponse>(
        string url,
        object requestBody,
        JsonTypeInfo requestTypeInfo,
        JsonTypeInfo<ApiResponse<TResponse>> responseTypeInfo,
        CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(requestBody, requestTypeInfo);
        using var response = await _http.PutAsync(CreateRequestUri(url), content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, responseTypeInfo);
        return envelope is not null ? envelope.Data : default;
    }

    private async Task DeleteEnvelopeAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.DeleteAsync(
            CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Request failed";
        throw new HonuaAdminApiException(response.StatusCode, message, body);
    }

    private static void EnsureEnvelopeSucceeded(HttpResponseMessage response, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (doc.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                var message = TryExtractErrorMessage(body) ?? "API response indicated failure.";
                throw new HonuaAdminApiException(response.StatusCode, message, body);
            }
        }
        catch (JsonException)
        {
            // Not JSON or invalid JSON envelope, ignore.
        }
    }

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                // Admin envelope shape: { "message": "..." }.
                return msg.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON, that's fine.
            return null;
        }

        // RFC 7807 problem details via the shared Abstractions parser (detail, then title).
        return Honua.Sdk.Abstractions.HonuaProblemDetailsParser.TryParse(body, out var problem)
            ? problem?.Detail ?? problem?.Title
            : null;
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private static string? GetETag(HttpResponseMessage response) => response.Headers.ETag?.ToString();

    private static string BuildQuery(params ReadOnlySpan<(string Key, string? Value)> parameters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
    }

    private static string NormalizeSecureConnectionId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Connection ID is required.", parameterName);
        }

        if (!Guid.TryParse(id, out var parsed))
        {
            throw new ArgumentException("Connection ID must be a valid GUID.", parameterName);
        }

        return parsed.ToString("D");
    }
}
