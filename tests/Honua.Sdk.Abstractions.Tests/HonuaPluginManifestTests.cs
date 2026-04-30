// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Plugins;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class HonuaPluginManifestTests
{
    [Fact]
    public void ParseJson_ValidFixture_DeserializesAndValidates()
    {
        var manifest = HonuaPluginManifest.ParseJson(ReadFixture("plugin-manifest.v1.json"));

        var result = manifest.Validate();

        Assert.True(result.IsValid, FormatIssues(result));
        Assert.False(result.HasWarnings, FormatIssues(result));
        Assert.Equal(HonuaPluginManifest.CurrentSchemaVersion, manifest.SchemaVersion);
        Assert.Equal("io.honua.plugins.asset-quality", manifest.PluginId);
        Assert.Equal(HonuaPluginEditionGates.Enterprise, manifest.EditionGate);
        Assert.Contains(HonuaPluginHostKinds.Mobile, manifest.Compatibility.SupportedHosts);
        Assert.Contains(HonuaPluginExtensionTypes.FieldValidator, manifest.Extensions.Select(extension => extension.Type));
        Assert.Contains(HonuaPluginExtensionTypes.CalculatedField, manifest.Extensions.Select(extension => extension.Type));
        Assert.Contains(HonuaPluginExtensionTypes.DataTransformer, manifest.Extensions.Select(extension => extension.Type));
        Assert.Contains(HonuaPluginExtensionTypes.WorkflowHook, manifest.Extensions.Select(extension => extension.Type));
    }

    [Fact]
    public void ToJson_RoundTripsManifest()
    {
        var manifest = HonuaPluginManifest.ParseJson(ReadFixture("plugin-manifest.v1.json"));

        var roundTripped = HonuaPluginManifest.ParseJson(manifest.ToJson(writeIndented: true));

        Assert.Equal(manifest.PluginId, roundTripped.PluginId);
        Assert.Equal(manifest.Permissions.Count, roundTripped.Permissions.Count);
        Assert.Equal(manifest.Extensions.Count, roundTripped.Extensions.Count);
        Assert.True(roundTripped.Validate().IsValid);
    }

    [Fact]
    public void ParseJson_MalformedJson_ThrowsFormatException()
    {
        var ex = Assert.Throws<FormatException>(() => HonuaPluginManifest.ParseJson("{ not-json"));

        Assert.Contains("malformed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_UnsafeManifest_ReturnsBlockingIssues()
    {
        var manifest = HonuaPluginManifest.ParseJson("""
            {
              "schemaVersion": "honua.plugin.v2",
              "pluginId": "bad plugin",
              "displayName": "Bad Plugin",
              "publisher": "Honua",
              "version": "0.1.0-alpha.1",
              "editionGate": "enterprise-plus",
              "compatibility": {
                "supportedHosts": ["mobile", "unknown-host", "mobile"],
                "requiredFeatureFlags": ["plugins"]
              },
              "permissions": [
                {
                  "permission": "features:read",
                  "access": "root",
                  "required": true,
                  "reason": "Needs everything."
                },
                {
                  "permission": "features:read",
                  "access": "root",
                  "required": true,
                  "reason": "Duplicate."
                }
              ],
              "configuration": {
                "maxSerializedBytes": 999999,
                "fields": [
                  {
                    "key": "apiKey",
                    "type": "string",
                    "sensitive": true
                  },
                  {
                    "key": "mode",
                    "type": "custom-widget"
                  }
                ],
                "defaults": {
                  "apiKey": "secret",
                  "missingKey": "value"
                }
              },
              "extensions": [
                {
                  "extensionId": "validate",
                  "type": "ui-component",
                  "target": "form:inspection.asset_id",
                  "handler": "assetQuality/validate",
                  "configurationKey": "missingKey"
                }
              ],
              "metadata": {
                "bad key": "value"
              }
            }
            """);

        var result = manifest.Validate();

        Assert.False(result.IsValid);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsupportedSchemaVersion);
        AssertHasCode(result, HonuaPluginValidationCodes.InvalidIdentifier);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsupportedEditionGate);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsupportedHost);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsupportedPermissionAccess);
        AssertHasCode(result, HonuaPluginValidationCodes.DuplicateDeclaration);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsafeConfigurationEnvelope);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsupportedConfigurationType);
        AssertHasCode(result, HonuaPluginValidationCodes.SensitiveDefaultValue);
        AssertHasCode(result, HonuaPluginValidationCodes.UnknownConfigurationKey);
        AssertHasCode(result, HonuaPluginValidationCodes.UnsupportedExtensionType);
    }

    private static void AssertHasCode(HonuaPluginValidationResult result, string code)
        => Assert.Contains(result.Issues, issue => issue.Code == code);

    private static string FormatIssues(HonuaPluginValidationResult result)
        => string.Join(
            Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Severity}: {issue.Code} at {issue.Path}: {issue.Message}"));

    private static string ReadFixture(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "contracts", "fixtures", fileName);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find plugin manifest fixture.", fileName);
    }
}
