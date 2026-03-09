// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Admin capabilities response payload exposing server compatibility metadata.
/// </summary>
public sealed class AdminCapabilitiesResponse
{
    /// <summary>
    /// Supported metadata API versions.
    /// </summary>
    [JsonPropertyName("metadataApiVersions")]
    public IReadOnlyList<string> MetadataApiVersions { get; init; } = [];

    /// <summary>
    /// Supported resource kinds.
    /// </summary>
    [JsonPropertyName("resourceKinds")]
    public IReadOnlyList<string> ResourceKinds { get; init; } = [];

    /// <summary>
    /// Indicates manifest export/apply support.
    /// </summary>
    [JsonPropertyName("manifestSupported")]
    public bool ManifestSupported { get; init; }

    /// <summary>
    /// Indicates whether dry-run is supported for manifest apply.
    /// </summary>
    [JsonPropertyName("manifestDryRunSupported")]
    public bool ManifestDryRunSupported { get; init; }

    /// <summary>
    /// Indicates whether prune is supported for manifest apply.
    /// </summary>
    [JsonPropertyName("manifestPruneSupported")]
    public bool ManifestPruneSupported { get; init; }

    /// <summary>
    /// Compatibility metadata advertised by the server.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public AdminCompatibilityInfo? Compatibility { get; init; }

    /// <summary>
    /// Server version string reported by the compatibility contract.
    /// </summary>
    [JsonIgnore]
    public string ServerVersion => Compatibility?.ServerVersion ?? string.Empty;

    /// <summary>
    /// Release channel reported by the server.
    /// </summary>
    [JsonIgnore]
    public string ReleaseChannel => Compatibility?.ReleaseChannel ?? string.Empty;

    /// <summary>
    /// Control-plane API compatibility metadata.
    /// </summary>
    [JsonIgnore]
    public ControlPlaneApiCompatibility ControlPlaneApi => Compatibility?.ControlPlaneApi ?? new();

    /// <summary>
    /// Metadata schema compatibility information.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<MetadataSchemaCompatibility> MetadataSchemas => Compatibility?.MetadataSchemas ?? [];

    /// <summary>
    /// Coarse-grained feature support advertised by the server.
    /// </summary>
    [JsonIgnore]
    public AdminFeatureCompatibility Features => Compatibility?.Features ?? new();
}

/// <summary>
/// Compatibility metadata returned by the admin capabilities contract.
/// </summary>
public sealed class AdminCompatibilityInfo
{
    /// <summary>
    /// Server version string.
    /// </summary>
    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; init; } = string.Empty;

    /// <summary>
    /// Release channel string such as stable, beta, or preview.
    /// </summary>
    [JsonPropertyName("releaseChannel")]
    public string ReleaseChannel { get; init; } = string.Empty;

    /// <summary>
    /// Control-plane API compatibility metadata.
    /// </summary>
    [JsonPropertyName("controlPlaneApi")]
    public ControlPlaneApiCompatibility ControlPlaneApi { get; init; } = new();

    /// <summary>
    /// Metadata schema compatibility metadata.
    /// </summary>
    [JsonPropertyName("metadataSchemas")]
    public IReadOnlyList<MetadataSchemaCompatibility> MetadataSchemas { get; init; } = [];

    /// <summary>
    /// Coarse-grained feature flags advertised by the server.
    /// </summary>
    [JsonPropertyName("features")]
    public AdminFeatureCompatibility Features { get; init; } = new();
}

/// <summary>
/// Control-plane API compatibility metadata.
/// </summary>
public sealed class ControlPlaneApiCompatibility
{
    /// <summary>
    /// Control-plane API major version.
    /// </summary>
    [JsonPropertyName("major")]
    public int Major { get; init; }

    /// <summary>
    /// Control-plane API base path served by the server.
    /// </summary>
    [JsonPropertyName("basePath")]
    public string BasePath { get; init; } = string.Empty;

    /// <summary>
    /// Whether the control-plane API surface is deprecated.
    /// </summary>
    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; init; }
}

/// <summary>
/// Metadata schema compatibility metadata.
/// </summary>
public sealed class MetadataSchemaCompatibility
{
    /// <summary>
    /// Schema version string.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Whether the schema version is deprecated.
    /// </summary>
    [JsonPropertyName("deprecated")]
    public bool Deprecated { get; init; }
}

/// <summary>
/// Coarse-grained feature flags advertised by the server.
/// </summary>
public sealed class AdminFeatureCompatibility
{
    /// <summary>
    /// Whether metadata resource endpoints are available.
    /// </summary>
    [JsonPropertyName("metadataResources")]
    public bool MetadataResources { get; init; }

    /// <summary>
    /// Whether metadata manifest export is available.
    /// </summary>
    [JsonPropertyName("manifestExport")]
    public bool ManifestExport { get; init; }

    /// <summary>
    /// Whether metadata manifest apply is available.
    /// </summary>
    [JsonPropertyName("manifestApply")]
    public bool ManifestApply { get; init; }

    /// <summary>
    /// Whether metadata manifest dry-run is available.
    /// </summary>
    [JsonPropertyName("manifestDryRun")]
    public bool ManifestDryRun { get; init; }

    /// <summary>
    /// Whether metadata manifest prune is available.
    /// </summary>
    [JsonPropertyName("manifestPrune")]
    public bool ManifestPrune { get; init; }
}

/// <summary>
/// Result of evaluating whether a server is supported by this SDK.
/// </summary>
public sealed class ServerCompatibilityResult
{
    /// <summary>
    /// Minimum server version supported by this SDK.
    /// </summary>
    public string MinimumSupportedServerVersion { get; init; } = HonuaAdminCompatibility.MinimumSupportedServerVersion;

    /// <summary>
    /// Minimum server release channel supported by this SDK baseline.
    /// </summary>
    public string MinimumSupportedReleaseChannel { get; init; } = HonuaAdminCompatibility.MinimumSupportedReleaseChannel;

    /// <summary>
    /// The capabilities payload returned by the server.
    /// </summary>
    public AdminCapabilitiesResponse Capabilities { get; init; } = new();

    /// <summary>
    /// Whether the connected server is supported by this SDK baseline.
    /// </summary>
    public bool IsSupported { get; init; }

    /// <summary>
    /// Optional reason explaining why the server is unsupported.
    /// </summary>
    public string? UnsupportedReason { get; init; }

    /// <summary>
    /// Server version string reported by the compatibility contract.
    /// </summary>
    public string ServerVersion => Capabilities.ServerVersion;

    /// <summary>
    /// Release channel reported by the server.
    /// </summary>
    public string ReleaseChannel => Capabilities.ReleaseChannel;

    /// <summary>
    /// Control-plane API compatibility metadata.
    /// </summary>
    public ControlPlaneApiCompatibility ControlPlaneApi => Capabilities.ControlPlaneApi;

    /// <summary>
    /// Metadata schema compatibility metadata.
    /// </summary>
    public IReadOnlyList<MetadataSchemaCompatibility> MetadataSchemas => Capabilities.MetadataSchemas;

    /// <summary>
    /// Coarse-grained feature support advertised by the server.
    /// </summary>
    public AdminFeatureCompatibility Features => Capabilities.Features;
}
