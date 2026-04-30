// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class MobileContractHarmonizationFixtureTests
{
    private static readonly Assembly[] SdkContractAssemblies =
    [
        typeof(SourceDescriptor).Assembly,
        typeof(OfflinePackageManifest).Assembly
    ];

    private static readonly string[] ExpectedModelFamilyIds =
    [
        "feature-query",
        "feature-edit",
        "feature-attachments",
        "geometry",
        "offline-sync-state",
        "form-feature-schema",
        "scene-metadata",
        "routing",
        "geopackage-sync",
        "display-embed",
        "plugin-contracts",
        "legacy-mobile-sdk"
    ];

    private static readonly string[] ExpectedSdkPackageIds =
    [
        "Honua.Sdk.Abstractions",
        "Honua.Sdk.Offline.Abstractions",
        "Honua.Sdk.Offline",
        "Honua.Sdk.Grpc",
        "Honua.Sdk.GeoServices",
        "Honua.Sdk.Scenes",
        "Honua.Sdk.OgcFeatures"
    ];

    [Fact]
    public void Fixture_DefinesExpectedSchemaAndModelFamilies()
    {
        using var document = LoadFixture();
        var root = document.RootElement;

        Assert.Equal("honua.mobile-contract-harmonization.v1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("honua-sdk-dotnet#68", root.GetProperty("sdkIssue").GetString());
        Assert.Equal("honua-mobile#48", root.GetProperty("mobileIssue").GetString());

        var familyIds = root
            .GetProperty("modelFamilies")
            .EnumerateArray()
            .Select(family => family.GetProperty("id").GetString())
            .ToArray();

        Assert.Equal(ExpectedModelFamilyIds, familyIds);
    }

    [Fact]
    public void Fixture_SdkOwnedAuthoritativeTypesResolveToCurrentAssemblies()
    {
        using var document = LoadFixture();

        var unresolvedTypes = document.RootElement
            .GetProperty("modelFamilies")
            .EnumerateArray()
            .Where(IsSdkOwnedFamily)
            .SelectMany(GetAuthoritativeTypeNames)
            .Where(typeName => ResolveSdkType(typeName) is null)
            .ToArray();

        Assert.Empty(unresolvedTypes);
    }

    [Fact]
    public void Fixture_UsesCurrentSdkPackageBaseline()
    {
        using var document = LoadFixture();

        var packages = document.RootElement
            .GetProperty("compatibility")
            .GetProperty("sdkBaseline")
            .GetProperty("packages")
            .EnumerateArray()
            .Select(package => new
            {
                PackageId = package.GetProperty("packageId").GetString(),
                Version = package.GetProperty("version").GetString()
            })
            .ToArray();

        Assert.Equal(ExpectedSdkPackageIds, packages.Select(package => package.PackageId));
        Assert.All(packages, package => Assert.Equal("0.1.6-alpha.1", package.Version));
    }

    private static bool IsSdkOwnedFamily(JsonElement family)
    {
        var owner = family.GetProperty("owner").GetString();
        var package = family.GetProperty("authoritativePackage").GetString() ?? string.Empty;

        return owner == "honua-sdk-dotnet" ||
            package == "Honua.Sdk.Abstractions" ||
            package == "Honua.Sdk.Offline.Abstractions" ||
            package.Contains("Honua.Sdk.Abstractions", StringComparison.Ordinal) ||
            package.StartsWith("Honua.Sdk.Abstractions ", StringComparison.Ordinal);
    }

    private static IEnumerable<string> GetAuthoritativeTypeNames(JsonElement family)
    {
        if (!family.TryGetProperty("authoritativeTypes", out var types))
        {
            return [];
        }

        return types
            .EnumerateArray()
            .Select(type => type.GetString())
            .OfType<string>()
            .Where(type => !string.IsNullOrWhiteSpace(type));
    }

    private static Type? ResolveSdkType(string typeName)
        => SdkContractAssemblies
            .Select(assembly => assembly.GetType(typeName, throwOnError: false))
            .FirstOrDefault(type => type is not null);

    private static JsonDocument LoadFixture()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "contracts",
                "fixtures",
                "mobile-sdk-contract-harmonization.v1.json");

            if (File.Exists(candidate))
            {
                return JsonDocument.Parse(File.ReadAllText(candidate));
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find the mobile contract harmonization fixture.");
    }
}
