// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Spec.Tests;

public sealed class HonuaSpecExceptionTests
{
    [Fact]
    public void Default_Ctor_IsHonuaException()
    {
        var ex = new HonuaSpecException();
        Assert.IsAssignableFrom<HonuaException>(ex);
    }

    [Fact]
    public void Message_Ctor_PreservesMessage()
    {
        var ex = new HonuaSpecException("boom");
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Message_Inner_Ctor_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaSpecException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("wrap", ex.Message);
    }

    [Fact]
    public void Status_Ctor_PopulatesProperties()
    {
        var ex = new HonuaSpecException(HttpStatusCode.NotFound, "missing", "raw");
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("raw", ex.ResponseBody);
        Assert.Null(ex.Problem);
        Assert.Equal("missing", ex.Message);
    }
}
