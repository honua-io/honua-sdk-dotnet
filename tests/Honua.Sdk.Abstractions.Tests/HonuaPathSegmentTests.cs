// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class HonuaPathSegmentTests
{
    [Theory]
    [InlineData("share-7f3c", "share-7f3c")]
    [InlineData("layer/1", "layer%2F1")]
    [InlineData("a b", "a%20b")]
    [InlineData("..hidden", "..hidden")]
    public void Escape_EncodesOneSegment(string value, string expected)
    {
        Assert.Equal(expected, HonuaPathSegment.Escape(value, "id"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Escape_RejectsBlank(string? value)
    {
        var ex = Assert.Throws<ArgumentException>(() => HonuaPathSegment.Escape(value, "shareId"));
        Assert.Equal("shareId", ex.ParamName);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void Escape_RejectsDotSegments(string value)
    {
        var ex = Assert.Throws<ArgumentException>(() => HonuaPathSegment.Escape(value, "shareId"));
        Assert.Equal("shareId", ex.ParamName);
        Assert.Contains("dot segment", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Escape_RejectsControlCharacters()
    {
        var ex = Assert.Throws<ArgumentException>(() => HonuaPathSegment.Escape("a\nb", "shareId"));
        Assert.Equal("shareId", ex.ParamName);
        Assert.Contains("control", ex.Message, StringComparison.Ordinal);
    }
}
