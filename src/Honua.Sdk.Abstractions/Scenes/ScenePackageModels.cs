// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Scenes;

/// <summary>
/// Offline 3D scene package manifest shared by server packaging and mobile runtimes.
/// </summary>
public sealed class HonuaScenePackageManifest
{
    /// <summary>
    /// Current manifest schema version supported by this SDK.
    /// </summary>
    public const string CurrentSchemaVersion = "honua.scene-package.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };
    private static readonly HonuaScenePackageJsonContext JsonContext = new(JsonOptions);

    /// <summary>
    /// Manifest schema version. Must be <see cref="CurrentSchemaVersion"/>.
    /// </summary>
    public string? SchemaVersion { get; init; }

    /// <summary>
    /// Stable package identifier used by local catalogs, resume, and eviction.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Scene identifier that this package renders offline.
    /// </summary>
    public string? SceneId { get; init; }

    /// <summary>
    /// Human-readable package name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Required product edition for using the package.
    /// </summary>
    public string? EditionGate { get; init; }

    /// <summary>
    /// Server scene revision used to invalidate previously downloaded packages.
    /// </summary>
    public string? ServerRevision { get; init; }

    /// <summary>
    /// Server timestamp for package generation.
    /// </summary>
    public DateTimeOffset? CreatedAtUtc { get; init; }

    /// <summary>
    /// Time after which the package is stale but can still render with visible stale state.
    /// </summary>
    public DateTimeOffset? StaleAfterUtc { get; init; }

    /// <summary>
    /// Time after which protected offline package content must not render without revalidation.
    /// </summary>
    public DateTimeOffset? OfflineUseExpiresAtUtc { get; init; }

    /// <summary>
    /// Expiry for download or refresh credentials. Public packages may omit this value.
    /// </summary>
    public DateTimeOffset? AuthExpiresAtUtc { get; init; }

    /// <summary>
    /// WGS84 extent covered by the package.
    /// </summary>
    public HonuaSceneBounds? Extent { get; init; }

    /// <summary>
    /// Level-of-detail or zoom range included in the package.
    /// </summary>
    public HonuaScenePackageLod? Lod { get; init; }

    /// <summary>
    /// Declared package byte budget.
    /// </summary>
    public HonuaScenePackageByteBudget? ByteBudget { get; init; }

    /// <summary>
    /// Attribution lines that must remain available offline.
    /// </summary>
    public IReadOnlyList<string> Attribution { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Complete set of assets in the package.
    /// </summary>
    public IReadOnlyList<HonuaScenePackageAsset> Assets { get; init; } = Array.Empty<HonuaScenePackageAsset>();

    /// <summary>
    /// Parses a UTF-8 JSON manifest document into the shared SDK model.
    /// </summary>
    public static HonuaScenePackageManifest ParseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FormatException("Offline scene package manifest JSON is required.");
        }

        try
        {
            return JsonSerializer.Deserialize(json, JsonContext.HonuaScenePackageManifest)
                ?? throw new FormatException("Offline scene package manifest JSON did not contain an object.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Offline scene package manifest JSON was malformed.", ex);
        }
    }

    /// <summary>
    /// Validates this manifest using package metadata only.
    /// </summary>
    public HonuaScenePackageValidationResult Validate(DateTimeOffset utcNow)
        => HonuaScenePackageManifestValidator.Validate(this, utcNow);

    /// <summary>
    /// Validates this manifest and marks missing required local assets as partial.
    /// </summary>
    public HonuaScenePackageValidationResult Validate(
        DateTimeOffset utcNow,
        IEnumerable<string>? availableAssetKeys)
        => HonuaScenePackageManifestValidator.Validate(this, utcNow, availableAssetKeys);
}

/// <summary>
/// Level-of-detail range for an offline scene package.
/// </summary>
public sealed class HonuaScenePackageLod
{
    /// <summary>
    /// Minimum zoom level included in the package.
    /// </summary>
    public int? MinZoom { get; init; }

