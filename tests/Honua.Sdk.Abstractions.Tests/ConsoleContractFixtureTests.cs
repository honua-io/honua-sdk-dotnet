// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Console;
using Honua.Sdk.Abstractions.Environments;
using Honua.Sdk.Abstractions.Serialization;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class ConsoleContractFixtureTests
{
    private static readonly HonuaCertificateValidationStatus[] RequiredNativeTrustStatuses =
    [
        HonuaCertificateValidationStatus.Missing,
        HonuaCertificateValidationStatus.Expired,
        HonuaCertificateValidationStatus.Untrusted,
        HonuaCertificateValidationStatus.Rejected,
        HonuaCertificateValidationStatus.WrongEnvironment,
        HonuaCertificateValidationStatus.Ready
    ];

    [Fact]
    public void EnvironmentProfilesFixture_CoversBrowserNativeAndMtlsTrustStates()
    {
        var profiles = JsonSerializer.Deserialize(
            ReadFixture("environment-profiles.v1.json"),
            HonuaAbstractionsJsonContext.Default.HonuaEnvironmentProfileSet)
            ?? throw new InvalidOperationException("Environment profile fixture was empty.");

        Assert.Equal("honua.environment-profiles.v1", profiles.SchemaVersion);
        Assert.True(profiles.Environments.Count >= 2);

        var browserProfile = Assert.Single(profiles.Environments, profile => profile.EnvironmentId == "prod-web");
        Assert.Equal(HonuaEnvironmentAuthMode.BrowserSession, browserProfile.DefaultAuthMode);
        Assert.True(browserProfile.TransportCapabilities.SupportsRestOpenApi);
        Assert.True(browserProfile.TransportCapabilities.SupportsBrowserRealtime);
        Assert.False(browserProfile.TransportCapabilities.SupportsNativeGrpc);
        Assert.False(browserProfile.TransportCapabilities.SupportsMutualTls);

        var nativeProfile = Assert.Single(profiles.Environments, profile => profile.EnvironmentId == "field-native-ready");
        Assert.Equal(HonuaEnvironmentAuthMode.NativeMutualTls, nativeProfile.DefaultAuthMode);
        Assert.True(nativeProfile.TransportCapabilities.SupportsNativeGrpc);
        Assert.True(nativeProfile.TransportCapabilities.SupportsJobStream);
        Assert.True(nativeProfile.TransportCapabilities.SupportsTelemetryStream);
        Assert.True(nativeProfile.TransportCapabilities.SupportsMutualTls);
        Assert.Equal(HonuaClientCertificateReferenceKind.KeychainReference, nativeProfile.TrustProfile?.ClientCertificate?.Kind);

        var coveredStatuses = profiles.Environments
            .Select(profile => profile.TrustState.Status)
            .ToHashSet();

        foreach (var status in RequiredNativeTrustStatuses)
        {
            Assert.Contains(status, coveredStatuses);
        }
    }

    [Fact]
    public void RouteGuardFixture_ProvidesShellDescriptorAndGuardDecisions()
    {
        using var document = JsonDocument.Parse(ReadFixture("route-guard-rbac.v1.json"));
        var root = document.RootElement;

        Assert.Equal("honua.console-route-guard-rbac.v1", root.GetProperty("schemaVersion").GetString());

        var shell = JsonSerializer.Deserialize(
            root.GetProperty("shell").GetRawText(),
            HonuaAbstractionsJsonContext.Default.HonuaConsoleShellDescriptor)
            ?? throw new InvalidOperationException("Route guard shell fixture was empty.");

        Assert.Equal("user-operator", shell.Principal?.UserId);
        Assert.Contains(shell.Navigation, item => item.Id == "publishing");
        Assert.Contains(shell.RouteGuards, guard => guard.RoutePattern == "/publishing");
        Assert.Contains(
            shell.RouteGuards.Single(guard => guard.RoutePattern == "/publishing").RequiredPermissions,
            permission => permission.Operation == "publish");

        var decisions = root.GetProperty("routeDecisions")
            .EnumerateArray()
            .Select(element => JsonSerializer.Deserialize(
                element.GetRawText(),
                HonuaAbstractionsJsonContext.Default.HonuaConsoleRouteGuardDecision))
            .OfType<HonuaConsoleRouteGuardDecision>()
            .ToArray();

        Assert.Contains(decisions, decision => decision.Access == HonuaConsoleRouteAccess.Allowed);
        Assert.Contains(decisions, decision => decision.Access == HonuaConsoleRouteAccess.Denied);
        Assert.Contains(decisions, decision => decision.Access == HonuaConsoleRouteAccess.Challenge);
        Assert.Contains(decisions, decision => decision.ReasonCodes.Contains("missing-permission", StringComparer.Ordinal));
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Join(FindRepoRoot(), "contracts", "fixtures", "console", name));

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
