// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Features.FeatureServer;
using Honua.Sdk.Features.OgcFeatures;

namespace Honua.Sdk.Features.Tests.Fixtures;

/// <summary>
/// Shared test helpers for creating mock responses and clients.
/// </summary>
internal static class TestHelpers
{
    public static HonuaFeatureServerClient CreateFeatureServerClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        return new HonuaFeatureServerClient(httpClient);
    }

    public static HonuaOgcFeaturesClient CreateOgcFeaturesClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        return new HonuaOgcFeaturesClient(httpClient);
    }

    public static HttpResponseMessage CreateJsonResponse<T>(T data, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    public static HttpResponseMessage CreateRawJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    public static HttpResponseMessage CreateGeoServicesErrorResponse(int code, string message, string[]? details = null)
    {
        var error = new
        {
            error = new
            {
                code,
                message,
                details = details ?? []
            }
        };

        var json = JsonSerializer.Serialize(error);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    public static HttpResponseMessage CreateProblemDetailsResponse(
        HttpStatusCode statusCode, string title, string detail, string? type = null)
    {
        var body = new
        {
            type,
            title,
            status = (int)statusCode,
            detail
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    public static HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, string message)
    {
        var body = new { message };
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}
