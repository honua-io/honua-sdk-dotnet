// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Client interface for the Honua Admin REST API.
/// </summary>
public interface IHonuaAdminClient
{
    // Services

    /// <summary>
    /// Lists all registered services.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of service summaries.</returns>
    Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the settings for a specific service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service settings.</returns>
    Task<ServiceSettingsResponse> GetServiceSettingsAsync(string serviceName, CancellationToken ct = default);

    /// <summary>
    /// Updates the enabled protocols for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="protocols">The protocols to enable.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateProtocolsAsync(string serviceName, IReadOnlyList<string> protocols, CancellationToken ct = default);

    /// <summary>
    /// Updates the MapServer rendering settings for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="request">The MapServer settings to update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateMapServerSettingsAsync(string serviceName, UpdateMapServerSettingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates the access policy for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="request">The access policy update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateAccessPolicyAsync(string serviceName, UpdateAccessPolicyRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates temporal metadata for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="request">The time info update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateTimeInfoAsync(string serviceName, UpdateTimeInfoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates metadata for a specific layer within a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="request">The layer metadata update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated layer metadata.</returns>
    Task<LayerMetadataResponse> UpdateLayerMetadataAsync(string serviceName, int layerId, UpdateLayerMetadataRequest request, CancellationToken ct = default);

    // Metadata Resources

