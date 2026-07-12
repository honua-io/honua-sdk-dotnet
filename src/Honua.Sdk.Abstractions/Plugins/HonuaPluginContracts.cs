// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Plugins;

/// <summary>
/// Host-neutral plugin manifest shared by server, admin, mobile, and web runtimes.
/// </summary>
public sealed class HonuaPluginManifest
{
    /// <summary>
    /// Current plugin manifest schema version supported by this SDK.
    /// </summary>
    public const string CurrentSchemaVersion = "honua.plugin.v1";

    private static readonly HonuaPluginJsonContext ReadJsonContext = new(CreateJsonOptions(writeIndented: false));
    private static readonly HonuaPluginJsonContext WriteJsonContext = new(CreateJsonOptions(writeIndented: false));
    private static readonly HonuaPluginJsonContext IndentedWriteJsonContext = new(CreateJsonOptions(writeIndented: true));

    // Source-generated deserialization supplies null for absent init-only reference values.
    // Coalescing setters preserve the models' existing non-null default invariants.
    private HonuaPluginCompatibility _compatibility = new();
    private IReadOnlyList<string> _capabilities = Array.Empty<string>();
    private IReadOnlyList<HonuaPluginPermissionDeclaration> _permissions =
        Array.Empty<HonuaPluginPermissionDeclaration>();
    private HonuaPluginConfigurationEnvelope _configuration = new();
    private IReadOnlyList<HonuaPluginExtensionPoint> _extensions = Array.Empty<HonuaPluginExtensionPoint>();
    private IReadOnlyDictionary<string, string> _metadata = new Dictionary<string, string>();

    /// <summary>
    /// Manifest schema version. Must be <see cref="CurrentSchemaVersion"/>.
    /// </summary>
    public string? SchemaVersion { get; init; }

    /// <summary>
    /// Stable plugin identifier. Reverse-DNS style identifiers are recommended.
    /// </summary>
    public string? PluginId { get; init; }

    /// <summary>
    /// Human-readable plugin name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Plugin publisher or organization.
    /// </summary>
    public string? Publisher { get; init; }

    /// <summary>
    /// Plugin version advertised by the publisher.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Optional short description for catalogs or audits.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Minimum product edition required to enable this plugin.
    /// </summary>
    public string? EditionGate { get; init; }

    /// <summary>
    /// Host, SDK, server, and feature-flag compatibility requirements.
    /// </summary>
    public HonuaPluginCompatibility Compatibility
    {
        get => _compatibility;
        init => _compatibility = value ?? new HonuaPluginCompatibility();
    }

    /// <summary>
    /// Capability flags advertised by this plugin.
    /// </summary>
    public IReadOnlyList<string> Capabilities
    {
        get => _capabilities;
        init => _capabilities = value ?? Array.Empty<string>();
    }

    /// <summary>
    /// Permissions requested by this plugin.
    /// </summary>
    public IReadOnlyList<HonuaPluginPermissionDeclaration> Permissions
    {
        get => _permissions;
        init => _permissions = value ?? Array.Empty<HonuaPluginPermissionDeclaration>();
    }

    /// <summary>
    /// Safe configuration envelope accepted by host runtimes.
    /// </summary>
    public HonuaPluginConfigurationEnvelope Configuration
    {
        get => _configuration;
        init => _configuration = value ?? new HonuaPluginConfigurationEnvelope();
    }

    /// <summary>
    /// Non-UI extension points implemented by this plugin.
    /// </summary>
    public IReadOnlyList<HonuaPluginExtensionPoint> Extensions
    {
        get => _extensions;
        init => _extensions = value ?? Array.Empty<HonuaPluginExtensionPoint>();
    }

    /// <summary>
    /// Optional opaque metadata for catalogs and operators.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata
    {
        get => _metadata;
        init => _metadata = value ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Parses a UTF-8 JSON plugin manifest into the shared SDK model.
    /// </summary>
    /// <param name="json">Manifest JSON.</param>
    /// <returns>The parsed plugin manifest.</returns>
    /// <exception cref="FormatException">The manifest is empty, malformed, or not a JSON object.</exception>
    public static HonuaPluginManifest ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FormatException("Plugin manifest JSON is required.");
        }

