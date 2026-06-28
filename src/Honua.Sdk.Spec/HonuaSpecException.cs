// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Spec.Models;

namespace Honua.Sdk.Spec;

/// <summary>
/// Exception thrown when the spec REST API returns an unsuccessful response.
/// </summary>
public sealed class HonuaSpecException : Honua.Sdk.Abstractions.HonuaException
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

    /// <inheritdoc />
    public override int? HttpStatus => (int)StatusCode;

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaProblemDetails? ProblemDetails =>
        Problem is { } problem
            ? new Honua.Sdk.Abstractions.HonuaProblemDetails
            {
                Type = problem.Type,
                Title = problem.Title,
                Status = problem.Status,
                Detail = problem.Detail,
                Instance = problem.NodeId,
            }
            : new Honua.Sdk.Abstractions.HonuaProblemDetails
            {
                Status = (int)StatusCode,
                Detail = Message,
            };

    /// <summary>Raw response body when available.</summary>
    public string? ResponseBody { get; }

    /// <summary>Structured problem details when available.</summary>
    public SpecProblem? Problem { get; }
}
