// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.OgcFeatures.Exceptions;

namespace Honua.Sdk.OgcFeatures.Tests;

public sealed class HonuaOgcFeaturesExceptionTests
{
    [Fact]
    public void Default_Ctor_IsHonuaException()
    {
        var ex = new HonuaOgcFeaturesException();
        Assert.IsAssignableFrom<HonuaException>(ex);
    }

    [Fact]
    public void Message_Ctor_PreservesMessage()
    {
        var ex = new HonuaOgcFeaturesException("boom");
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Message_Inner_Ctor_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaOgcFeaturesException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("wrap", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public void Status_Ctor_PreservesStatusCode(HttpStatusCode status)
    {
        var ex = new HonuaOgcFeaturesException(status, "message");
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal("message", ex.Message);
    }
}