    /// <summary>
    /// Maximum zoom level included in the package.
    /// </summary>
    public int? MaxZoom { get; init; }

    /// <summary>
    /// Optional renderer geometric-error ceiling included in the package, in meters.
    /// </summary>
    public double? MaxGeometricErrorMeters { get; init; }
}

/// <summary>
/// Package byte budget advertised before download.
/// </summary>
public sealed class HonuaScenePackageByteBudget
{
    /// <summary>
    /// Maximum allowed package size in bytes.
    /// </summary>
    public long? MaxPackageBytes { get; init; }

    /// <summary>
    /// Server-declared expected package bytes before download starts.
    /// </summary>
    public long? DeclaredBytes { get; init; }
}

/// <summary>
/// File or payload entry in an offline 3D scene package.
/// </summary>
public sealed class HonuaScenePackageAsset
{
    /// <summary>
    /// Stable asset key used for resume, validation, and local catalog records.
    /// </summary>
    public string? Key { get; init; }

    /// <summary>
    /// Cacheable asset type. Known values are in <see cref="HonuaScenePackageAssetTypes"/>.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Asset role, such as <c>metadata</c>, <c>primary-tileset</c>, or <c>terrain</c>.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Package-local relative path.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// HTTP content type expected for this asset.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Expected byte length for this asset.
    /// </summary>
    public long? Bytes { get; init; }

    /// <summary>
    /// SHA-256 digest encoded as base16 or base64.
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>
    /// Server ETag used for range resume or cache validation when available.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// Whether this asset must be present and hash-valid before the package can render.
    /// </summary>
    public bool Required { get; init; }
}

/// <summary>
/// Cacheable asset types for offline 3D scene packages.
/// </summary>
public static class HonuaScenePackageAssetTypes
{
    /// <summary>
    /// Resolved scene metadata, capability flags, bounds, and attribution.
    /// </summary>
    public const string SceneMetadata = "scene-metadata";

    /// <summary>
    /// Entry tileset JSON for a 3D Tiles endpoint.
    /// </summary>
    public const string ThreeDimensionalTileset = "3d-tileset";

    /// <summary>
    /// Nested 3D tile payload, subtree, model, binary, or texture content.
    /// </summary>
    public const string ThreeDimensionalTileContent = "3d-tile-content";

    /// <summary>
    /// Terrain mesh or raster tile.
    /// </summary>
    public const string TerrainTile = "terrain-tile";

    /// <summary>
    /// Shared texture not already embedded in 3D tile content.
    /// </summary>
    public const string Texture = "texture";

    /// <summary>
    /// Precomputed elevation profile samples.
    /// </summary>
    public const string ElevationProfile = "elevation-profile";

    /// <summary>
    /// Offline license or attribution file required by source data.
    /// </summary>
    public const string LicenseAttribution = "license-attribution";

