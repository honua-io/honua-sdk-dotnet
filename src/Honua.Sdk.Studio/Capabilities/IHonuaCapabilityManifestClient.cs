// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Capabilities;

/// <summary>
/// Typed client for the Honua server capability manifest
/// (<c>GET /api/v1/capabilities/manifest</c>). Lets a Console/MCP host gate
/// authoring UI and tool exposure on what the connected server actually supports
/// for the current tenant, workspace, environment, and caller policy scope.
/// </summary>
public interface IHonuaCapabilityManifestClient
{
    /// <summary>
    /// Fetches the capability manifest for the current caller scope.
    /// </summary>
    /// <param name="environment">Optional environment identifier to scope the manifest to.</param>
    /// <param name="workspaceId">Optional workspace identifier to scope the manifest to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The parsed capability manifest.</returns>
    Task<CapabilityManifest> GetManifestAsync(
        string? environment = null,
        string? workspaceId = null,
        CancellationToken cancellationToken = default);
}
