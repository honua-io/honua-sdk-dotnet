// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.OgcFeatures.Styles;

namespace Honua.Sdk.OgcFeatures.Tests.Fixtures;

/// <summary>
/// Shared test helpers for creating mock responses and clients.
/// </summary>
internal static class TestHelpers
{
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

    public static HonuaOgcStylesClient CreateOgcStylesClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        return new HonuaOgcStylesClient(httpClient);
    }

    public static HttpResponseMessage CreateRawResponse(
        string content, string mediaType, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var httpContent = new StringContent(content, System.Text.Encoding.UTF8);
        httpContent.Headers.ContentType =
            System.Net.Http.Headers.MediaTypeHeaderValue.Parse(mediaType);
        return new HttpResponseMessage(statusCode)
        {
            Content = httpContent
        };
    }

    public static HttpResponseMessage CreateRawJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
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
