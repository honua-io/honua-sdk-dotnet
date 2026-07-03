// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Models;

namespace Honua.Sdk.Studio.Internal;

/// <summary>
/// Shared HTTP response handling for Studio clients: RFC 7807 problem mapping to
/// <see cref="HonuaStudioApiException"/> and source-generated deserialization of
/// successful bodies to <see cref="HonuaStudioContractException"/> on drift.
/// </summary>
internal static class StudioHttpResponseReader
{
    /// <summary>
    /// Reads a successful JSON response body into <typeparamref name="T"/>, mapping
    /// error status codes and malformed/empty bodies to the appropriate Studio
    /// exception.
    /// </summary>
    public static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        string operation,
        CancellationToken cancellationToken)
        where T : class
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (JsonException ex)
        {
            throw new HonuaStudioContractException(
                $"Failed to deserialize the Studio '{operation}' response envelope.",
                operation,
                body,
                ex);
        }

        return value
            ?? throw new HonuaStudioContractException(
                $"Server returned an empty Studio '{operation}' response envelope.",
                operation,
                body);
    }

    /// <summary>Builds a <see cref="HonuaStudioApiException"/> from an error response body.</summary>
    public static HonuaStudioApiException CreateApiException(HttpStatusCode statusCode, string body)
    {
        if (TryParseProblem(body, out var problem) && problem is not null)
        {
            var message = problem.Detail ?? problem.Title ?? "Studio API request failed.";
            return new HonuaStudioApiException(statusCode, message, body, problem.Title, problem.Detail);
        }

        return new HonuaStudioApiException(statusCode, "Studio API request failed.", body);
    }

    private static bool TryParseProblem(string body, out StudioProblem? problem)
    {
        try
        {
            problem = JsonSerializer.Deserialize(body, StudioJsonContext.Default.StudioProblem);
            return problem is not null;
        }
        catch (JsonException)
        {
            problem = null;
            return false;
        }
    }
}
