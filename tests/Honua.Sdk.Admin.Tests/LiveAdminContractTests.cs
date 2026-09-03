// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin;

namespace Honua.Sdk.Admin.Tests;

public sealed class LiveAdminContractTests
{
    [Fact]
    public async Task ManifestExportAdvertisedByCapabilities_IsReachable()
    {
        var baseUrl = Environment.GetEnvironmentVariable("HONUA_CONTRACT_LIVE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        using var http = new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute) };
        http.DefaultRequestHeaders.Add(
            "X-API-Key",
            Environment.GetEnvironmentVariable("HONUA_CONTRACT_LIVE_API_KEY") ?? "quickstart-admin-password");
        var client = new HonuaAdminClient(http);

        var capabilities = await client.GetCapabilitiesAsync();
        Assert.NotNull(capabilities.Compatibility);
        Assert.True(capabilities.Features.ManifestExport);

        // Trunk advertises export support, but the v1 /manifest route was
        // removed. The SDK should receive a typed manifest, not HTTP 404.
        var manifest = await client.GetManifestAsync();
        Assert.NotNull(manifest);
    }
}
