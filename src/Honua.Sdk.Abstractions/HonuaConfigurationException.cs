// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Thrown when SDK options fail validation at registration time
/// (missing <c>BaseAddress</c>, out-of-range <c>Timeout</c>, invalid scheme,
/// or similar configuration errors). Derives from <see cref="HonuaException"/>
/// so a single <c>catch (HonuaException)</c> handles both configuration and
/// runtime failures uniformly.
/// </summary>
public sealed class HonuaConfigurationException : HonuaException
{
    /// <summary>
    /// Creates a new <see cref="HonuaConfigurationException"/> with no message.
    /// </summary>
    public HonuaConfigurationException()
    {
    }

    /// <summary>
    /// Creates a new <see cref="HonuaConfigurationException"/> with the given message.
    /// </summary>
    /// <param name="message">Human-readable description of the configuration error.</param>
    public HonuaConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="HonuaConfigurationException"/> wrapping an inner cause.
    /// </summary>
    /// <param name="message">Human-readable description of the configuration error.</param>
    /// <param name="innerException">The underlying cause, if any.</param>
    public HonuaConfigurationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
