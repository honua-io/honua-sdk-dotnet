// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.Wfs.Exceptions;

/// <summary>
/// Exception thrown when a WFS request fails.
/// </summary>
public sealed class HonuaWfsException : Exception
{
    /// <summary>HTTP status code returned by the server.</summary>
    public HttpStatusCode StatusCode { get; }

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