        try
        {
            return JsonSerializer.Deserialize(json, ReadJsonContext.HonuaPluginManifest)
                ?? throw new FormatException("Plugin manifest JSON did not contain an object.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Plugin manifest JSON was malformed.", ex);
        }
    }

    /// <summary>
    /// Serializes this manifest as UTF-8 compatible JSON.
    /// </summary>
    /// <param name="writeIndented">Whether to format the JSON with indentation.</param>
    /// <returns>Serialized manifest JSON.</returns>
    public string ToJson(bool writeIndented = false)
        => JsonSerializer.Serialize(
            this,
            writeIndented ? IndentedWriteJsonContext.HonuaPluginManifest : WriteJsonContext.HonuaPluginManifest);

    /// <summary>
    /// Validates this manifest using SDK-owned host-neutral rules.
    /// </summary>
    /// <returns>Validation result with warnings and blocking errors.</returns>
    public HonuaPluginValidationResult Validate()
        => HonuaPluginManifestValidator.Validate(this);

    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented)
        => new()
        {
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = writeIndented,
        };
}

/// <summary>
/// Compatibility envelope for plugin host runtimes.
/// </summary>
public sealed class HonuaPluginCompatibility
{
    private IReadOnlyList<string> _supportedHosts = Array.Empty<string>();
    private IReadOnlyList<string> _requiredFeatureFlags = Array.Empty<string>();

    /// <summary>
    /// Host kinds allowed to load this plugin.
    /// </summary>
    public IReadOnlyList<string> SupportedHosts
    {
        get => _supportedHosts;
        init => _supportedHosts = value ?? Array.Empty<string>();
    }

    /// <summary>
    /// Minimum Honua SDK version required by this plugin.
    /// </summary>
    public string? MinSdkVersion { get; init; }

    /// <summary>
    /// Maximum Honua SDK version supported by this plugin.
    /// </summary>
    public string? MaxSdkVersion { get; init; }

    /// <summary>
    /// Minimum Honua Server version required by this plugin.
    /// </summary>
    public string? MinServerVersion { get; init; }

    /// <summary>
    /// Maximum Honua Server version supported by this plugin.
    /// </summary>
    public string? MaxServerVersion { get; init; }

    /// <summary>
    /// Required server or host feature flags.
    /// </summary>
    public IReadOnlyList<string> RequiredFeatureFlags
    {
        get => _requiredFeatureFlags;
        init => _requiredFeatureFlags = value ?? Array.Empty<string>();
    }
}

/// <summary>
/// Permission declaration requested by a plugin.
/// </summary>
public sealed class HonuaPluginPermissionDeclaration
{
    /// <summary>
    /// Permission scope requested by the plugin.
    /// </summary>
    public string? Permission { get; init; }

    /// <summary>
    /// Requested access level for the permission.
    /// </summary>
    public string? Access { get; init; }

    /// <summary>
    /// Whether the plugin cannot run without this permission.
    /// </summary>
    public bool Required { get; init; } = true;

    /// <summary>
    /// Human-readable reason shown to operators and reviewers.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Configuration envelope that hosts may expose to operators without loading plugin code.
/// </summary>
public sealed class HonuaPluginConfigurationEnvelope
{
    private IReadOnlyList<HonuaPluginConfigurationField> _fields =
        Array.Empty<HonuaPluginConfigurationField>();
    private IReadOnlyDictionary<string, JsonElement> _defaults = new Dictionary<string, JsonElement>();

    /// <summary>
    /// Maximum serialized configuration size accepted by host runtimes.
    /// </summary>
    public int MaxSerializedBytes { get; init; } = 32 * 1024;

    /// <summary>
    /// Declared configuration fields.
    /// </summary>
    public IReadOnlyList<HonuaPluginConfigurationField> Fields
    {
        get => _fields;
        init => _fields = value ?? Array.Empty<HonuaPluginConfigurationField>();
    }

    /// <summary>
    /// Non-secret default configuration values keyed by field key.
    /// </summary>
    public IReadOnlyDictionary<string, JsonElement> Defaults
    {
        get => _defaults;
        init => _defaults = value ?? new Dictionary<string, JsonElement>();
    }
}

/// <summary>
/// A single plugin configuration field declaration.
/// </summary>
public sealed class HonuaPluginConfigurationField
{
    private IReadOnlyList<string> _allowedValues = Array.Empty<string>();

    /// <summary>
    /// Stable configuration key.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Field type. Known values are in <see cref="HonuaPluginConfigurationTypes"/>.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Whether callers must provide this configuration value.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Whether hosts must treat this value as sensitive.
    /// </summary>
    public bool Sensitive { get; init; }

    /// <summary>
    /// Maximum allowed string length for string-like values.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    /// Allowed values for enum-like configuration fields.
    /// </summary>
    public IReadOnlyList<string> AllowedValues
    {
        get => _allowedValues;
        init => _allowedValues = value ?? Array.Empty<string>();
    }

