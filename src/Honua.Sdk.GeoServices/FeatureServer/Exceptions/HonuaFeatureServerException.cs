// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.GeoServices.FeatureServer.Exceptions;

/// <summary>
/// Exception thrown when a FeatureServer request fails, including GeoServices 200-with-error responses.
/// </summary>
public sealed class HonuaFeatureServerException : Exception
{
    /// <summary>
    /// HTTP status code returned by the server, or the GeoServices error code mapped to an HTTP status.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

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

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerException"/> class.
    /// </summary>
    public HonuaFeatureServerException(
        HttpStatusCode statusCode,
        string message,
        string? responseBody = null,
        int? geoServicesErrorCode = null,
        IReadOnlyList<string>? details = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        GeoServicesErrorCode = geoServicesErrorCode;
        Details = details;
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
