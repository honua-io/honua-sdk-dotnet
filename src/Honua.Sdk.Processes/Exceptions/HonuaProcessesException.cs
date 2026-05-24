// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.Processes.Exceptions;

/// <summary>
/// Exception thrown when an OGC API Processes request fails.
/// </summary>
public sealed class HonuaProcessesException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesException"/> class.
    /// </summary>
    public HonuaProcessesException()
        : this(HttpStatusCode.InternalServerError, "OGC API Processes request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesException"/> class.
    /// </summary>
    public HonuaProcessesException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesException"/> class with an inner exception.
    /// </summary>
    public HonuaProcessesException(string message, Exception innerException)
        : this(HttpStatusCode.InternalServerError, message, null, innerException)
    {
    }

    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Problem type URI, if supplied by the server.
    /// </summary>
    public string? ProblemType { get; }

    /// <summary>
    /// Problem title, if supplied by the server.
    /// </summary>
    public string? ProblemTitle { get; }

    /// <summary>
    /// Problem detail, if supplied by the server.
    /// </summary>
    public string? ProblemDetail { get; }

    /// <summary>
    /// Raw response body.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesException"/> class.
    /// </summary>
    public HonuaProcessesException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        string? problemType = null,
        string? problemTitle = null,
        string? problemDetail = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ProblemType = problemType;
        ProblemTitle = problemTitle;
        ProblemDetail = problemDetail;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesException"/> class with an inner exception.
    /// </summary>
    public HonuaProcessesException(HttpStatusCode statusCode, string message, string? responseBody, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
