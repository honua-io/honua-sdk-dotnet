// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Studio.Packages;

/// <summary>
/// Generic success envelope returned by the Console Studio lifecycle endpoints.
/// Mirrors the server <c>ApiResponse&lt;T&gt;</c> wrapper.
/// </summary>
/// <typeparam name="T">Type of the response data payload.</typeparam>
public sealed record StudioApiResponse<T>
    where T : class
{
    /// <summary>Whether the request was successful.</summary>
    public bool Success { get; init; }

    /// <summary>The response data payload.</summary>
    public T? Data { get; init; }

    /// <summary>Optional message about the response.</summary>
    public string? Message { get; init; }

    /// <summary>Timestamp when the response was generated.</summary>
    public DateTimeOffset Timestamp { get; init; }
}
