// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;
using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Grpc.Tests;

public sealed class HonuaGrpcExceptionTests
{
    [Fact]
    public void Default_Ctor_HasUnknownStatus()
    {
        var ex = new HonuaGrpcException();
        Assert.IsAssignableFrom<HonuaException>(ex);
        Assert.Equal(StatusCode.Unknown, ex.StatusCode);
    }

    [Fact]
    public void Message_Ctor_FormatsMessage()
    {
        var ex = new HonuaGrpcException("boom");
        Assert.Equal(StatusCode.Unknown, ex.StatusCode);
        Assert.Contains("boom", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_Inner_Ctor_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaGrpcException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Contains("wrap", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StatusCode.NotFound)]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.DeadlineExceeded)]
    public void Status_Ctor_PreservesStatusCode(StatusCode status)
    {
        var ex = new HonuaGrpcException(status, "detail");
        Assert.Equal(status, ex.StatusCode);
        Assert.Contains("detail", ex.Message, StringComparison.Ordinal);
    }
}
