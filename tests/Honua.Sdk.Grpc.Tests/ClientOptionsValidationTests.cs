// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Grpc.Tests;

public sealed class ClientOptionsValidationTests
{
    [Fact]
    public void ParseAndValidateAddress_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => HonuaGrpcClientOptions.ParseAndValidateAddress(null!));
    }

    [Fact]
    public void ParseAndValidateAddress_MissingBaseAddress_Throws()
    {
        var options = new HonuaGrpcClientOptions();
        var ex = Assert.Throws<HonuaConfigurationException>(
            () => HonuaGrpcClientOptions.ParseAndValidateAddress(options));
        Assert.Contains("address", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateAddress_RelativeUri_Throws()
    {
        var options = new HonuaGrpcClientOptions
        {
            BaseAddress = new Uri("/rel", UriKind.Relative),
        };
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGrpcClientOptions.ParseAndValidateAddress(options));
    }

    [Theory]
    [InlineData("ftp://example.com")]
    [InlineData("file:///tmp/x")]
    public void ParseAndValidateAddress_UnsupportedScheme_Throws(string value)
    {
        var options = new HonuaGrpcClientOptions { BaseAddress = new Uri(value) };
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGrpcClientOptions.ParseAndValidateAddress(options));
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:5000")]
    public void ParseAndValidateAddress_AcceptsHttpAndHttps(string value)
    {
        var options = new HonuaGrpcClientOptions { BaseAddress = new Uri(value) };
        var result = HonuaGrpcClientOptions.ParseAndValidateAddress(options);
        Assert.Equal(new Uri(value), result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ValidateTimeout_AtOrBelowFloor_Throws(int millis)
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGrpcClientOptions.ValidateTimeout(TimeSpan.FromMilliseconds(millis)));
    }

    [Fact]
    public void ValidateTimeout_AtCeiling_Throws()
    {
        Assert.Throws<HonuaConfigurationException>(
            () => HonuaGrpcClientOptions.ValidateTimeout(TimeSpan.FromHours(24)));
    }

    [Fact]
    public void ValidateTimeout_AcceptsTypicalValue()
    {
        HonuaGrpcClientOptions.ValidateTimeout(TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void MaxRetryAttempts_OutOfRange_Throws(int attempts)
    {
        var options = new HonuaGrpcClientOptions();
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxRetryAttempts = attempts);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void MaxRetryAttempts_InRange_Assigns(int attempts)
    {
        var options = new HonuaGrpcClientOptions { MaxRetryAttempts = attempts };
        Assert.Equal(attempts, options.MaxRetryAttempts);
    }

    [Fact]
    public void RequiresHttpsForAuthentication_NullUri_ReturnsTrue()
    {
        Assert.True(HonuaGrpcClientOptions.RequiresHttpsForAuthentication(null));
    }

    [Fact]
    public void RequiresHttpsForAuthentication_Https_ReturnsFalse()
    {
        Assert.False(HonuaGrpcClientOptions.RequiresHttpsForAuthentication(new Uri("https://example.com")));
    }

    [Theory]
    [InlineData("http://localhost:5000")]
    [InlineData("http://127.0.0.1:5000")]
    public void RequiresHttpsForAuthentication_LocalDevHttp_ReturnsFalse(string value)
    {
        Assert.False(HonuaGrpcClientOptions.RequiresHttpsForAuthentication(new Uri(value)));
    }

    [Fact]
    public void RequiresHttpsForAuthentication_RemoteHttp_ReturnsTrue()
    {
        Assert.True(HonuaGrpcClientOptions.RequiresHttpsForAuthentication(new Uri("http://example.com")));
    }

    [Fact]
    public void IsLocalDevelopmentHttp_HttpsScheme_ReturnsFalse()
    {
        Assert.False(HonuaGrpcClientOptions.IsLocalDevelopmentHttp(new Uri("https://localhost:5000")));
    }

    [Fact]
    public void IsLocalDevelopmentHttp_LoopbackHttp_ReturnsTrue()
    {
        Assert.True(HonuaGrpcClientOptions.IsLocalDevelopmentHttp(new Uri("http://127.0.0.1")));
    }

    [Fact]
    public void Defaults_AreSensible()
    {
        var options = new HonuaGrpcClientOptions();
        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
        Assert.True(options.EnableRetry);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.True(options.EnableCompressionNegotiation);
        Assert.Equal("gzip,identity", options.AcceptedCompressionEncodings);
    }
}
