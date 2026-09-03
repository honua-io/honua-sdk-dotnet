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
}
