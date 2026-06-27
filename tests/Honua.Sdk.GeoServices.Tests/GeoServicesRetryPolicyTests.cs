// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.GeoServices;

namespace Honua.Sdk.GeoServices.Tests;

public sealed class GeoServicesRetryPolicyTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void IsRetryableRequest_IdempotentMethods_AreRetryable(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://host/rest/services/x/FeatureServer/0/query");
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_QueryPostFallback_IsRetryable()
    {
        // Long filter strings force GET -> POST; the /query read is still idempotent.
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://host/rest/services/x/FeatureServer/0/query");
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_QueryPostFallback_WithRelativeUri_IsRetryable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("rest/services/x/FeatureServer/0/query", UriKind.Relative));
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_ApplyEditsPost_IsNotRetryable()
    {
        // applyEdits is a mutation and must never be auto-retried.
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://host/rest/services/x/FeatureServer/0/applyEdits");
        Assert.False(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_PatchAddAttachment_IsNotRetryable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "https://host/rest/services/x/FeatureServer/0/1/addAttachment");
        Assert.False(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_NullRequest_FallsBackToTransientPolicy()
    {
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(null));
    }
}