    /// <summary>
    /// Returns whether <paramref name="assetType"/> is understood by this SDK version.
    /// </summary>
    public static bool IsSupported(string? assetType)
        => string.Equals(assetType, SceneMetadata, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetType, ThreeDimensionalTileset, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetType, ThreeDimensionalTileContent, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetType, TerrainTile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetType, Texture, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetType, ElevationProfile, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(assetType, LicenseAttribution, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Edition gates advertised by offline scene packages.
/// </summary>
public static class HonuaScenePackageEditionGates
{
    /// <summary>
    /// Community edition package.
    /// </summary>
    public const string Community = "community";

    /// <summary>
    /// Pro edition package.
    /// </summary>
    public const string Pro = "pro";

    /// <summary>
    /// Enterprise edition package.
    /// </summary>
    public const string Enterprise = "enterprise";

    /// <summary>
    /// Returns whether <paramref name="editionGate"/> is understood by this SDK version.
    /// </summary>
    public static bool IsSupported(string? editionGate)
        => string.Equals(editionGate, Community, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editionGate, Pro, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(editionGate, Enterprise, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Runtime state derived from manifest validation and local asset availability.
/// </summary>
public enum HonuaScenePackageState
{
    /// <summary>
    /// Required assets are present and the package is not stale or expired.
    /// </summary>
    Ready,

    /// <summary>
    /// Required assets are valid, but the package should refresh when connectivity returns.
    /// </summary>
    Stale,

    /// <summary>
    /// Offline use has expired and protected content must not render.
    /// </summary>
    Expired,

    /// <summary>
    /// Required local assets are missing.
    /// </summary>
    Partial,

    /// <summary>
    /// Manifest metadata is malformed or incompatible with this SDK.
    /// </summary>
    Invalid,
}

/// <summary>
/// Severity for manifest validation findings.
/// </summary>
public enum HonuaScenePackageValidationSeverity
{
    /// <summary>
    /// The package may still render, but should surface state or refresh guidance.
    /// </summary>
    Warning,

    /// <summary>
    /// The package is not renderable until the issue is corrected.
    /// </summary>
    Error,
}

/// <summary>
/// Well-known validation issue codes for offline scene package manifests.
/// </summary>
public static class HonuaScenePackageValidationCodes
{
    /// <summary>Manifest schema version is not supported by this SDK.</summary>
    public const string UnsupportedSchemaVersion = "unsupported-schema-version";

    /// <summary>Manifest is missing a package identifier.</summary>
    public const string MissingPackageId = "missing-package-id";

    /// <summary>Manifest is missing the scene identifier.</summary>
    public const string MissingSceneId = "missing-scene-id";

    /// <summary>Manifest edition gate is not supported by this SDK.</summary>
    public const string UnsupportedEditionGate = "unsupported-edition-gate";

    /// <summary>Manifest is missing the server revision.</summary>
    public const string MissingServerRevision = "missing-server-revision";

    /// <summary>Manifest is missing its creation timestamp.</summary>
    public const string MissingCreatedAt = "missing-created-at";

    /// <summary>Manifest is missing the stale-after timestamp.</summary>
    public const string MissingStaleAfter = "missing-stale-after";

    /// <summary>Manifest is missing the offline use expiry timestamp.</summary>
    public const string MissingOfflineUseExpiry = "missing-offline-use-expiry";

    /// <summary>Manifest expiry timestamps are not ordered correctly.</summary>
    public const string InvalidExpiryOrder = "invalid-expiry-order";

    /// <summary>Manifest extent is missing or outside a valid range.</summary>
    public const string InvalidExtent = "invalid-extent";

    /// <summary>Manifest level-of-detail range is missing or invalid.</summary>
    public const string InvalidLod = "invalid-lod";

    /// <summary>Manifest byte budget is missing or invalid.</summary>
    public const string InvalidByteBudget = "invalid-byte-budget";

    /// <summary>Declared package bytes exceed the advertised byte budget.</summary>
    public const string OverByteBudget = "over-byte-budget";

    /// <summary>Manifest has no asset entries.</summary>
    public const string MissingAssets = "missing-assets";

    /// <summary>Manifest contains a null asset entry.</summary>
    public const string NullAsset = "null-asset";

    /// <summary>Manifest is missing a required scene metadata asset.</summary>
    public const string MissingRequiredSceneMetadata = "missing-required-scene-metadata";

    /// <summary>Manifest is missing a required asset.</summary>
    public const string MissingRequiredAsset = "missing-required-asset";

    /// <summary>Manifest contains duplicate asset keys.</summary>
    public const string DuplicateAssetKey = "duplicate-asset-key";

    /// <summary>Manifest contains an unsupported asset type.</summary>
    public const string UnsupportedAssetType = "unsupported-asset-type";

    /// <summary>Manifest contains an unsafe or invalid asset path.</summary>
    public const string InvalidAssetPath = "invalid-asset-path";

    /// <summary>Manifest contains missing or invalid asset byte counts.</summary>
    public const string InvalidAssetBytes = "invalid-asset-bytes";

    /// <summary>Manifest contains missing or invalid asset hashes.</summary>
    public const string InvalidAssetHash = "invalid-asset-hash";

    /// <summary>Offline use for the scene package has expired.</summary>
    public const string OfflineUseExpired = "offline-use-expired";

    /// <summary>Download or refresh credentials for the package have expired.</summary>
    public const string AuthExpired = "auth-expired";

    /// <summary>Scene package content is stale but still renderable.</summary>
    public const string Stale = "stale";
}

/// <summary>
/// A single manifest validation finding.
/// </summary>
public sealed class HonuaScenePackageValidationIssue
{
    /// <summary>
    /// Machine-readable issue code from <see cref="HonuaScenePackageValidationCodes"/>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Human-readable validation message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Severity for this issue.
    /// </summary>
    public HonuaScenePackageValidationSeverity Severity { get; init; } = HonuaScenePackageValidationSeverity.Error;

    /// <summary>
    /// Asset key related to this issue, when applicable.
    /// </summary>
    public string? AssetKey { get; init; }
}

/// <summary>
/// Result of validating an offline scene package manifest.
/// </summary>
public sealed class HonuaScenePackageValidationResult
{
    /// <summary>
    /// Derived package state.
    /// </summary>
    public required HonuaScenePackageState State { get; init; }

    /// <summary>
    /// Validation findings.
    /// </summary>
    public IReadOnlyList<HonuaScenePackageValidationIssue> Issues { get; init; } =
        Array.Empty<HonuaScenePackageValidationIssue>();

    /// <summary>
    /// Whether validation found no blocking errors.
    /// </summary>
    public bool IsValid => !Issues.Any(issue => issue.Severity == HonuaScenePackageValidationSeverity.Error);

    /// <summary>
    /// Whether validation found non-blocking warnings.
    /// </summary>
    public bool HasWarnings => Issues.Any(issue => issue.Severity == HonuaScenePackageValidationSeverity.Warning);
}

/// <summary>
/// Validation helpers for offline scene package manifests.
/// </summary>
public static class HonuaScenePackageManifestValidator
{
    /// <summary>
    /// Validates a package manifest without checking local file availability.
    /// </summary>
    public static HonuaScenePackageValidationResult Validate(
        HonuaScenePackageManifest manifest,
        DateTimeOffset utcNow)
        => Validate(manifest, utcNow, availableAssetKeys: null);

    /// <summary>
    /// Validates a package manifest and optional local asset key set.
    /// </summary>
    public static HonuaScenePackageValidationResult Validate(
        HonuaScenePackageManifest manifest,
        DateTimeOffset utcNow,
        IEnumerable<string>? availableAssetKeys)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var issues = new List<HonuaScenePackageValidationIssue>();
        var invalid = false;
        var partial = false;
        var expired = false;
        var stale = false;
        var availableAssets = availableAssetKeys is null
            ? null
            : new HashSet<string>(
                availableAssetKeys.Where(key => !string.IsNullOrWhiteSpace(key)),
                StringComparer.OrdinalIgnoreCase);

        invalid |= ValidateIdentity(manifest, issues);
        invalid |= ValidateDates(manifest, utcNow, issues, ref expired, ref stale);
        invalid |= ValidateExtent(manifest.Extent, issues);
        invalid |= ValidateLod(manifest.Lod, issues);
        invalid |= ValidateByteBudget(manifest, issues);
        ValidateAssets(manifest, availableAssets, issues, ref invalid, ref partial);

        var state = invalid
            ? HonuaScenePackageState.Invalid
            : partial
                ? HonuaScenePackageState.Partial
                : expired
                    ? HonuaScenePackageState.Expired
                    : stale
                        ? HonuaScenePackageState.Stale
                        : HonuaScenePackageState.Ready;

        return new HonuaScenePackageValidationResult
        {
            State = state,
            Issues = issues,
        };
    }

    private static bool ValidateIdentity(
        HonuaScenePackageManifest manifest,
        ICollection<HonuaScenePackageValidationIssue> issues)
    {
        var invalid = false;

        if (!string.Equals(manifest.SchemaVersion, HonuaScenePackageManifest.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.UnsupportedSchemaVersion,
                $"Unsupported scene package schema version '{manifest.SchemaVersion ?? "<missing>"}'.");
            invalid = true;
        }

        invalid |= AddRequiredStringIssue(
            manifest.PackageId,
            HonuaScenePackageValidationCodes.MissingPackageId,
            "Scene package manifest is missing packageId.",
            issues);
        invalid |= AddRequiredStringIssue(
            manifest.SceneId,
            HonuaScenePackageValidationCodes.MissingSceneId,
            "Scene package manifest is missing sceneId.",
            issues);
        invalid |= AddRequiredStringIssue(
            manifest.ServerRevision,
            HonuaScenePackageValidationCodes.MissingServerRevision,
            "Scene package manifest is missing serverRevision.",
            issues);

        if (!HonuaScenePackageEditionGates.IsSupported(manifest.EditionGate))
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.UnsupportedEditionGate,
                $"Unsupported scene package edition gate '{manifest.EditionGate ?? "<missing>"}'.");
            invalid = true;
        }

        return invalid;
    }

    private static bool ValidateDates(
        HonuaScenePackageManifest manifest,
        DateTimeOffset utcNow,
        ICollection<HonuaScenePackageValidationIssue> issues,
        ref bool expired,
        ref bool stale)
    {
        var invalid = false;

        if (!manifest.CreatedAtUtc.HasValue)
        {
            AddError(issues, HonuaScenePackageValidationCodes.MissingCreatedAt, "Scene package manifest is missing createdAtUtc.");
            invalid = true;
        }

        if (!manifest.StaleAfterUtc.HasValue)
        {
            AddError(issues, HonuaScenePackageValidationCodes.MissingStaleAfter, "Scene package manifest is missing staleAfterUtc.");
            invalid = true;
        }

        if (!manifest.OfflineUseExpiresAtUtc.HasValue)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.MissingOfflineUseExpiry,
                "Scene package manifest is missing offlineUseExpiresAtUtc.");
            invalid = true;
        }

        if (manifest.StaleAfterUtc.HasValue &&
            manifest.OfflineUseExpiresAtUtc.HasValue &&
            manifest.StaleAfterUtc.Value > manifest.OfflineUseExpiresAtUtc.Value)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.InvalidExpiryOrder,
                "staleAfterUtc must be before or equal to offlineUseExpiresAtUtc.");
            invalid = true;
        }

