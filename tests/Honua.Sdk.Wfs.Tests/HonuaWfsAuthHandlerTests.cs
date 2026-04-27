// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Wfs.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Wfs.Tests;

public sealed class HonuaWfsAuthHandlerTests
{
    [Fact]
    public async Task AuthHandler_UsesCredentialProvidersPerRequest()
    {
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;
        var capturedCredentials = new List<(string? ApiKey, string? Authorization)>();

        var options = Options.Create(new HonuaWfsClientOptions
        {
            ApiKeyProvider = _ => Task.FromResult<string?>($"wfs-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"wfs-token-{++bearerTokenCalls}")
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            req.Headers.TryGetValues("X-API-Key", out var apiValues);
            capturedCredentials.Add((apiValues?.SingleOrDefault(), req.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var authHandler = new HonuaWfsAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        using var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        await httpClient.GetAsync("/wfs");
        await httpClient.GetAsync("/wfs");

        Assert.Collection(
            capturedCredentials,
            first =>
            {
                Assert.Equal("wfs-key-1", first.ApiKey);
                Assert.Equal("Bearer wfs-token-1", first.Authorization);
            },
            second =>
            {
                Assert.Equal("wfs-key-2", second.ApiKey);
                Assert.Equal("Bearer wfs-token-2", second.Authorization);
            });
    }
}
