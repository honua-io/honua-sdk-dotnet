// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.ConsoleShare.Models;

/// <summary>
/// The server's standard success envelope (<c>{ "success", "data", "message",
/// "timestamp" }</c>) used by the authenticated Console open-data endpoints and
/// the anonymous open-data dataset read. The client unwraps <see cref="Data"/>.
/// </summary>
/// <typeparam name="T">Payload type carried in <see cref="Data"/>.</typeparam>
internal sealed class ApiResponseEnvelope<T>
{
    /// <summary>Whether the request was successful.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>The response payload.</summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>Optional message about the response.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
