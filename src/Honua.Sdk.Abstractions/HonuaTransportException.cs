// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Exception raised when a REST request fails before an HTTP response is available.
/// </summary>
public sealed class HonuaTransportException : HonuaException
{
    /// <summary>Initializes a transport exception with the default message.</summary>
    public HonuaTransportException()
        : base("REST request failed before receiving a response.")
    {
    }

    /// <summary>Initializes a transport exception.</summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaTransportException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a transport exception with an inner exception.</summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The underlying transport failure.</param>
    public HonuaTransportException(string message, Exception innerException)
        : this(message, innerException, null)
    {
    }

    /// <summary>Initializes a transport exception with an optional HTTP status.</summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The underlying transport failure.</param>
    /// <param name="httpStatus">An HTTP status associated with the failure, if available.</param>
    public HonuaTransportException(string message, Exception innerException, int? httpStatus = null)
        : base(message, innerException)
    {
        HttpStatus = httpStatus;
    }

    /// <inheritdoc />
    public override int? HttpStatus { get; }
}
