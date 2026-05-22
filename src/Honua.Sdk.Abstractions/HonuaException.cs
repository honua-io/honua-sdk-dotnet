// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Base type for every exception thrown by an official <c>Honua.Sdk.*</c> client.
/// Callers can write a single <c>catch (HonuaException)</c> to handle any SDK
/// failure uniformly without having to enumerate per-protocol exception types
/// (<c>HonuaGrpcException</c>, <c>HonuaAdminApiException</c>,
/// <c>HonuaStacException</c>, etc., all derive from this class).
/// </summary>
public abstract class HonuaException : Exception
{
    /// <summary>
    /// Creates a new <see cref="HonuaException"/> with no message.
    /// </summary>
    protected HonuaException()
    {
    }

    /// <summary>
    /// Creates a new <see cref="HonuaException"/> with the specified message.
    /// </summary>
    /// <param name="message">Human-readable description of the failure.</param>
    protected HonuaException(string? message) : base(message)
    {
    }

    /// <summary>
    /// Creates a new <see cref="HonuaException"/> with the specified message and inner exception.
    /// </summary>
    /// <param name="message">Human-readable description of the failure.</param>
    /// <param name="innerException">The underlying transport, parsing, or domain error.</param>
    protected HonuaException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
