// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions;

namespace Honua.Sdk.GeoServices.Tests;

public sealed class ClientOptionsValidationTests
{
    [Fact]
    public void ValidateBaseAddress_NullUri_Throws()
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGeoServicesClientOptions.ValidateBaseAddress(null));
    }

    [Fact]
    public void ValidateBaseAddress_RelativeUri_Throws()
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGeoServicesClientOptions.ValidateBaseAddress(new Uri("/relative", UriKind.Relative)));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///tmp/x")]
    public void ValidateBaseAddress_UnsupportedScheme_Throws(string value)
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGeoServicesClientOptions.ValidateBaseAddress(new Uri(value)));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:5000")]
    public void ValidateBaseAddress_AcceptsHttpAndHttps(string value)
    {
        HonuaGeoServicesClientOptions.ValidateBaseAddress(new Uri(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ValidateTimeout_AtOrBelowFloor_Throws(int millis)
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGeoServicesClientOptions.ValidateTimeout(TimeSpan.FromMilliseconds(millis)));
    }

    [Fact]
    public void ValidateTimeout_AtCeiling_Throws()
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGeoServicesClientOptions.ValidateTimeout(TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ValidateTimeout_AcceptsTypicalValue()
    {
        HonuaGeoServicesClientOptions.ValidateTimeout(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void MaxRetryAttempts_OutOfRange_Throws(int attempts)
    {
        var options = new HonuaGeoServicesClientOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxRetryAttempts = attempts);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void MaxRetryAttempts_InRange_Assigns(int attempts)
    {
        var options = new HonuaGeoServicesClientOptions { MaxRetryAttempts = attempts };
        Assert.Equal(attempts, options.MaxRetryAttempts);
    }

    [Fact]
    public void RequiresHttpsForAuthentication_NullUri_ReturnsTrue()
    {
        Assert.True(HonuaGeoServicesClientOptions.RequiresHttpsForAuthentication(null));
    }

    [Fact]
    public void RequiresHttpsForAuthentication_Https_ReturnsFalse()
    {
        Assert.False(HonuaGeoServicesClientOptions.RequiresHttpsForAuthentication(new Uri("https://example.com")));
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1:5000")]
    public void RequiresHttpsForAuthentication_LocalDevHttp_ReturnsFalse(string value)
    {
        Assert.False(HonuaGeoServicesClientOptions.RequiresHttpsForAuthentication(new Uri(value)));
    }

    [Fact]
    public void RequiresHttpsForAuthentication_RemoteHttp_ReturnsTrue()
    {
        Assert.True(HonuaGeoServicesClientOptions.RequiresHttpsForAuthentication(new Uri("http://example.com")));
    }

    [Fact]
    public void IsLocalDevelopmentHttp_HttpsScheme_ReturnsFalse()
    {
        Assert.False(HonuaGeoServicesClientOptions.IsLocalDevelopmentHttp(new Uri("https://localhost:5000")));
    }

    [Fact]
    public void IsLocalDevelopmentHttp_LoopbackHttp_ReturnsTrue()
    {
        Assert.True(HonuaGeoServicesClientOptions.IsLocalDevelopmentHttp(new Uri("http://127.0.0.1")));
    }
}
