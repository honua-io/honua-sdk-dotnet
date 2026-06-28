// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Http;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class NextLinkOriginValidatorTests
{
    private static readonly Uri Base = new("https://api.honua.example/");

    [Fact]
    public void IsSameOrigin_RelativeLink_IsAllowed()
    {
        Assert.True(NextLinkOriginValidator.IsSameOrigin("/collections/x/items?token=2", Base));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void IsSameOrigin_EmptyLink_IsAllowed(string? link)
    {
        Assert.True(NextLinkOriginValidator.IsSameOrigin(link, Base));
    }

    [Fact]
    public void IsSameOrigin_SameOriginAbsoluteLink_IsAllowed()
    {
        Assert.True(NextLinkOriginValidator.IsSameOrigin("https://api.honua.example/items?page=2", Base));
    }

    [Fact]
    public void IsSameOrigin_NullBaseAddress_IsAllowed()
    {
        Assert.True(NextLinkOriginValidator.IsSameOrigin("https://evil.example/steal", null));
    }

    [Theory]
    [InlineData("https://evil.example/steal")]            // different host
    [InlineData("http://api.honua.example/items")]        // different scheme
    [InlineData("https://api.honua.example:8443/items")]  // different port
    [InlineData("//evil.example/steal")]                  // protocol-relative cross-origin
    [InlineData("//evil.example:9000/steal")]             // protocol-relative cross-origin with port
    public void IsSameOrigin_CrossOriginLink_IsRejected(string link)
    {
        Assert.False(NextLinkOriginValidator.IsSameOrigin(link, Base));
    }

    [Theory]
    [InlineData("//api.honua.example/items?page=2")]      // protocol-relative same authority
    [InlineData("collections/x/items?page=2")]            // path-relative (no leading slash)
    public void IsSameOrigin_SameAuthorityRelativeLink_IsAllowed(string link)
    {
        Assert.True(NextLinkOriginValidator.IsSameOrigin(link, Base));
    }

    [Fact]
    public void CrossOriginMessage_NamesTheOffendingAuthority()
    {
        var message = NextLinkOriginValidator.CrossOriginMessage("https://evil.example:9000/steal");
        Assert.Contains("evil.example:9000", message, StringComparison.Ordinal);
        Assert.Contains("open-redirect", message, StringComparison.Ordinal);
    }
}
