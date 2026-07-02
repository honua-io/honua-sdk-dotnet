// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Studio.Internal;

namespace Honua.Sdk.Studio.Capabilities;

/// <summary>
/// HTTP client for the Honua server capability manifest.
/// </summary>
public sealed class HonuaCapabilityManifestClient : IHonuaCapabilityManifestClient
{
    private const string BasePath = "/api/v1/capabilities/manifest";
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaCapabilityManifestClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaCapabilityManifestClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<CapabilityManifest> GetManifestAsync(
        string? environment = null,
        string? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(environment, workspaceId);
        using var response = await _http
            .GetAsync(new Uri(BasePath + query, UriKind.RelativeOrAbsolute), cancellationToken)
            .ConfigureAwait(false);

        return await StudioHttpResponseReader.ReadAsync(
            response,
            CapabilityManifestJsonContext.Default.CapabilityManifest,
            "GetCapabilityManifest",
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildQuery(string? environment, string? workspaceId)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(environment))
        {
            parts.Add($"environment={Uri.EscapeDataString(environment)}");
        }

        if (!string.IsNullOrWhiteSpace(workspaceId))
        {
            parts.Add($"workspaceId={Uri.EscapeDataString(workspaceId)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
