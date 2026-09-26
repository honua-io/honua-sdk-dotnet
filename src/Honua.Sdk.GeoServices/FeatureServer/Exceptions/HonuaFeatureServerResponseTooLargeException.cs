// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.GeoServices.FeatureServer.Exceptions;

/// <summary>
/// Exception thrown when a FeatureServer or MapServer response body (JSON or error) exceeds the configured
/// <see cref="HonuaFeatureServerClientOptions.MaxResponseBytes"/>. The body is abandoned once the ceiling is
/// crossed, or before any byte is read when <c>Content-Length</c> already declares a larger body.
/// </summary>
public sealed class HonuaFeatureServerResponseTooLargeException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerResponseTooLargeException"/> class.
    /// </summary>
    public HonuaFeatureServerResponseTooLargeException()
        : this("FeatureServer response exceeded the configured size limit.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerResponseTooLargeException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaFeatureServerResponseTooLargeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerResponseTooLargeException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaFeatureServerResponseTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    internal HonuaFeatureServerResponseTooLargeException(
        HttpStatusCode statusCode,
        Uri? requestUri,
        long maxResponseBytes,
        long? declaredContentLength)
        : base(declaredContentLength is { } declared
            ? $"FeatureServer response declares {declared} bytes, exceeding the {maxResponseBytes}-byte limit."
            : $"FeatureServer response exceeded the {maxResponseBytes}-byte limit.")
    {
        StatusCode = statusCode;
        RequestUri = requestUri;
        MaxResponseBytes = maxResponseBytes;
        DeclaredContentLength = declaredContentLength;
    }

    /// <summary>HTTP status code of the oversized response.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <inheritdoc />
    public override int? HttpStatus => StatusCode == default ? null : (int)StatusCode;

    /// <summary>The request URI whose response was refused, when known.</summary>
    public Uri? RequestUri { get; }

    /// <summary>The byte ceiling that was exceeded.</summary>
    public long MaxResponseBytes { get; }

    /// <summary>The declared <c>Content-Length</c> when it alone exceeded the ceiling; null when the body was cut off while streaming.</summary>
    public long? DeclaredContentLength { get; }
}
