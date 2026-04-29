// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Spec.Models;

namespace Honua.Sdk.Spec;

/// <summary>
/// Exception thrown when the spec REST API returns an unsuccessful response.
/// </summary>
public sealed class HonuaSpecException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecException"/> class.
    /// </summary>
    public HonuaSpecException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    public HonuaSpecException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecException"/> class.
    /// </summary>
    /// <param name="message">Error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public HonuaSpecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecException"/> class.
    /// </summary>
    public HonuaSpecException(HttpStatusCode statusCode, string message, string? responseBody = null, SpecProblem? problem = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Problem = problem;
    }

    /// <summary>HTTP status code.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Raw response body when available.</summary>
    public string? ResponseBody { get; }

    /// <summary>Structured problem details when available.</summary>
    public SpecProblem? Problem { get; }
}
