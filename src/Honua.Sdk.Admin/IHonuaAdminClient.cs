// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin;

/// <summary>
/// Aggregate client interface for the Honua Admin REST API.
/// </summary>
/// <remarks>
/// <para>
/// This is a composition of role-segregated sub-interfaces. Prefer depending on
/// the narrowest sub-interface (for example, <see cref="IHonuaAdminConnectionsClient"/>
/// or <see cref="IHonuaAdminDeployClient"/>) at call sites; <see cref="IHonuaAdminClient"/>
/// itself is retained for compatibility and for callers that genuinely need the
/// full surface.
/// </para>
/// <para>
/// All sub-interfaces are also registered in the DI container by
/// <see cref="Extensions.ServiceCollectionExtensions.AddHonuaAdmin"/>, resolving to
/// the same underlying <see cref="HonuaAdminClient"/> instance.
/// </para>
/// </remarks>
public interface IHonuaAdminClient :
    IHonuaAdminServicesClient,
    IHonuaAdminMetadataClient,
    IHonuaAdminCompatibilityClient,
    IHonuaAdminManifestClient,
    IHonuaAdminConnectionsClient,
    IHonuaAdminLayersClient,
    IHonuaAdminStylesClient,
    IHonuaAdminConfigClient,
    IHonuaAdminIdentityClient,
    IHonuaAdminRolesClient,
    IHonuaAdminUsersClient,
    IHonuaAdminLicenseClient,
    IHonuaAdminObservabilityClient,
    IHonuaAdminAlertsClient,
    IHonuaAdminFeatureEventsClient,
    IHonuaAdminStreamingOperationsClient,
    IHonuaAdminDeployClient,
    IHonuaAdminRasterImportClient
{
}
