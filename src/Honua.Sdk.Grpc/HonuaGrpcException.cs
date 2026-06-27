// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Exception thrown when a gRPC call to the Honua server fails.
/// </summary>
public sealed class HonuaGrpcException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    public HonuaGrpcException()
        : this(StatusCode.Unknown, "gRPC request failed.")
    {
    }

    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    /// <param name="message">The error detail message.</param>
    public HonuaGrpcException(string message)
        : this(StatusCode.Unknown, message)
    {
    }

    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    /// <param name="message">The error detail message.</param>
    /// <param name="innerException">The original exception.</param>
    public HonuaGrpcException(string message, Exception innerException)
        : this(StatusCode.Unknown, message, innerException)
    {
    }

    /// <summary>
    /// The gRPC status code.
    /// </summary>
    public StatusCode StatusCode { get; }

    /// <inheritdoc />
    /// <remarks>gRPC is not an HTTP transport, so no HTTP status is surfaced; use <see cref="StatusCode"/>.</remarks>
    public override int? HttpStatus => null;

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaProblemDetails? ProblemDetails =>
        new Honua.Sdk.Abstractions.HonuaProblemDetails
        {
            Title = StatusCode.ToString(),
            Detail = Message,
        };

    /// <summary>
    /// Creates a new gRPC exception.
    /// </summary>
    /// <param name="statusCode">The gRPC status code.</param>
    /// <param name="message">The error detail message.</param>
    /// <param name="innerException">The original exception, if any.</param>
    public HonuaGrpcException(StatusCode statusCode, string message, Exception? innerException = null)
        : base($"gRPC {statusCode}: {message}", innerException)
    {
        StatusCode = statusCode;
    }
}