    /// <summary>
    /// Optional field description for catalogs and operators.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Non-UI plugin extension point declaration.
/// </summary>
public sealed class HonuaPluginExtensionPoint
{
    /// <summary>
    /// Stable extension identifier within the plugin manifest.
    /// </summary>
    public string? ExtensionId { get; init; }

    /// <summary>
    /// Extension type. Known values are in <see cref="HonuaPluginExtensionTypes"/>.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Source, form, field, workflow, or event target for the extension.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Host-resolved symbolic handler key.
    /// </summary>
    public string? Handler { get; init; }

    /// <summary>
    /// Optional configuration field key used by this extension.
    /// </summary>
    public string? ConfigurationKey { get; init; }

    /// <summary>
    /// Relative execution order for hosts that compose multiple extensions.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Input payload contract consumed by the extension.
    /// </summary>
    public HonuaPluginDataContract? Input { get; init; }

    /// <summary>
    /// Output payload contract produced by the extension.
    /// </summary>
    public HonuaPluginDataContract? Output { get; init; }
}

/// <summary>
/// Schema reference and semantic payload tags for a plugin extension.
/// </summary>
public sealed class HonuaPluginDataContract
{
    private IReadOnlyList<string> _tags = Array.Empty<string>();

    /// <summary>
    /// JSON Schema, OpenAPI component, Protobuf type, or other stable schema reference.
    /// </summary>
    public string? SchemaRef { get; init; }

    /// <summary>
    /// Semantic payload tags understood by host runtimes.
    /// </summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        init => _tags = value ?? Array.Empty<string>();
    }
}

/// <summary>
/// Result of validating a plugin manifest.
/// </summary>
public sealed class HonuaPluginValidationResult
{
    /// <summary>
    /// Validation findings.
    /// </summary>
    public IReadOnlyList<HonuaPluginValidationIssue> Issues { get; init; } =
        Array.Empty<HonuaPluginValidationIssue>();

    /// <summary>
    /// Whether validation found no blocking errors.
    /// </summary>
    public bool IsValid => !Issues.Any(issue => issue.Severity == HonuaPluginValidationSeverity.Error);

    /// <summary>
    /// Whether validation found non-blocking warnings.
    /// </summary>
    public bool HasWarnings => Issues.Any(issue => issue.Severity == HonuaPluginValidationSeverity.Warning);
}

/// <summary>
/// A single plugin validation finding.
/// </summary>
public sealed class HonuaPluginValidationIssue
{
    /// <summary>
    /// Machine-readable issue code from <see cref="HonuaPluginValidationCodes"/>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable validation message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// JSON-style manifest path for the offending value.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Validation severity.
    /// </summary>
    public HonuaPluginValidationSeverity Severity { get; init; } = HonuaPluginValidationSeverity.Error;
}

/// <summary>
/// Severity for plugin manifest validation findings.
/// </summary>
public enum HonuaPluginValidationSeverity
{
    /// <summary>
    /// The plugin can load, but hosts should surface a warning or degraded state.
    /// </summary>
    Warning,

    /// <summary>
    /// The plugin manifest is not safe or compatible to load.
    /// </summary>
    Error,
}

/// <summary>
/// Well-known host kinds for plugin compatibility declarations.
/// </summary>
public static class HonuaPluginHostKinds
{
    /// <summary>
    /// Honua mobile host runtime.
    /// </summary>
    public const string Mobile = "mobile";

    /// <summary>
    /// Honua web or browser host runtime.
    /// </summary>
    public const string Web = "web";

    /// <summary>
    /// Honua Server host runtime.
    /// </summary>
    public const string Server = "server";

    /// <summary>
    /// Honua admin application host runtime.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Honua command-line or automation host runtime.
    /// </summary>
    public const string Cli = "cli";

    /// <summary>
    /// Honua background worker host runtime.
    /// </summary>
    public const string Worker = "worker";

