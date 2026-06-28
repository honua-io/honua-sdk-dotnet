// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Spec.Models;

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

    [Fact]
    public void ProblemDetails_WithoutProblem_FallsBackToStatusAndMessage()
    {
        var ex = new HonuaSpecException(HttpStatusCode.BadGateway, "upstream down", "raw");

        var problem = ex.ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal((int)HttpStatusCode.BadGateway, problem!.Status);
        Assert.Equal("upstream down", problem.Detail);
    }

    [Fact]
    public void ProblemDetails_WithProblem_SurfacesStructuredFields()
    {
        var specProblem = new SpecProblem
        {
            Type = "https://honua.io/problems/spec-invalid",
            Title = "Spec invalid",
            Status = (int)HttpStatusCode.UnprocessableEntity,
            Detail = "node 'foo' is malformed",
            Code = "SPEC_INVALID",
            NodeId = "foo",
        };
        var ex = new HonuaSpecException(HttpStatusCode.UnprocessableEntity, "Spec invalid", "raw", specProblem);

        var problem = ex.ProblemDetails;
        Assert.NotNull(problem);
        Assert.Equal("https://honua.io/problems/spec-invalid", problem!.Type);
        Assert.Equal("Spec invalid", problem.Title);
        Assert.Equal((int)HttpStatusCode.UnprocessableEntity, problem.Status);
        Assert.Equal("node 'foo' is malformed", problem.Detail);
        Assert.Equal("foo", problem.Instance);
    }
}
