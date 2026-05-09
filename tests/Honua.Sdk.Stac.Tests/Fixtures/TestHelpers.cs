// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;

namespace Honua.Sdk.Stac.Tests.Fixtures;

internal static class TestHelpers
{
    public static HonuaStacClient CreateStacClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://honua.example.test")
        };

        return new HonuaStacClient(httpClient);
    }

    public static HttpResponseMessage CreateRawJsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    public static HttpResponseMessage CreateRawJsonResponse<T>(T value)
        => CreateRawJsonResponse(JsonSerializer.Serialize(value));
}
