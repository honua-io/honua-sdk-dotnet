// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Encodes one URI path segment and rejects values that HTTP dot-segment removal would collapse.
/// </summary>
/// <remarks>
/// <see cref="Uri.EscapeDataString(string)"/> leaves <c>.</c> and <c>..</c> unchanged.
/// <see cref="HttpClient"/> then merges a relative URI into <see cref="HttpClient.BaseAddress"/>
/// with RFC 3986 <c>remove_dot_segments</c>, so those two values walk off the intended route.
/// A slash is percent-encoded and stays inside the one segment.
/// </remarks>
public static class HonuaPathSegment
{
    /// <summary>
    /// Returns <paramref name="value"/> encoded as a single path segment.
    /// </summary>
    /// <param name="value">The raw segment text.</param>
    /// <param name="paramName">The parameter name reported when <paramref name="value"/> is rejected.</param>
    /// <returns>The encoded segment.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, whitespace, <c>.</c>, <c>..</c>, or contains a control character,
    /// or <paramref name="paramName"/> is null or whitespace.
    /// </exception>
    public static string Escape(string? value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paramName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier must be supplied.", paramName);
        }

        if (value is "." or "..")
        {
            throw new ArgumentException("Identifier must not be a dot segment.", paramName);
        }

        foreach (var ch in value)
        {
            if (char.IsControl(ch))
            {
                throw new ArgumentException("Identifier must not contain control characters.", paramName);
            }
        }

        return Uri.EscapeDataString(value);
    }
}
