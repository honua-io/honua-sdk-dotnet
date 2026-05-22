// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Tests;

public sealed class HonuaExceptionTests
{
    [Fact]
    public void ConfigurationException_Default_IsHonuaException()
    {
        var ex = new HonuaConfigurationException();
        Assert.IsAssignableFrom<HonuaException>(ex);
    }

    [Fact]
    public void ConfigurationException_Message_PreservesMessage()
    {
        var ex = new HonuaConfigurationException("missing base address");
        Assert.Equal("missing base address", ex.Message);
    }

    [Fact]
    public void ConfigurationException_Message_Inner_PreservesInner()
    {
        var inner = new InvalidOperationException("root");
        var ex = new HonuaConfigurationException("wrap", inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal("wrap", ex.Message);
    }

    [Fact]
    public void ConfigurationException_IsDerivedFromException()
    {
        Exception ex = new HonuaConfigurationException("x");
        Assert.IsType<HonuaConfigurationException>(ex);
    }

    [Fact]
    public void HonuaException_CanBeCaughtAsBaseType_FromDerived()
    {
        try
        {
            throw new HonuaConfigurationException("config");
        }
        catch (HonuaException ex)
        {
            Assert.Equal("config", ex.Message);
        }
    }
}
