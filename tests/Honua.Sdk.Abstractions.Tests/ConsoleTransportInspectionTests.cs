// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Environments;
using Honua.Sdk.Abstractions.Serialization;

namespace Honua.Sdk.Abstractions.Tests;

/// <summary>
/// AC2 coverage for ticket 169: a native Console host can inspect transport and
/// mTLS capability state from the shared environment-profile contracts (no
/// server "transport manifest" exists or is required). Exercises the same
/// host-neutral DTOs Console reads at runtime.
/// </summary>
public sealed class ConsoleTransportInspectionTests
{
    [Fact]
    public void NativeProfile_ExposesGrpcAndMutualTlsTransportState()
    {
        var profiles = LoadProfiles();

        var native = Assert.Single(profiles.Environments, p => p.EnvironmentId == "field-native-ready");

        // Native transport inspection: gRPC + job/telemetry streaming + mTLS.
        Assert.Equal(HonuaEnvironmentAuthMode.NativeMutualTls, native.DefaultAuthMode);
        Assert.True(native.TransportCapabilities.SupportsNativeGrpc);
        Assert.True(native.TransportCapabilities.SupportsMutualTls);
        Assert.True(native.TransportCapabilities.SupportsJobStream);
        Assert.True(native.TransportCapabilities.SupportsTelemetryStream);

        // mTLS capability state: the host stores only a certificate selector and
        // the sanitized validation outcome — never certificate bytes.
        Assert.True(native.TrustProfile?.RequiresClientCertificate);
        Assert.Equal(HonuaClientCertificateReferenceKind.KeychainReference, native.TrustProfile?.ClientCertificate?.Kind);
        Assert.Equal(HonuaCertificateValidationStatus.Ready, native.TrustState.Status);
    }

    [Fact]
    public void BrowserProfile_GatesOutNativeOnlyTransport()
    {
        var profiles = LoadProfiles();

        var browser = Assert.Single(profiles.Environments, p => p.EnvironmentId == "prod-web");

        // A browser host inspects the same flags to disable native-only features.
        Assert.False(browser.TransportCapabilities.SupportsNativeGrpc);
        Assert.False(browser.TransportCapabilities.SupportsMutualTls);
        Assert.Null(browser.TrustProfile);
    }

    [Fact]
    public void MutualTlsValidationStates_AreInspectableForNativeDiagnostics()
    {
        var profiles = LoadProfiles();

        var observedStatuses = profiles.Environments
            .Select(p => p.TrustState.Status)
            .ToHashSet();

        // The native diagnostics surface must be able to represent each failure
        // and success state the host can report back into the profile.
        HonuaCertificateValidationStatus[] required =
        [
            HonuaCertificateValidationStatus.Missing,
            HonuaCertificateValidationStatus.Expired,
            HonuaCertificateValidationStatus.ExpiringSoon,
            HonuaCertificateValidationStatus.Untrusted,
            HonuaCertificateValidationStatus.Rejected,
            HonuaCertificateValidationStatus.WrongEnvironment,
            HonuaCertificateValidationStatus.Ready
        ];

        foreach (var status in required)
        {
            Assert.Contains(status, observedStatuses);
        }
    }

    private static HonuaEnvironmentProfileSet LoadProfiles()
        => JsonSerializer.Deserialize(
            ReadFixture("environment-profiles.v1.json"),
            HonuaAbstractionsJsonContext.Default.HonuaEnvironmentProfileSet)
            ?? throw new InvalidOperationException("Environment profile fixture was empty.");

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
