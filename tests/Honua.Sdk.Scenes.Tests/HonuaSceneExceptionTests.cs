// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Scenes.Exceptions;

namespace Honua.Sdk.Scenes.Tests;

public sealed class HonuaSceneExceptionTests
{
    [Fact]
    public void Default_Ctor_IsHonuaException()
    {
        var ex = new HonuaSceneException();
        Assert.IsAssignableFrom<HonuaException>(ex);
    }

    [Fact]
    public void Message_Ctor_PreservesMessage()
    {
        var ex = new HonuaSceneException("boom");
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Message_Inner_Ctor_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaSceneException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("wrap", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    public void Status_Ctor_PreservesStatusCode(HttpStatusCode status)
    {
        var ex = new HonuaSceneException(status, "msg", "raw");
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("raw", ex.ResponseBody);
    }
}
