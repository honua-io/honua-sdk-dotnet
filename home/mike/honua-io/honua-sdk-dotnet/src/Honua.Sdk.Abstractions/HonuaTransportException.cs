// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Exception raised when a REST request fails before an HTTP response is available.
/// </summary>
public sealed class HonuaTransportException : HonuaException
{
    /// <summary>Initializes a transport exception.</summary>
    public HonuaTransportException(string message, Exception innerException, int? httpStatus = null)
        : base(message, innerException)
    {
        HttpStatus = httpStatus;
    }

    /// <inheritdoc />
    public override int? HttpStatus { get; }
}
