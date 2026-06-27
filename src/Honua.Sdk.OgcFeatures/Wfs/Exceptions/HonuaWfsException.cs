// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.OgcFeatures.Wfs.Exceptions;

/// <summary>
/// Exception thrown when a WFS request fails.
/// </summary>
public sealed class HonuaWfsException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsException"/> class.
    /// </summary>
    public HonuaWfsException()
        : this(HttpStatusCode.InternalServerError, "WFS request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaWfsException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaWfsException(string message, Exception innerException)
        : this(HttpStatusCode.InternalServerError, message, null, null, innerException)
    {
    }

    /// <summary>HTTP status code returned by the server.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <inheritdoc />
    public override int? HttpStatus => (int)StatusCode;

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaProblemDetails? ProblemDetails =>
        new Honua.Sdk.Abstractions.HonuaProblemDetails
        {
            Status = (int)StatusCode,
            Title = ExceptionCode,
            Detail = Message,
        };

    /// <summary>The raw response body, if available.</summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// OGC exception code from the ExceptionReport (e.g. InvalidParameterValue, OperationNotSupported).
    /// <c>null</c> if the response was not a valid ExceptionReport.
    /// </summary>
    public string? ExceptionCode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsException"/> class.
    /// </summary>
    public HonuaWfsException(HttpStatusCode statusCode, string message, string? responseBody = null, string? exceptionCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ExceptionCode = exceptionCode;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsException"/> class with an inner exception.
    /// </summary>
    public HonuaWfsException(HttpStatusCode statusCode, string message, string? responseBody, string? exceptionCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ExceptionCode = exceptionCode;
    }
}