    /// <summary>
    /// Returns whether <paramref name="hostKind"/> is understood by this SDK version.
    /// </summary>
    /// <param name="hostKind">Host kind to check.</param>
    /// <returns><see langword="true"/> if the host kind is supported.</returns>
    public static bool IsSupported(string? hostKind)
        => string.Equals(hostKind, Mobile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hostKind, Web, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hostKind, Server, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hostKind, Admin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hostKind, Cli, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(hostKind, Worker, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Well-known plugin edition gates.
/// </summary>
public static class HonuaPluginEditionGates
{
    /// <summary>
    /// Community edition.
    /// </summary>
    public const string Community = "community";

    /// <summary>
    /// Pro edition.
    /// </summary>
    public const string Pro = "pro";

    /// <summary>
    /// Enterprise edition.
    /// </summary>
    public const string Enterprise = "enterprise";

    /// <summary>
    /// Internal or operator-only edition gate.
    /// </summary>
    public const string Internal = "internal";

    /// <summary>
    /// Returns whether <paramref name="editionGate"/> is understood by this SDK version.
    /// </summary>
    /// <param name="editionGate">Edition gate to check.</param>
    /// <returns><see langword="true"/> if the edition gate is supported.</returns>
    public static bool IsSupported(string? editionGate)
        => string.Equals(editionGate, Community, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editionGate, Pro, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editionGate, Enterprise, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editionGate, Internal, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Well-known plugin permission access levels.
/// </summary>
public static class HonuaPluginPermissionAccess
{
    /// <summary>
    /// Read-only access.
    /// </summary>
    public const string Read = "read";

    /// <summary>
    /// Write or mutation access.
    /// </summary>
    public const string Write = "write";

    /// <summary>
    /// Invocation access for hooks or workflows.
    /// </summary>
    public const string Invoke = "invoke";

    /// <summary>
    /// Administrative management access.
    /// </summary>
    public const string Manage = "manage";

    /// <summary>
    /// Returns whether <paramref name="access"/> is understood by this SDK version.
    /// </summary>
    /// <param name="access">Permission access value.</param>
    /// <returns><see langword="true"/> if the access value is supported.</returns>
    public static bool IsSupported(string? access)
        => string.Equals(access, Read, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(access, Write, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(access, Invoke, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(access, Manage, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Well-known plugin extension point types.
/// </summary>
public static class HonuaPluginExtensionTypes
{
    /// <summary>
    /// Field validation extension.
    /// </summary>
    public const string FieldValidator = "field-validator";

    /// <summary>
    /// Calculated field extension.
    /// </summary>
    public const string CalculatedField = "calculated-field";

    /// <summary>
    /// Data transformation extension.
    /// </summary>
    public const string DataTransformer = "data-transformer";

    /// <summary>
    /// Workflow hook extension.
    /// </summary>
    public const string WorkflowHook = "workflow-hook";

    /// <summary>
    /// Returns whether <paramref name="extensionType"/> is understood by this SDK version.
    /// </summary>
    /// <param name="extensionType">Extension type to check.</param>
    /// <returns><see langword="true"/> if the extension type is supported.</returns>
    public static bool IsSupported(string? extensionType)
        => string.Equals(extensionType, FieldValidator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extensionType, CalculatedField, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extensionType, DataTransformer, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extensionType, WorkflowHook, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Well-known plugin configuration field types.
/// </summary>
public static class HonuaPluginConfigurationTypes
{
    /// <summary>
    /// String configuration field.
    /// </summary>
    public const string Text = "string";

    /// <summary>
    /// Number configuration field.
    /// </summary>
    public const string Numeric = "number";

    /// <summary>
    /// Boolean configuration field.
    /// </summary>
    public const string Bool = "boolean";

    /// <summary>
    /// URI configuration field.
    /// </summary>
    public const string Uri = "uri";

    /// <summary>
    /// Enumerated string configuration field.
    /// </summary>
    public const string Enum = "enum";

    /// <summary>
    /// JSON object or array configuration field.
    /// </summary>
    public const string Json = "json";

    /// <summary>
    /// Returns whether <paramref name="configurationType"/> is understood by this SDK version.
    /// </summary>
    /// <param name="configurationType">Configuration field type to check.</param>
    /// <returns><see langword="true"/> if the configuration type is supported.</returns>
    public static bool IsSupported(string? configurationType)
        => string.Equals(configurationType, Text, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configurationType, Numeric, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configurationType, Bool, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configurationType, Uri, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configurationType, Enum, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(configurationType, Json, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Machine-readable validation issue codes for plugin manifests.
/// </summary>
public static class HonuaPluginValidationCodes
{
    /// <summary>Manifest schema version is not supported by this SDK.</summary>
    public const string UnsupportedSchemaVersion = "unsupported-schema-version";

    /// <summary>Manifest is missing a required value.</summary>
    public const string MissingRequiredValue = "missing-required-value";

    /// <summary>Manifest contains a malformed identifier or symbolic reference.</summary>
    public const string InvalidIdentifier = "invalid-identifier";

    /// <summary>Manifest contains a value that is too long for the shared contract.</summary>
    public const string ValueTooLong = "value-too-long";

    /// <summary>Manifest contains duplicate declarations.</summary>
    public const string DuplicateDeclaration = "duplicate-declaration";

    /// <summary>Manifest declares an unsupported host kind.</summary>
    public const string UnsupportedHost = "unsupported-host";

    /// <summary>Manifest declares an unsupported edition gate.</summary>
    public const string UnsupportedEditionGate = "unsupported-edition-gate";

    /// <summary>Manifest declares an unsupported permission access level.</summary>
    public const string UnsupportedPermissionAccess = "unsupported-permission-access";

    /// <summary>Manifest declares an unsupported extension point type.</summary>
    public const string UnsupportedExtensionType = "unsupported-extension-type";

    /// <summary>Manifest declares an unsupported configuration field type.</summary>
    public const string UnsupportedConfigurationType = "unsupported-configuration-type";

    /// <summary>Manifest configuration exceeds the maximum safe envelope size.</summary>
    public const string UnsafeConfigurationEnvelope = "unsafe-configuration-envelope";

    /// <summary>Manifest provides a default for a sensitive configuration field.</summary>
    public const string SensitiveDefaultValue = "sensitive-default-value";

    /// <summary>Manifest references an unknown configuration key.</summary>
    public const string UnknownConfigurationKey = "unknown-configuration-key";
}

/// <summary>
/// Validation helpers for host-neutral plugin manifests.
/// </summary>
public static class HonuaPluginManifestValidator
{
    private const int MaxIdentifierLength = 128;
    private const int MaxNameLength = 128;
    private const int MaxReasonLength = 256;
    private const int MaxDescriptionLength = 1024;
    private const int MaxMetadataEntries = 32;
    private const int MaxMetadataValueLength = 512;
    private const int MaxConfigurationBytes = 64 * 1024;
    private const int MaxConfigurationFields = 64;
    private const int MaxExtensionCount = 128;
    private const int MaxFieldLength = 4096;

    /// <summary>
    /// Validates a plugin manifest.
    /// </summary>
    /// <param name="manifest">Manifest to validate.</param>
    /// <returns>Validation result with warnings and blocking errors.</returns>
    public static HonuaPluginValidationResult Validate(HonuaPluginManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<HonuaPluginValidationIssue>();

        ValidateRequiredIdentifier(issues, manifest.PluginId, "$.pluginId", "Plugin ID is required.");
        ValidateRequiredText(issues, manifest.DisplayName, "$.displayName", "Display name is required.", MaxNameLength);
        ValidateRequiredText(issues, manifest.Publisher, "$.publisher", "Publisher is required.", MaxNameLength);
        ValidateRequiredText(issues, manifest.Version, "$.version", "Plugin version is required.", 64);
        ValidateOptionalText(issues, manifest.Description, "$.description", MaxDescriptionLength);
        ValidateSchema(issues, manifest.SchemaVersion);
        ValidateEditionGate(issues, manifest.EditionGate);
        ValidateCompatibility(issues, manifest.Compatibility);
        ValidateCapabilities(issues, manifest.Capabilities);
        ValidatePermissions(issues, manifest.Permissions);
        ValidateConfiguration(issues, manifest.Configuration);
        ValidateExtensions(issues, manifest.Extensions, manifest.Configuration);
        ValidateMetadata(issues, manifest.Metadata);

        return new HonuaPluginValidationResult { Issues = issues };
    }

    private static void ValidateSchema(ICollection<HonuaPluginValidationIssue> issues, string? schemaVersion)
    {
        if (!string.Equals(schemaVersion, HonuaPluginManifest.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.UnsupportedSchemaVersion,
                "Plugin manifest schema version is not supported.",
                "$.schemaVersion");
        }
    }

    private static void ValidateEditionGate(ICollection<HonuaPluginValidationIssue> issues, string? editionGate)
    {
        if (string.IsNullOrWhiteSpace(editionGate))
        {
            return;
        }

        if (!HonuaPluginEditionGates.IsSupported(editionGate))
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.UnsupportedEditionGate,
                "Plugin manifest edition gate is not supported.",
                "$.editionGate");
        }
    }

    private static void ValidateCompatibility(
        ICollection<HonuaPluginValidationIssue> issues,
        HonuaPluginCompatibility? compatibility)
    {
        if (compatibility is null)
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.MissingRequiredValue,
                "Plugin compatibility envelope is required.",
                "$.compatibility");
            return;
        }

        if (compatibility.SupportedHosts.Count == 0)
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.MissingRequiredValue,
                "At least one supported host kind is required.",
                "$.compatibility.supportedHosts");
        }

        ValidateUniqueIdentifiers(
            issues,
            compatibility.SupportedHosts,
            "$.compatibility.supportedHosts",
            HonuaPluginValidationCodes.UnsupportedHost,
            "Plugin manifest declares an unsupported host kind.",
            HonuaPluginHostKinds.IsSupported);

        ValidateUniqueIdentifiers(
            issues,
            compatibility.RequiredFeatureFlags,
            "$.compatibility.requiredFeatureFlags",
            HonuaPluginValidationCodes.InvalidIdentifier,
            "Required feature flag is not a safe identifier.",
            _ => true);

        ValidateOptionalVersion(issues, compatibility.MinSdkVersion, "$.compatibility.minSdkVersion");
        ValidateOptionalVersion(issues, compatibility.MaxSdkVersion, "$.compatibility.maxSdkVersion");
        ValidateOptionalVersion(issues, compatibility.MinServerVersion, "$.compatibility.minServerVersion");
        ValidateOptionalVersion(issues, compatibility.MaxServerVersion, "$.compatibility.maxServerVersion");
    }

    private static void ValidateCapabilities(ICollection<HonuaPluginValidationIssue> issues, IReadOnlyList<string> capabilities)
        => ValidateUniqueIdentifiers(
            issues,
            capabilities,
            "$.capabilities",
            HonuaPluginValidationCodes.InvalidIdentifier,
            "Plugin capability flag is not a safe identifier.",
            _ => true);

    private static void ValidatePermissions(
        ICollection<HonuaPluginValidationIssue> issues,
        IReadOnlyList<HonuaPluginPermissionDeclaration> permissions)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < permissions.Count; i++)
        {
            var permission = permissions[i];
            var path = $"$.permissions[{i}]";

            if (permission is null)
            {
                AddError(issues, HonuaPluginValidationCodes.MissingRequiredValue, "Permission declaration is required.", path);
                continue;
            }

            ValidateRequiredIdentifier(issues, permission.Permission, $"{path}.permission", "Permission scope is required.");
            ValidateRequiredText(issues, permission.Access, $"{path}.access", "Permission access is required.", MaxIdentifierLength);
            ValidateRequiredText(issues, permission.Reason, $"{path}.reason", "Permission reason is required.", MaxReasonLength);

            if (!string.IsNullOrWhiteSpace(permission.Access) && !HonuaPluginPermissionAccess.IsSupported(permission.Access))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.UnsupportedPermissionAccess,
                    "Plugin manifest declares an unsupported permission access level.",
                    $"{path}.access");
            }

            if (!string.IsNullOrWhiteSpace(permission.Permission) &&
                !string.IsNullOrWhiteSpace(permission.Access) &&
                !seen.Add($"{permission.Permission}:{permission.Access}"))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.DuplicateDeclaration,
                    "Plugin manifest contains a duplicate permission declaration.",
                    path);
            }
        }
    }

    private static void ValidateConfiguration(
        ICollection<HonuaPluginValidationIssue> issues,
        HonuaPluginConfigurationEnvelope? configuration)
    {
        if (configuration is null)
        {
            return;
        }

        if (configuration.MaxSerializedBytes <= 0 || configuration.MaxSerializedBytes > MaxConfigurationBytes)
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.UnsafeConfigurationEnvelope,
                "Plugin configuration envelope exceeds the maximum safe serialized size.",
                "$.configuration.maxSerializedBytes");
        }

        if (configuration.Fields.Count > MaxConfigurationFields)
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.UnsafeConfigurationEnvelope,
                "Plugin configuration envelope declares too many fields.",
                "$.configuration.fields");
        }

