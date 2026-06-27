// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.Scenes.Exceptions;

/// <summary>
/// Exception thrown when a scene metadata or package contract operation fails.
/// </summary>
public sealed class HonuaSceneException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneException"/> class.
    /// </summary>
    public HonuaSceneException()
        : this(HttpStatusCode.InternalServerError, "Honua scene request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaSceneException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaSceneException(string message, Exception innerException)
        : this(HttpStatusCode.InternalServerError, message, null, innerException)
    {
    }

    /// <summary>
    /// HTTP status code returned by the server.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <inheritdoc />
    public override int? HttpStatus => (int)StatusCode;

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaProblemDetails? ProblemDetails =>
        new Honua.Sdk.Abstractions.HonuaProblemDetails
        {
            Status = (int)StatusCode,
            Detail = Message,
        };

    /// <summary>
    /// The raw response body, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneException"/> class.
    /// </summary>
    /// <param name="statusCode">HTTP status code returned by the server.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    public HonuaSceneException(HttpStatusCode statusCode, string message, string? responseBody = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneException"/> class with an inner exception.
    /// </summary>
    /// <param name="statusCode">HTTP status code returned by the server.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaSceneException(
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
