// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.OgcFeatures.WfsTests.Fixtures;

/// <summary>
/// Shared test helpers for creating mock responses and clients.
/// </summary>
internal static class TestHelpers
{
    public static HonuaWfsClient CreateClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        return new HonuaWfsClient(httpClient);
    }

    public static HttpResponseMessage CreateXmlResponse(string xml, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/xml")
        };
    }

    public static HttpResponseMessage CreateGeoJsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/geo+json")
        };
    }

    public static HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, string body, string mediaType = "application/xml")
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType)
        };
    }

    /// <summary>
    /// Creates an XML wfs:FeatureCollection response matching the server's RESULTTYPE=hits format.
    /// </summary>
    public static HttpResponseMessage CreateWfsHitsXmlResponse(long numberMatched)
    {
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <wfs:FeatureCollection
              xmlns:wfs="http://www.opengis.net/wfs/2.0"
              timeStamp="2024-01-01T00:00:00Z"
              numberMatched="{numberMatched}"
              numberReturned="0" />
            """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(xml, System.Text.Encoding.UTF8, "application/gml+xml")
        };
    }
}