        var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < configuration.Fields.Count; i++)
        {
            var field = configuration.Fields[i];
            var path = $"$.configuration.fields[{i}]";

            if (field is null)
            {
                AddError(issues, HonuaPluginValidationCodes.MissingRequiredValue, "Configuration field is required.", path);
                continue;
            }

            ValidateRequiredIdentifier(issues, field.Key, $"{path}.key", "Configuration field key is required.");
            ValidateRequiredText(issues, field.Type, $"{path}.type", "Configuration field type is required.", MaxIdentifierLength);
            ValidateOptionalText(issues, field.Description, $"{path}.description", MaxMetadataValueLength);

            if (!string.IsNullOrWhiteSpace(field.Type) && !HonuaPluginConfigurationTypes.IsSupported(field.Type))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.UnsupportedConfigurationType,
                    "Plugin manifest declares an unsupported configuration field type.",
                    $"{path}.type");
            }

            if (field.MaxLength is <= 0 or > MaxFieldLength)
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.UnsafeConfigurationEnvelope,
                    "Configuration field max length is outside the safe range.",
                    $"{path}.maxLength");
            }

            ValidateUniqueIdentifiers(
                issues,
                field.AllowedValues,
                $"{path}.allowedValues",
                HonuaPluginValidationCodes.InvalidIdentifier,
                "Configuration allowed value is not a safe identifier.",
                _ => true);

            if (!string.IsNullOrWhiteSpace(field.Key))
            {
                if (!fieldKeys.Add(field.Key))
                {
                    AddError(
                        issues,
                        HonuaPluginValidationCodes.DuplicateDeclaration,
                        "Plugin manifest contains a duplicate configuration field key.",
                        $"{path}.key");
                }

                if (field.Sensitive)
                {
                    sensitiveKeys.Add(field.Key);
                }
            }
        }

        foreach (var defaultValue in configuration.Defaults)
        {
            var path = $"$.configuration.defaults.{defaultValue.Key}";

            if (!IsSafeIdentifier(defaultValue.Key))
            {
                AddError(issues, HonuaPluginValidationCodes.InvalidIdentifier, "Configuration default key is not safe.", path);
                continue;
            }

            if (!fieldKeys.Contains(defaultValue.Key))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.UnknownConfigurationKey,
                    "Configuration default references an unknown field key.",
                    path);
                continue;
            }

            if (sensitiveKeys.Contains(defaultValue.Key))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.SensitiveDefaultValue,
                    "Sensitive configuration fields must not declare default values.",
                    path);
            }
        }
    }

    private static void ValidateExtensions(
        ICollection<HonuaPluginValidationIssue> issues,
        IReadOnlyList<HonuaPluginExtensionPoint> extensions,
        HonuaPluginConfigurationEnvelope? configuration)
    {
        if (extensions.Count == 0)
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.MissingRequiredValue,
                "At least one plugin extension point is required.",
                "$.extensions");
            return;
        }

        if (extensions.Count > MaxExtensionCount)
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.UnsafeConfigurationEnvelope,
                "Plugin manifest declares too many extension points.",
                "$.extensions");
        }

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var configKeys = configuration?.Fields
            .Select(field => field?.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        for (var i = 0; i < extensions.Count; i++)
        {
            var extension = extensions[i];
            var path = $"$.extensions[{i}]";

            if (extension is null)
            {
                AddError(issues, HonuaPluginValidationCodes.MissingRequiredValue, "Extension declaration is required.", path);
                continue;
            }

            ValidateRequiredIdentifier(issues, extension.ExtensionId, $"{path}.extensionId", "Extension ID is required.");
            ValidateRequiredText(issues, extension.Type, $"{path}.type", "Extension type is required.", MaxIdentifierLength);
            ValidateRequiredText(issues, extension.Target, $"{path}.target", "Extension target is required.", MaxIdentifierLength);
            ValidateRequiredText(issues, extension.Handler, $"{path}.handler", "Extension handler is required.", MaxIdentifierLength);

            if (!string.IsNullOrWhiteSpace(extension.ExtensionId) && !seenIds.Add(extension.ExtensionId))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.DuplicateDeclaration,
                    "Plugin manifest contains a duplicate extension ID.",
                    $"{path}.extensionId");
            }

            if (!string.IsNullOrWhiteSpace(extension.Type) && !HonuaPluginExtensionTypes.IsSupported(extension.Type))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.UnsupportedExtensionType,
                    "Plugin manifest declares an unsupported extension point type.",
                    $"{path}.type");
            }

            if (!string.IsNullOrWhiteSpace(extension.Handler) && !IsSafeSymbol(extension.Handler))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.InvalidIdentifier,
                    "Extension handler is not a safe symbolic reference.",
                    $"{path}.handler");
            }

            if (!string.IsNullOrWhiteSpace(extension.ConfigurationKey) &&
                !configKeys.Contains(extension.ConfigurationKey))
            {
                AddError(
                    issues,
                    HonuaPluginValidationCodes.UnknownConfigurationKey,
                    "Extension references an unknown configuration key.",
                    $"{path}.configurationKey");
            }

            ValidateDataContract(issues, extension.Input, $"{path}.input");
            ValidateDataContract(issues, extension.Output, $"{path}.output");
        }
    }

    private static void ValidateDataContract(
        ICollection<HonuaPluginValidationIssue> issues,
        HonuaPluginDataContract? contract,
        string path)
    {
        if (contract is null)
        {
            return;
        }

        ValidateOptionalText(issues, contract.SchemaRef, $"{path}.schemaRef", 256);
        if (!string.IsNullOrWhiteSpace(contract.SchemaRef) && HasControlCharacters(contract.SchemaRef))
        {
            AddError(
                issues,
                HonuaPluginValidationCodes.InvalidIdentifier,
                "Data contract schema reference contains control characters.",
                $"{path}.schemaRef");
        }

        ValidateUniqueIdentifiers(
            issues,
            contract.Tags,
            $"{path}.tags",
            HonuaPluginValidationCodes.InvalidIdentifier,
            "Data contract tag is not a safe identifier.",
            _ => true);
    }

    private static void ValidateMetadata(
        ICollection<HonuaPluginValidationIssue> issues,
        IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count > MaxMetadataEntries)
        {
            AddWarning(
                issues,
                HonuaPluginValidationCodes.ValueTooLong,
                "Plugin metadata contains more entries than host catalogs should index.",
                "$.metadata");
        }

        foreach (var item in metadata)
        {
            var path = $"$.metadata.{item.Key}";

            if (!IsSafeIdentifier(item.Key))
            {
                AddError(issues, HonuaPluginValidationCodes.InvalidIdentifier, "Metadata key is not safe.", path);
            }

            ValidateOptionalText(issues, item.Value, path, MaxMetadataValueLength);
        }
    }

    private static void ValidateUniqueIdentifiers(
        ICollection<HonuaPluginValidationIssue> issues,
        IReadOnlyList<string> values,
        string path,
        string unsupportedCode,
        string unsupportedMessage,
        Func<string?, bool> isSupported)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            var itemPath = $"{path}[{i}]";

            if (!IsSafeIdentifier(value))
            {
                AddError(issues, HonuaPluginValidationCodes.InvalidIdentifier, "Value is not a safe identifier.", itemPath);
                continue;
            }

            if (!isSupported(value))
            {
                AddError(issues, unsupportedCode, unsupportedMessage, itemPath);
            }

            if (!seen.Add(value))
            {
                AddError(issues, HonuaPluginValidationCodes.DuplicateDeclaration, "Value is duplicated.", itemPath);
            }
        }
    }

    private static void ValidateRequiredIdentifier(
        ICollection<HonuaPluginValidationIssue> issues,
        string? value,
        string path,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(issues, HonuaPluginValidationCodes.MissingRequiredValue, message, path);
            return;
        }

        if (!IsSafeIdentifier(value))
        {
            AddError(issues, HonuaPluginValidationCodes.InvalidIdentifier, "Value is not a safe identifier.", path);
        }
    }

    private static void ValidateRequiredText(
        ICollection<HonuaPluginValidationIssue> issues,
        string? value,
        string path,
        string missingMessage,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(issues, HonuaPluginValidationCodes.MissingRequiredValue, missingMessage, path);
            return;
        }

        ValidateOptionalText(issues, value, path, maxLength);
    }

    private static void ValidateOptionalVersion(ICollection<HonuaPluginValidationIssue> issues, string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ValidateOptionalText(issues, value, path, 64);
        if (!IsSafeVersionToken(value))
        {
            AddError(issues, HonuaPluginValidationCodes.InvalidIdentifier, "Version token is not safe.", path);
        }
    }

    private static void ValidateOptionalText(
        ICollection<HonuaPluginValidationIssue> issues,
        string? value,
        string path,
        int maxLength)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length > maxLength)
        {
            AddError(issues, HonuaPluginValidationCodes.ValueTooLong, "Value is too long.", path);
        }

        if (HasControlCharacters(value))
        {
            AddError(issues, HonuaPluginValidationCodes.InvalidIdentifier, "Value contains control characters.", path);
        }
    }

    private static bool IsSafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdentifierLength || HasControlCharacters(value))
        {
            return false;
        }

        return value.All(static c =>
            char.IsAsciiLetterOrDigit(c) ||
            c == '.' ||
            c == '-' ||
            c == '_' ||
            c == ':');
    }

    private static bool IsSafeSymbol(string value)
    {
        if (value.Length > MaxIdentifierLength || HasControlCharacters(value))
        {
            return false;
        }

        return value.All(static c =>
            char.IsAsciiLetterOrDigit(c) ||
            c == '.' ||
            c == '-' ||
            c == '_' ||
            c == ':');
    }

    private static bool IsSafeVersionToken(string value)
        => value.All(static c =>
            char.IsAsciiLetterOrDigit(c) ||
            c == '.' ||
            c == '-' ||
            c == '+' ||
            c == '_');

    private static bool HasControlCharacters(string value)
        => value.Any(static c => char.IsControl(c));

    private static void AddError(
        ICollection<HonuaPluginValidationIssue> issues,
        string code,
        string message,
        string path)
        => issues.Add(new HonuaPluginValidationIssue
        {
            Code = code,
            Message = message,
            Path = path,
            Severity = HonuaPluginValidationSeverity.Error,
        });

    private static void AddWarning(
        ICollection<HonuaPluginValidationIssue> issues,
        string code,
        string message,
        string path)
        => issues.Add(new HonuaPluginValidationIssue
        {
            Code = code,
            Message = message,
            Path = path,
            Severity = HonuaPluginValidationSeverity.Warning,
        });
}
