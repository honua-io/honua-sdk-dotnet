// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.OgcRecords.Exceptions;

/// <summary>
/// Exception thrown when an OGC API Records request fails, including RFC 7807 Problem Details responses.
/// </summary>
public sealed class HonuaOgcRecordsException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsException"/> class.
    /// </summary>
    public HonuaOgcRecordsException()
        : this(HttpStatusCode.InternalServerError, "OGC API Records request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaOgcRecordsException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaOgcRecordsException(string message, Exception innerException)
        : this(HttpStatusCode.InternalServerError, message, null, innerException)
    {
    }

    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// The RFC 7807 problem type URI, if available.
    /// </summary>
    public string? ProblemType { get; }

    /// <summary>
    /// The RFC 7807 problem title, if available.
    /// </summary>
    public string? ProblemTitle { get; }

    /// <summary>
    /// The RFC 7807 problem detail, if available.
    /// </summary>
    public string? ProblemDetail { get; }

    /// <summary>
    /// The raw response body, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsException"/> class.
    /// </summary>
    public HonuaOgcRecordsException(
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
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsException"/> class with an inner exception.
    /// </summary>
    public HonuaOgcRecordsException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody,
        Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
