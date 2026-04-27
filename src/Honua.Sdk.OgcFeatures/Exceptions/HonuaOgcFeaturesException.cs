// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.OgcFeatures.Exceptions;

/// <summary>
/// Exception thrown when an OGC API Features request fails, including RFC 7807 Problem Details responses.
/// </summary>
public sealed class HonuaOgcFeaturesException : Exception
{
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
    /// Initializes a new instance of the <see cref="HonuaOgcFeaturesException"/> class.
    /// </summary>
    public HonuaOgcFeaturesException(
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
    /// Initializes a new instance of the <see cref="HonuaOgcFeaturesException"/> class with an inner exception.
    /// </summary>
    public HonuaOgcFeaturesException(
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