    /// <summary>
    /// Lists metadata resources, optionally filtered by kind and namespace.
    /// </summary>
    /// <param name="kind">Optional resource kind filter.</param>
    /// <param name="ns">Optional namespace filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of metadata resources.</returns>
    Task<IReadOnlyList<MetadataResource>> ListMetadataResourcesAsync(string? kind = null, string? ns = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a specific metadata resource by its identifier.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the resource and its ETag (if present).</returns>
    Task<(MetadataResource Resource, string? ETag)> GetMetadataResourceAsync(string kind, string ns, string name, CancellationToken ct = default);

    /// <summary>
    /// Creates a new metadata resource.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created resource.</returns>
    Task<MetadataResource> CreateMetadataResourceAsync(MetadataResource resource, CancellationToken ct = default);

    /// <summary>
    /// Creates a new metadata resource and returns transport metadata.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created resource and its ETag, if present.</returns>
    Task<MetadataResourceResponse> CreateMetadataResourceWithResponseAsync(MetadataResource resource, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support metadata resources.");
    }

    /// <summary>
    /// Updates an existing metadata resource.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="resource">The updated resource.</param>
    /// <param name="ifMatch">Optional ETag for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated resource.</returns>
    Task<MetadataResource> UpdateMetadataResourceAsync(string kind, string ns, string name, MetadataResource resource, string? ifMatch = null, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing metadata resource and returns transport metadata.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="resource">The updated resource.</param>
    /// <param name="ifMatch">Optional ETag for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated resource and its ETag, if present.</returns>
    Task<MetadataResourceResponse> UpdateMetadataResourceWithResponseAsync(string kind, string ns, string name, MetadataResource resource, string? ifMatch = null, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support metadata resources.");
    }

    /// <summary>
    /// Deletes a metadata resource.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="ifMatch">Optional ETag for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteMetadataResourceAsync(string kind, string ns, string name, string? ifMatch = null, CancellationToken ct = default);

    // Manifests

    /// <summary>
    /// Gets the admin API version information.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The version information.</returns>
    Task<AdminVersionResponse> GetVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the admin API capabilities.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The capabilities response.</returns>
    Task<AdminCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether the connected server is supported by this SDK baseline.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A compatibility result containing support status and coarse feature metadata.</returns>
    Task<ServerCompatibilityResult> CheckCompatibilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Exports the metadata manifest.
    /// </summary>
    /// <param name="ns">Optional namespace filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The metadata manifest.</returns>
    Task<MetadataManifest> GetManifestAsync(string? ns = null, CancellationToken ct = default);

    /// <summary>
    /// Applies a metadata manifest.
    /// </summary>
    /// <param name="request">The manifest apply request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The manifest apply result.</returns>
    Task<ManifestApplyResult> ApplyManifestAsync(ManifestApplyRequest request, CancellationToken ct = default);

    // Connections

    /// <summary>
    /// Lists all secure database connections.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of connection summaries.</returns>
    Task<IReadOnlyList<SecureConnectionSummary>> ListConnectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information about a secure database connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connection details.</returns>
    Task<SecureConnectionDetail> GetConnectionAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Creates a new secure database connection.
    /// </summary>
    /// <param name="request">The connection creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created connection summary.</returns>
    Task<SecureConnectionSummary> CreateConnectionAsync(CreateSecureConnectionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Tests a draft connection before saving.
    /// </summary>
    /// <param name="request">The connection details to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<ConnectionTestResult> TestDraftConnectionAsync(CreateSecureConnectionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing secure database connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="request">The connection update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated connection summary.</returns>
    Task<SecureConnectionSummary> UpdateConnectionAsync(string id, UpdateSecureConnectionRequest request, CancellationToken ct = default);

    /// <summary>
    /// Tests the health of an existing connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<ConnectionTestResult> TestConnectionAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Deletes a secure database connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteConnectionAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Validates the encryption service.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<EncryptionValidationResult> ValidateEncryptionAsync(CancellationToken ct = default);

    /// <summary>
    /// Rotates the encryption key.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The key rotation result.</returns>
    Task<KeyRotationResult> RotateEncryptionKeyAsync(CancellationToken ct = default);

    // Layers

    /// <summary>
    /// Lists published layers for a connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="serviceName">Optional service name filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of published layer summaries.</returns>
    Task<IReadOnlyList<PublishedLayerSummary>> ListLayersAsync(string connectionId, string? serviceName = null, CancellationToken ct = default);

    /// <summary>
    /// Publishes a PostGIS table as a layer.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="request">The publish layer request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The published layer summary.</returns>
    Task<PublishedLayerSummary> PublishLayerAsync(string connectionId, PublishLayerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables a specific layer.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="enabled">Whether the layer should be enabled.</param>
    /// <param name="serviceName">Optional service name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated layer summary.</returns>
    Task<PublishedLayerSummary> SetLayerEnabledAsync(string connectionId, int layerId, bool enabled, string? serviceName = null, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables all layers for a service.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="enabled">Whether the layers should be enabled.</param>
    /// <param name="serviceName">Optional service name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of updated layer summaries.</returns>
    Task<IReadOnlyList<PublishedLayerSummary>> SetServiceLayersEnabledAsync(string connectionId, bool enabled, string? serviceName = null, CancellationToken ct = default);

    // Discovery

    /// <summary>
    /// Discovers PostGIS tables available on a connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The table discovery response.</returns>
    Task<TableDiscoveryResponse> DiscoverTablesAsync(string connectionId, CancellationToken ct = default);

    // Styles

    /// <summary>
    /// Gets the style for a layer.
    /// </summary>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The layer style response.</returns>
    Task<LayerStyleResponse> GetLayerStyleAsync(int layerId, CancellationToken ct = default);

    /// <summary>
    /// Updates the style for a layer.
    /// </summary>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="request">The style update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated layer style response.</returns>
    Task<LayerStyleResponse> UpdateLayerStyleAsync(int layerId, LayerStyleUpdateRequest request, CancellationToken ct = default);

    // Config

    /// <summary>
    /// Gets the server configuration documentation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The configuration as a JSON element.</returns>
    Task<JsonElement> GetConfigAsync(CancellationToken ct = default);

    // Identity

    /// <summary>
    /// Lists configured OIDC providers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of OIDC providers.</returns>
    Task<IReadOnlyList<OidcProviderResponse>> ListOidcProvidersAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Gets a configured OIDC provider by identifier.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider, or null when the server returns 404.</returns>
    Task<OidcProviderResponse?> GetOidcProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Creates a configured OIDC provider.
    /// </summary>
    /// <param name="request">Provider create request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created provider.</returns>
    Task<OidcProviderResponse> CreateOidcProviderAsync(CreateOidcProviderRequest request, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Updates a configured OIDC provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="request">Provider update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated provider.</returns>
    Task<OidcProviderResponse> UpdateOidcProviderAsync(Guid providerId, UpdateOidcProviderRequest request, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Deletes a configured OIDC provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteOidcProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Tests a configured OIDC provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider test result.</returns>
    Task<OidcProviderTestResponse> TestOidcProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Gets identity provider catalog status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The identity provider catalog response.</returns>
    Task<IdentityProvidersResponse> GetIdentityProvidersAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    /// <summary>
    /// Tests a provider type from the identity provider catalog.
    /// </summary>
    /// <param name="providerType">Provider type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The provider test result.</returns>
    Task<IdentityProviderTestResult> TestIdentityProviderAsync(string providerType, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support identity administration.");
    }

    // License

    /// <summary>
    /// Gets the active license status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active license status.</returns>
    Task<LicenseStatusResponse> GetLicenseStatusAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support license administration.");
    }

    /// <summary>
    /// Gets active license entitlements.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active license entitlements.</returns>
    Task<IReadOnlyList<LicenseEntitlement>> GetLicenseEntitlementsAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support license administration.");
    }

    /// <summary>
    /// Uploads a replacement license file.
    /// </summary>
    /// <param name="bytes">License file bytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The refreshed license status.</returns>
    Task<LicenseStatusResponse> UploadLicenseAsync(byte[] bytes, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support license administration.");
    }

    // Observability

    /// <summary>
    /// Gets recent errors from the server.
    /// </summary>
    /// <param name="limit">Optional maximum number of errors to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of recent errors.</returns>
    Task<IReadOnlyList<RecentError>> GetRecentErrorsAsync(int? limit = null, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support recent errors.");
    }

    /// <summary>
    /// Gets recent errors from the server, including response metadata.
    /// </summary>
    /// <param name="limit">Optional maximum number of errors to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recent-errors response payload.</returns>
    Task<RecentErrorsResponse> GetRecentErrorsResponseAsync(int? limit = null, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support recent errors.");
    }

    /// <summary>
    /// Gets the current telemetry subsystem status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The telemetry status.</returns>
    Task<TelemetryStatus> GetTelemetryStatusAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support telemetry status.");
    }

    /// <summary>
    /// Gets the current database migration status.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The migration status.</returns>
    Task<MigrationStatus> GetMigrationStatusAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support migration status.");
    }

    // Deploy Control

    /// <summary>
    /// Runs preflight checks to determine deployment readiness.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preflight check result.</returns>
    Task<DeployPreflightResult> GetDeployPreflightAsync(CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy preflight checks.");
    }

    /// <summary>
    /// Runs preflight checks to determine deployment readiness.
    /// </summary>
    /// <param name="includeDiagnostics">Whether to include diagnostic detail in the response.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preflight check result.</returns>
    Task<DeployPreflightResult> GetDeployPreflightAsync(bool includeDiagnostics, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy preflight checks.");
    }

    /// <summary>
    /// Creates a new deploy plan.
    /// </summary>
    /// <param name="request">The deploy plan creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created deploy plan.</returns>
    Task<DeployPlan> CreateDeployPlanAsync(CreateDeployPlanRequest request, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy plans.");
    }

    /// <summary>
    /// Creates a new deploy operation from a plan.
    /// </summary>
    /// <param name="request">The deploy operation creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created deploy operation.</returns>
    Task<DeployOperation> CreateDeployOperationAsync(CreateDeployOperationRequest request, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy operations.");
    }

    /// <summary>
    /// Gets the status of a deploy operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deploy operation.</returns>
    Task<DeployOperation> GetDeployOperationAsync(string operationId, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy operations.");
    }

    /// <summary>
    /// Submits a deploy operation for execution.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> SubmitDeployOperationAsync(string operationId, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy operations.");
    }

    /// <summary>
    /// Submits a deploy operation for execution.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="request">The submit request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> SubmitDeployOperationAsync(string operationId, SubmitDeployOperationRequest request, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy operations.");
    }

    /// <summary>
    /// Rolls back a deploy operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> RollbackDeployOperationAsync(string operationId, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy operations.");
    }

    /// <summary>
    /// Rolls back a deploy operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="request">The rollback request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> RollbackDeployOperationAsync(string operationId, RollbackDeployOperationRequest request, CancellationToken ct = default)
    {
        throw new NotSupportedException("This IHonuaAdminClient implementation does not support deploy operations.");
    }
}
