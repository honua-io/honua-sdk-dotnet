// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.OgcFeatures.Wfs.Exceptions;

namespace Honua.Sdk.OgcFeatures.WfsTests;

public sealed class HonuaWfsExceptionTests
{
    [Fact]
    public void Default_Ctor_IsHonuaException()
    {
        var ex = new HonuaWfsException();
        Assert.IsAssignableFrom<HonuaException>(ex);
    }

    [Fact]
    public void Message_Ctor_PreservesMessage()
    {
        var ex = new HonuaWfsException("boom");
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void Message_Inner_Ctor_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaWfsException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("wrap", ex.Message);
    }

    [Fact]
    public void Status_Ctor_PreservesStatusAndExceptionCode()
    {
        var ex = new HonuaWfsException(HttpStatusCode.BadRequest, "bad", responseBody: "raw", exceptionCode: "InvalidParameterValue");
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("raw", ex.ResponseBody);
        Assert.Equal("InvalidParameterValue", ex.ExceptionCode);
    }

    [Fact]
    public void StatusInner_Ctor_PreservesInner()
    {
        var inner = new HttpRequestException();
        var ex = new HonuaWfsException(HttpStatusCode.BadGateway, "upstream", "raw", "Code", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("Code", ex.ExceptionCode);
    }
}
