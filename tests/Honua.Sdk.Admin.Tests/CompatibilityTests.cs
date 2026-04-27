// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class CompatibilityTests
{
    [Theory]
    [InlineData("0.1.0", "preview")]
    [InlineData("0.1.0", "beta")]
    [InlineData("0.1.1", "rc")]
    [InlineData("v0.2.0+build.5", "stable")]
    [InlineData("1.0.0-alpha.1", "lts")]
    public void Evaluate_ReturnsSupported_ForServerMatrixBaselineOrNewer(
        string serverVersion,
        string releaseChannel)
    {
        var capabilities = CreateCapabilities(serverVersion, releaseChannel);

        var result = HonuaAdminCompatibility.Evaluate(capabilities);

        Assert.True(result.IsSupported);
        Assert.Null(result.UnsupportedReason);
        Assert.Equal(serverVersion, result.ServerVersion);
        Assert.Equal(releaseChannel, result.ReleaseChannel);
    }

    [Theory]
    [InlineData("0.0.9", "preview", "minimum supported server version")]
    [InlineData("0.1.0", "alpha", "minimum supported release channel")]
    [InlineData("0.1.0", "nightly", "minimum supported release channel")]
    public void Evaluate_ReturnsUnsupported_ForServerMatrixBelowBaseline(
        string serverVersion,
        string releaseChannel,
        string expectedReason)
    {
        var capabilities = CreateCapabilities(serverVersion, releaseChannel);

        var result = HonuaAdminCompatibility.Evaluate(capabilities);

        Assert.False(result.IsSupported);
        Assert.Contains(expectedReason, result.UnsupportedReason);
    }

    [Fact]
    public void Evaluate_ReturnsUnsupported_WhenControlPlaneMajorChanges()
    {
        var capabilities = CreateCapabilities(
            HonuaAdminCompatibility.MinimumSupportedServerVersion,
            HonuaAdminCompatibility.MinimumSupportedReleaseChannel,
            controlPlaneMajor: HonuaAdminCompatibility.SupportedControlPlaneApiMajor + 1);

        var result = HonuaAdminCompatibility.Evaluate(capabilities);

        Assert.False(result.IsSupported);
        Assert.Contains("Control-plane API major", result.UnsupportedReason);
    }

    [Fact]
    public void Evaluate_ReturnsUnsupported_WhenControlPlaneBasePathChanges()
    {
        var capabilities = CreateCapabilities(
            HonuaAdminCompatibility.MinimumSupportedServerVersion,
            HonuaAdminCompatibility.MinimumSupportedReleaseChannel,
            basePath: "/api/v2/admin");

        var result = HonuaAdminCompatibility.Evaluate(capabilities);

        Assert.False(result.IsSupported);
        Assert.Contains("Control-plane API base path", result.UnsupportedReason);
    }

    [Fact]
    public void Evaluate_ReturnsUnsupported_WhenControlPlaneApiIsDeprecated()
    {
        var capabilities = CreateCapabilities(
            HonuaAdminCompatibility.MinimumSupportedServerVersion,
            HonuaAdminCompatibility.MinimumSupportedReleaseChannel,
            deprecated: true);

        var result = HonuaAdminCompatibility.Evaluate(capabilities);

        Assert.False(result.IsSupported);
        Assert.Equal("The advertised control-plane API major is deprecated.", result.UnsupportedReason);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ReturnsSupportedResult_AndFeatures()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("/admin/capabilities", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                compatibility = new
                {
                    serverVersion = "0.1.0",
                    releaseChannel = "beta",
                    controlPlaneApi = new
                    {
                        major = 1,
                        basePath = "/api/v1/admin",
                        deprecated = false
                    },
                    metadataSchemas = new[]
                    {
                        new
                        {
                            version = "honua.io/v1alpha1",
                            deprecated = false
                        }
                    },
                    features = new
                    {
                        metadataResources = true,
                        manifestExport = true,
                        manifestApply = true,
                        manifestDryRun = true,
                        manifestPrune = false
                    }
                }
            }));
        });

        var result = await client.CheckCompatibilityAsync();

        Assert.True(result.IsSupported);
        Assert.Null(result.UnsupportedReason);
        Assert.Equal(HonuaAdminCompatibility.MinimumSupportedServerVersion, result.MinimumSupportedServerVersion);
        Assert.Equal("0.1.0", result.ServerVersion);
        Assert.Equal("beta", result.ReleaseChannel);
        Assert.True(result.Features.MetadataResources);
        Assert.True(result.Features.ManifestApply);
        Assert.True(result.Features.ManifestDryRun);
        Assert.False(result.Features.ManifestPrune);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ReturnsUnsupportedResult_WhenServerVersionBelowBaseline()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                compatibility = new
                {
                    serverVersion = "0.0.9",
                    releaseChannel = "preview",
                    controlPlaneApi = new
                    {
                        major = 1,
                        basePath = "/api/v1/admin",
                        deprecated = false
                    },
                    metadataSchemas = new[]
                    {
                        new
                        {
                            version = "honua.io/v1alpha1",
                            deprecated = false
                        }
                    },
                    features = new
                    {
                        metadataResources = true,
                        manifestExport = true,
                        manifestApply = true,
                        manifestDryRun = false,
                        manifestPrune = false
                    }
                }
            })));

        var result = await client.CheckCompatibilityAsync();

        Assert.False(result.IsSupported);
        Assert.Contains(HonuaAdminCompatibility.MinimumSupportedServerVersion, result.UnsupportedReason);
        Assert.Equal("0.0.9", result.ServerVersion);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ReturnsUnsupportedResult_WhenCompatibilityMetadataMissing()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                metadataApiVersions = new[] { "honua.io/v1alpha1" },
                manifestSupported = true
            })));

        var result = await client.CheckCompatibilityAsync();

        Assert.False(result.IsSupported);
        Assert.Equal("Server did not return compatibility metadata.", result.UnsupportedReason);
        Assert.Equal(string.Empty, result.ServerVersion);
    }

    [Fact]
    public async Task CheckCompatibilityAsync_ReturnsUnsupportedResult_WhenReleaseChannelBelowBaseline()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                compatibility = new
                {
                    serverVersion = "0.1.0",
                    releaseChannel = "alpha",
                    controlPlaneApi = new
                    {
                        major = 1,
                        basePath = "/api/v1/admin",
                        deprecated = false
                    },
                    metadataSchemas = Array.Empty<object>(),
                    features = new
                    {
                        metadataResources = true,
                        manifestExport = true,
                        manifestApply = true,
                        manifestDryRun = true,
                        manifestPrune = true
                    }
                }
            })));

        var result = await client.CheckCompatibilityAsync();

        Assert.False(result.IsSupported);
        Assert.Contains(HonuaAdminCompatibility.MinimumSupportedReleaseChannel, result.UnsupportedReason);
    }

    private static AdminCapabilitiesResponse CreateCapabilities(
        string serverVersion,
        string releaseChannel,
        int controlPlaneMajor = HonuaAdminCompatibility.SupportedControlPlaneApiMajor,
        string basePath = HonuaAdminCompatibility.SupportedControlPlaneApiBasePath,
        bool deprecated = false)
        => new()
        {
            Compatibility = new AdminCompatibilityInfo
            {
                ServerVersion = serverVersion,
                ReleaseChannel = releaseChannel,
                ControlPlaneApi = new ControlPlaneApiCompatibility
                {
                    Major = controlPlaneMajor,
                    BasePath = basePath,
                    Deprecated = deprecated
                },
                MetadataSchemas =
                [
                    new MetadataSchemaCompatibility
                    {
                        Version = "honua.io/v1alpha1",
                        Deprecated = false
                    }
                ],
                Features = new AdminFeatureCompatibility
                {
                    MetadataResources = true,
                    ManifestExport = true,
                    ManifestApply = true,
                    ManifestDryRun = true,
                    ManifestPrune = true
                }
            }
        };
}
