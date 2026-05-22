// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices.Tests;

public sealed class HonuaGeoServicesAuthHandlerTests
{
    [Fact]
    public async Task AuthHandler_UsesCredentialProvidersPerRequest()
    {
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;
        var capturedCredentials = new List<(string? ApiKey, string? Authorization)>();

        var options = Options.Create(new HonuaGeoServicesClientOptions
        {
            BaseAddress = new Uri("https://localhost:5001"),
            ApiKeyProvider = _ => Task.FromResult<string?>($"geoservices-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"geoservices-token-{++bearerTokenCalls}")
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            req.Headers.TryGetValues("X-API-Key", out var apiValues);
            capturedCredentials.Add((apiValues?.SingleOrDefault(), req.Headers.Authorization?.ToString()));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        var authHandler = new HonuaGeoServicesAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        using var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        await httpClient.GetAsync("/FeatureServer");
        await httpClient.GetAsync("/FeatureServer");

        Assert.Collection(
            capturedCredentials,
            first =>
            {
                Assert.Equal("geoservices-key-1", first.ApiKey);
                Assert.Equal("Bearer geoservices-token-1", first.Authorization);
            },
            second =>
            {
                Assert.Equal("geoservices-key-2", second.ApiKey);
                Assert.Equal("Bearer geoservices-token-2", second.Authorization);
            });
    }
}
