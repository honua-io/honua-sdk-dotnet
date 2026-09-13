// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.GeoServices.FeatureServer.Exceptions;

/// <summary>
/// Exception thrown when a FeatureServer request fails, including GeoServices 200-with-error responses.
/// </summary>
public sealed class HonuaFeatureServerException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerException"/> class.
    /// </summary>
    public HonuaFeatureServerException()
        : this(HttpStatusCode.InternalServerError, "FeatureServer request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaFeatureServerException(string message)
        : this(HttpStatusCode.InternalServerError, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaFeatureServerException(string message, Exception innerException)
        : this(HttpStatusCode.InternalServerError, message, null, innerException)
    {
    }

    /// <summary>
    /// HTTP status code returned by the server, or the GeoServices error code mapped to an HTTP status.
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
    /// The GeoServices-specific error code, if available.
    /// </summary>
    public int? GeoServicesErrorCode { get; }

    /// <summary>
    /// Additional error detail strings from the GeoServices error response.
    /// </summary>
    public IReadOnlyList<string>? Details { get; }

    /// <summary>
    /// The raw response body, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <inheritdoc />
    public override Honua.Sdk.Abstractions.HonuaFailureReceipt? FailureReceipt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerException"/> class.
    /// </summary>
    public HonuaFeatureServerException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        int? geoServicesErrorCode = null,
        IReadOnlyList<string>? details = null)
        : this(statusCode, message, responseBody, geoServicesErrorCode, details, null)
    {
    }

    internal HonuaFeatureServerException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody,
        int? geoServicesErrorCode,
        IReadOnlyList<string>? details,
        Honua.Sdk.Abstractions.HonuaFailureReceipt? failureReceipt)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        GeoServicesErrorCode = geoServicesErrorCode;
        Details = details;
        FailureReceipt = failureReceipt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerException"/> class with an inner exception.
    /// </summary>
    public HonuaFeatureServerException(
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
