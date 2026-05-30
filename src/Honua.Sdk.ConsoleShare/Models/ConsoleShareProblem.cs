// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.ConsoleShare.Models;

/// <summary>
/// Minimal projection of the RFC 7807 problem-details document the Console Share
/// endpoints return on error, used to surface a meaningful message on
/// <see cref="Exceptions.HonuaConsoleShareApiException"/>.
/// </summary>
internal sealed record ConsoleShareProblem
{
    /// <summary>Problem type URI.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Short, human-readable summary of the problem.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>HTTP status code.</summary>
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    /// <summary>Human-readable explanation specific to this occurrence.</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}
