// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Exceptions;

/// <summary>
/// Exception thrown when a successful Studio API response does not satisfy the
/// expected contract (for example an empty body or JSON that does not match the
/// modeled report shape). Distinct from <see cref="HonuaStudioApiException"/>,
/// which signals an unsuccessful HTTP status.
/// </summary>
public sealed class HonuaStudioContractException : Honua.Sdk.Abstractions.HonuaException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioContractException"/> class.
    /// </summary>
    public HonuaStudioContractException()
        : this("Honua Studio response did not satisfy the expected contract.", operation: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioContractException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public HonuaStudioContractException(string message)
        : this(message, operation: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioContractException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaStudioContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The operation that produced the malformed response.
    /// </summary>
    public string? Operation { get; }

    /// <summary>
    /// The raw response body, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioContractException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="operation">The operation that produced the malformed response.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    public HonuaStudioContractException(string message, string? operation, string? responseBody = null)
        : base(message)
    {
        Operation = operation;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioContractException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="operation">The operation that produced the malformed response.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public HonuaStudioContractException(string message, string? operation, string? responseBody, Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
        ResponseBody = responseBody;
    }
}
