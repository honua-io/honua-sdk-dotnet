// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.Studio.Exceptions;

/// <summary>
/// Exception thrown when the Honua Studio API returns an error HTTP status code.
/// </summary>
public sealed class HonuaStudioApiException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioApiException"/> class.
    /// </summary>
    public HonuaStudioApiException()
        : this(HttpStatusCode.InternalServerError, "Studio API request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioApiException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaStudioApiException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioApiException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaStudioApiException(string message, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// Problem detail title, when the server returned a problem-details document.
    /// </summary>
    public string? ProblemTitle { get; }

    /// <summary>
    /// Problem detail body, when the server returned a problem-details document.
    /// </summary>
    public string? ProblemDetail { get; }

    /// <summary>
    /// The raw response body, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioApiException"/> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code returned by the server.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    /// <param name="problemTitle">Problem title supplied by the server, if any.</param>
    /// <param name="problemDetail">Problem detail supplied by the server, if any.</param>
    public HonuaStudioApiException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        string? problemTitle = null,
        string? problemDetail = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ProblemTitle = problemTitle;
        ProblemDetail = problemDetail;
    }
}
