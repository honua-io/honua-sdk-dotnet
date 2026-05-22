// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Catalogs.Records.Exceptions;

namespace Honua.Sdk.Catalogs.RecordsTests;

public sealed class HonuaOgcRecordsExceptionTests
{
    [Fact]
    public void Default_Ctor_HasInternalServerErrorStatus()
    {
        var ex = new HonuaOgcRecordsException();

        Assert.IsAssignableFrom<HonuaException>(ex);
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public void Message_Ctor_PreservesMessage()
    {
        var ex = new HonuaOgcRecordsException("boom");

        Assert.Equal("boom", ex.Message);
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public void Message_Inner_Ctor_PreservesInnerException()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaOgcRecordsException("wrap", inner);

        Assert.Same(inner, ex.InnerException);
        Assert.Equal("wrap", ex.Message);
    }

    [Fact]
    public void Status_Ctor_PopulatesProblemDetailFields()
    {
        var ex = new HonuaOgcRecordsException(
            HttpStatusCode.NotFound,
            "missing",
            responseBody: "{}",
            problemType: "https://example.com/not-found",
            problemTitle: "Not Found",
            problemDetail: "Record missing");

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("{}", ex.ResponseBody);
        Assert.Equal("https://example.com/not-found", ex.ProblemType);
        Assert.Equal("Not Found", ex.ProblemTitle);
        Assert.Equal("Record missing", ex.ProblemDetail);
    }

    [Fact]
    public void StatusBody_Ctor_WithInner_PopulatesBodyAndInner()
    {
        var inner = new HttpRequestException();
        var ex = new HonuaOgcRecordsException(HttpStatusCode.BadGateway, "upstream", "raw", inner);

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal("raw", ex.ResponseBody);
        Assert.Same(inner, ex.InnerException);
    }
}
