// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class HonuaFailureReceiptTests
{
    [Fact]
    public async Task FromHttpResponse_ConsidersTransportStatusWhenProtocolCodeIsIndependent()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{\"error\":{\"code\":1000}}")
        };

        var receipt = HonuaFailureReceiptFactory.FromHttpResponse(
            response,
            await response.Content.ReadAsStringAsync(),
            protocolCode: 1000);

        Assert.Equal(503, receipt.TransportStatus);
        Assert.Equal("1000", receipt.ProtocolCode);
        Assert.Equal(HonuaFailureKind.Unavailable, receipt.Kind);
        Assert.True(receipt.Retryable);
    }

    [Theory]
    [InlineData(498, "Invalid Token")]
    [InlineData(499, "Token Required")]
    public void FromHttpResponse_ClassifiesGeoServicesTokenCodesAsAuthentication(int errorCode, string message)
    {
        var body = $$$"""{"error":{"code":{{{errorCode}}},"message":"{{{message}}}"}}""";
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body)
        };

        var receipt = HonuaFailureReceiptFactory.FromHttpResponse(response, body, protocolCode: errorCode);

        Assert.Equal(200, receipt.TransportStatus);
        Assert.Equal(errorCode.ToString(System.Globalization.CultureInfo.InvariantCulture), receipt.ProtocolCode);
        Assert.Equal(HonuaFailureKind.Authentication, receipt.Kind);
        Assert.Equal("authentication_required", receipt.Code);
        Assert.NotEqual(HonuaFailureKind.Authorization, receipt.Kind);
    }

    [Fact]
    public void FromHttpResponse_HonorsHonuaRetryableFalseHeaderOn503()
    {
        const string body = """{"code":"CAPABILITY_UNAVAILABLE","title":"Capability unavailable"}""";
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent(body)
        };
        response.Headers.Add("Honua-Retryable", "false");

        var receipt = HonuaFailureReceiptFactory.FromHttpResponse(response, body);

        Assert.Equal(503, receipt.TransportStatus);
        Assert.Equal(HonuaFailureKind.Unavailable, receipt.Kind);
        Assert.False(receipt.Retryable);
    }
}