        if (manifest.OfflineUseExpiresAtUtc.HasValue && utcNow >= manifest.OfflineUseExpiresAtUtc.Value)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.OfflineUseExpired,
                "Scene package offline use has expired.");
            expired = true;
        }
        else if (manifest.StaleAfterUtc.HasValue && utcNow >= manifest.StaleAfterUtc.Value)
        {
            AddWarning(issues, HonuaScenePackageValidationCodes.Stale, "Scene package content is stale.");
            stale = true;
        }

        if (manifest.AuthExpiresAtUtc.HasValue && utcNow >= manifest.AuthExpiresAtUtc.Value)
        {
            AddWarning(
                issues,
                HonuaScenePackageValidationCodes.AuthExpired,
                "Scene package download or refresh credentials have expired.");
        }

        return invalid;
    }

    private static bool ValidateExtent(
        HonuaSceneBounds? extent,
        ICollection<HonuaScenePackageValidationIssue> issues)
    {
        if (extent is null)
        {
            AddError(issues, HonuaScenePackageValidationCodes.InvalidExtent, "Scene package manifest is missing extent.");
            return true;
        }

        var invalid =
            extent.MinLongitude < -180 ||
            extent.MinLongitude > 180 ||
            extent.MaxLongitude < -180 ||
            extent.MaxLongitude > 180 ||
            extent.MinLatitude < -90 ||
            extent.MinLatitude > 90 ||
            extent.MaxLatitude < -90 ||
            extent.MaxLatitude > 90 ||
            extent.MinLongitude > extent.MaxLongitude ||
            extent.MinLatitude > extent.MaxLatitude ||
            (extent.MinHeight.HasValue && extent.MaxHeight.HasValue && extent.MinHeight.Value > extent.MaxHeight.Value);

        if (invalid)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.InvalidExtent,
                "Scene package extent must be a valid WGS84 bounding box.");
        }

        return invalid;
    }

    private static bool ValidateLod(
        HonuaScenePackageLod? lod,
        ICollection<HonuaScenePackageValidationIssue> issues)
    {
        if (lod is null)
        {
            AddError(issues, HonuaScenePackageValidationCodes.InvalidLod, "Scene package manifest is missing lod.");
            return true;
        }

        var invalid =
            !lod.MinZoom.HasValue ||
            !lod.MaxZoom.HasValue ||
            lod.MinZoom < 0 ||
            lod.MaxZoom < 0 ||
            lod.MinZoom > lod.MaxZoom ||
            lod.MaxGeometricErrorMeters < 0;

        if (invalid)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.InvalidLod,
                "Scene package lod must define a valid zoom range and non-negative geometric error.");
        }

        return invalid;
    }

    private static bool ValidateByteBudget(
        HonuaScenePackageManifest manifest,
        ICollection<HonuaScenePackageValidationIssue> issues)
    {
        if (manifest.ByteBudget is null)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.InvalidByteBudget,
                "Scene package manifest is missing byteBudget.");
            return true;
        }

        var invalid =
            !manifest.ByteBudget.MaxPackageBytes.HasValue ||
            !manifest.ByteBudget.DeclaredBytes.HasValue ||
            manifest.ByteBudget.MaxPackageBytes <= 0 ||
            manifest.ByteBudget.DeclaredBytes <= 0;

        if (invalid)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.InvalidByteBudget,
                "Scene package byteBudget must define positive maxPackageBytes and declaredBytes.");
            return true;
        }

        var maxPackageBytes = manifest.ByteBudget.MaxPackageBytes.GetValueOrDefault();
        var declaredBytes = manifest.ByteBudget.DeclaredBytes.GetValueOrDefault();
        var totalAssetBytes = 0L;
        var assetBytesOverflow = false;
        foreach (var asset in manifest.Assets)
        {
            if (asset?.Bytes is not > 0)
            {
                continue;
            }

            var bytes = asset.Bytes.Value;
            if (bytes > maxPackageBytes || totalAssetBytes > maxPackageBytes - bytes)
            {
                assetBytesOverflow = true;
                break;
            }

            totalAssetBytes += bytes;
        }

        var overBudget =
            declaredBytes > maxPackageBytes ||
            assetBytesOverflow ||
            totalAssetBytes > maxPackageBytes;

        if (overBudget)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.OverByteBudget,
                "Scene package declared or asset bytes exceed maxPackageBytes.");
        }

        return overBudget;
    }

    private static void ValidateAssets(
        HonuaScenePackageManifest manifest,
        HashSet<string>? availableAssetKeys,
        ICollection<HonuaScenePackageValidationIssue> issues,
        ref bool invalid,
        ref bool partial)
    {
        if (manifest.Assets.Count == 0)
        {
            AddError(issues, HonuaScenePackageValidationCodes.MissingAssets, "Scene package manifest has no assets.");
            invalid = true;
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasRequiredSceneMetadata = false;

        foreach (var asset in manifest.Assets)
        {
            if (asset is null)
            {
                AddError(
                    issues,
                    HonuaScenePackageValidationCodes.NullAsset,
                    "Scene package manifest contains a null asset entry.");
                invalid = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(asset.Key))
            {
                AddAssetError(issues, HonuaScenePackageValidationCodes.MissingRequiredAsset, asset, "Scene package asset is missing key.");
                invalid = true;
            }
            else if (!seen.Add(asset.Key))
            {
                AddAssetError(issues, HonuaScenePackageValidationCodes.DuplicateAssetKey, asset, "Scene package asset key is duplicated.");
                invalid = true;
            }

            if (!HonuaScenePackageAssetTypes.IsSupported(asset.Type))
            {
                AddAssetError(
                    issues,
                    HonuaScenePackageValidationCodes.UnsupportedAssetType,
                    asset,
                    $"Unsupported scene package asset type '{asset.Type ?? "<missing>"}'.");
                invalid = true;
            }

            if (!IsSafeRelativePath(asset.Path))
            {
                AddAssetError(
                    issues,
                    HonuaScenePackageValidationCodes.InvalidAssetPath,
                    asset,
                    "Scene package asset path must be package-local and relative.");
                invalid = true;
            }

            if (!asset.Bytes.HasValue || asset.Bytes <= 0)
            {
                AddAssetError(
                    issues,
                    HonuaScenePackageValidationCodes.InvalidAssetBytes,
                    asset,
                    "Scene package asset bytes must be positive.");
                invalid = true;
            }

            if (!IsValidSha256(asset.Sha256))
            {
                AddAssetError(
                    issues,
                    HonuaScenePackageValidationCodes.InvalidAssetHash,
                    asset,
                    "Scene package asset sha256 must be a base16 or base64 SHA-256 digest.");
                invalid = true;
            }

            if (asset.Required &&
                string.Equals(asset.Type, HonuaScenePackageAssetTypes.SceneMetadata, StringComparison.OrdinalIgnoreCase))
            {
                hasRequiredSceneMetadata = true;
            }

            if (asset.Required &&
                availableAssetKeys is not null &&
                !string.IsNullOrWhiteSpace(asset.Key) &&
                !availableAssetKeys.Contains(asset.Key))
            {
                AddAssetError(
                    issues,
                    HonuaScenePackageValidationCodes.MissingRequiredAsset,
                    asset,
                    "Required scene package asset is missing from local storage.");
                partial = true;
            }
        }

        if (!hasRequiredSceneMetadata)
        {
            AddError(
                issues,
                HonuaScenePackageValidationCodes.MissingRequiredSceneMetadata,
                "Scene package manifest must include a required scene-metadata asset.");
            invalid = true;
        }
    }

    private static bool AddRequiredStringIssue(
        string? value,
        string code,
        string message,
        ICollection<HonuaScenePackageValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        AddError(issues, code, message);
        return true;
    }

    private static bool IsSafeRelativePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.Contains('\\', StringComparison.Ordinal) ||
            path.StartsWith('/') ||
            Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            return false;
        }

        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .All(segment => segment is not "." and not "..");
    }

    private static bool IsValidSha256(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            return false;
        }

        var value = sha256.Trim();
        if (value.Length == 64 && value.All(Uri.IsHexDigit))
        {
            return true;
        }

        Span<byte> digest = stackalloc byte[32];
        return Convert.TryFromBase64String(value, digest, out var bytesWritten) && bytesWritten == 32;
    }

    private static void AddError(
        ICollection<HonuaScenePackageValidationIssue> issues,
        string code,
        string message)
        => issues.Add(new HonuaScenePackageValidationIssue
        {
            Code = code,
            Message = message,
            Severity = HonuaScenePackageValidationSeverity.Error,
        });

    private static void AddWarning(
        ICollection<HonuaScenePackageValidationIssue> issues,
        string code,
        string message)
        => issues.Add(new HonuaScenePackageValidationIssue
        {
            Code = code,
            Message = message,
            Severity = HonuaScenePackageValidationSeverity.Warning,
        });

    private static void AddAssetError(
        ICollection<HonuaScenePackageValidationIssue> issues,
        string code,
        HonuaScenePackageAsset asset,
        string message)
        => issues.Add(new HonuaScenePackageValidationIssue
        {
            Code = code,
            Message = message,
            Severity = HonuaScenePackageValidationSeverity.Error,
            AssetKey = asset.Key,
        });
}
