// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Admin;

namespace Honua.Sdk.Admin.Tests;

/// <summary>
/// honua-release#233: the runtime server floor is a declaration of the capability baseline
/// file, so the two must never disagree.
/// </summary>
public sealed class ServerFloorBaselineTests
{
    // honua-release docs/SDK-SERVER-BASELINE-RULE.md "Runtime floor constants": a first-release
    // baseline maps to the least SemVer identity of the first server release line.
    private const string FirstReleaseRuntimeFloor = "2026.1.0-0";

    [Fact]
    public void MinimumSupportedServerVersion_IsDerivedFromTheCapabilityBaseline()
    {
        using var baseline = JsonDocument.Parse(
            File.ReadAllText(Path.Join(FindRepoRoot(), "release", "sdk-capability-baseline.json")));
        var declared = baseline.RootElement.GetProperty("minimumServerVersion").GetString();

        var derived = declared == "first-release" ? FirstReleaseRuntimeFloor : declared;

        Assert.Equal(HonuaAdminCompatibility.MinimumSupportedServerVersion, derived);
    }

    [Fact]
    public void CompatibilityMatrix_DocumentsTheDerivedFloor()
    {
        var policy = File.ReadAllText(Path.Join(FindRepoRoot(), "docs", "compatibility.md"));

        Assert.Contains(
            $"| `{HonuaAdminCompatibility.MinimumSupportedServerVersion}` or newer |",
            policy,
            StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Honua.Sdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }
}
