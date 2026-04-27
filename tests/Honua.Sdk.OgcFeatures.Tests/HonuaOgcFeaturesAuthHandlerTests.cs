// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.OgcFeatures.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures.Tests;

public sealed class HonuaOgcFeaturesAuthHandlerTests
{
    [Fact]
    public async Task AuthHandler_UsesCredentialProvidersPerRequest()
    {
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;
        var capturedCredentials = new List<(string? ApiKey, string? Authorization)>();

        var options = Options.Create(new HonuaOgcFeaturesClientOptions
        {
            ApiKeyProvider = _ => Task.FromResult<string?>($"ogc-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"ogc-token-{++bearerTokenCalls}")
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            req.Headers.TryGetValues("X-API-Key", out var apiValues);
            capturedCredentials.Add((apiValues?.SingleOrDefault(), req.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var authHandler = new HonuaOgcFeaturesAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        using var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        await httpClient.GetAsync("/ogc/features/v1");
        await httpClient.GetAsync("/ogc/features/v1");

        Assert.Collection(
            capturedCredentials,
            first =>
            {
                Assert.Equal("ogc-key-1", first.ApiKey);
                Assert.Equal("Bearer ogc-token-1", first.Authorization);
            },
            second =>
            {
                Assert.Equal("ogc-key-2", second.ApiKey);
                Assert.Equal("Bearer ogc-token-2", second.Authorization);
            });
    }
}
