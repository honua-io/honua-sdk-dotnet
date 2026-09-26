// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class HonuaProblemDetailsParserTests
{
    [Fact]
    public void TryParse_ValidProblemJson_PopulatesAllFields()
    {
        const string body = """
            {
              "type": "https://example.com/probs/bad-request",
              "title": "Bad Request",
              "status": 400,
              "detail": "The 'where' clause is invalid.",
              "instance": "/collections/x/items"
            }
            """;

        Assert.True(HonuaProblemDetailsParser.TryParse(body, out var problem));
        Assert.NotNull(problem);
        Assert.Equal("https://example.com/probs/bad-request", problem!.Type);
        Assert.Equal("Bad Request", problem.Title);
        Assert.Equal(400, problem.Status);
        Assert.Equal("The 'where' clause is invalid.", problem.Detail);
        Assert.Equal("/collections/x/items", problem.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("<html>error</html>")]
    public void TryParse_NonProblemBody_ReturnsFalse(string? body)
    {
        Assert.False(HonuaProblemDetailsParser.TryParse(body, out var problem));
        Assert.Null(problem);
    }

    [Fact]
    public void TryParse_UnrelatedJsonObject_ReturnsFalse()
    {
        Assert.False(
            HonuaProblemDetailsParser.TryParse(
                "{ \"success\": false, \"message\": \"Service not found\" }",
                out var problem));
        Assert.Null(problem);
    }

    [Fact]
    public void ResolveMessage_PrefersDetailThenTitleThenFallback()
    {
        var message = HonuaProblemDetailsParser.ResolveMessage(
            """{ "title": "T", "detail": "D" }""", "fallback", out var problem);
        Assert.Equal("D", message);
        Assert.NotNull(problem);

        message = HonuaProblemDetailsParser.ResolveMessage(
            """{ "title": "T" }""", "fallback", out _);
        Assert.Equal("T", message);

        message = HonuaProblemDetailsParser.ResolveMessage("not json", "fallback", out var none);
        Assert.Equal("fallback", message);
        Assert.Null(none);
    }
}
