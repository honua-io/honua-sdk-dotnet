// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Admin.Exceptions;

namespace Honua.Sdk.Admin.Tests;

public sealed class AdminExceptionTests
{
    [Fact]
    public void ApiException_Default_IsHonuaException()
    {
        var ex = new HonuaAdminApiException();
        Assert.IsAssignableFrom<HonuaException>(ex);
    }

    [Fact]
    public void ApiException_Message_PreservesMessage()
    {
        var ex = new HonuaAdminApiException("boom");
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void ApiException_Message_Inner_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaAdminApiException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void ApiException_Status_PopulatesProperties()
    {
        var ex = new HonuaAdminApiException(HttpStatusCode.NotFound, "missing", "raw");
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("raw", ex.ResponseBody);
        Assert.Equal("missing", ex.Message);
    }

    [Fact]
    public void ApiException_AdminEnvelope_UsesMessageInProblemDetails()
    {
        var ex = new HonuaAdminApiException(
            HttpStatusCode.NotFound,
            "Service 'x' not found",
            "{\"success\":false,\"message\":\"Service 'x' not found\"}");

        Assert.Equal("Service 'x' not found", ex.ProblemDetails!.Detail);
        Assert.Equal((int)HttpStatusCode.NotFound, ex.ProblemDetails.Status);
    }

    [Fact]
    public void ApiException_Status_WithInner_PreservesInner()
    {
        var inner = new HttpRequestException();
        var ex = new HonuaAdminApiException(HttpStatusCode.BadGateway, "upstream", "raw", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
    }

    [Fact]
    public void OperationException_Default_IsHonuaException()
    {
        var ex = new HonuaAdminOperationException();
        Assert.IsAssignableFrom<HonuaException>(ex);
        Assert.Null(ex.Operation);
    }

    [Fact]
    public void OperationException_Message_HasNullOperation()
    {
        var ex = new HonuaAdminOperationException("boom");
        Assert.Equal("boom", ex.Message);
        Assert.Null(ex.Operation);
    }

    [Fact]
    public void OperationException_Message_Inner_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaAdminOperationException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Null(ex.Operation);
    }

    [Fact]
    public void OperationException_WithOperation_PopulatesOperation()
    {
        var ex = new HonuaAdminOperationException("oops", "service.create");
        Assert.Equal("service.create", ex.Operation);
    }

    [Fact]
    public void OperationException_WithOperationAndInner_PopulatesBoth()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaAdminOperationException("oops", "service.delete", inner);
        Assert.Equal("service.delete", ex.Operation);
        Assert.Same(inner, ex.InnerException);
    }
}
