// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Styles.Models;
using Honua.Sdk.OgcFeatures.Tests.Fixtures;

namespace Honua.Sdk.OgcFeatures.Tests.OgcFeatures;

public class HonuaOgcStylesClientTests
{
    // ── ListStylesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ListStylesAsync_ReturnsStylesAndDefault()
    {
        var json = """
        {
            "styles": [
                {
                    "id": "topographic",
                    "title": "Topographic",
                    "links": [
                        { "href": "/ogc/styles/topographic", "rel": "stylesheet", "type": "application/vnd.mapbox.style+json" }
                    ]
                },
                {
                    "id": "satellite",
                    "title": "Satellite",
                    "links": []
                }
            ],
            "default": "topographic",
            "links": [
                { "href": "/ogc/styles", "rel": "self", "type": "application/json" }
            ]
        }
        """;
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcStylesClient(req =>
        {
            captured = req;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.ListStylesAsync();

        Assert.Equal(2, result.Styles.Count);
        Assert.Equal("topographic", result.Styles[0].Id);
        Assert.Equal("Topographic", result.Styles[0].Title);
        Assert.Single(result.Styles[0].Links);
        Assert.Equal("stylesheet", result.Styles[0].Links[0].Rel);
        Assert.Equal("topographic", result.Default);
        Assert.NotNull(result.Links);
        Assert.NotNull(captured);
        Assert.Equal("/ogc/styles?f=json", captured!.RequestUri!.PathAndQuery);
    }

    // ── GetStylesheetAsync (content negotiation) ────────────────────

    [Fact]
    public async Task GetStylesheetAsync_DefaultEncoding_SendsMapboxAcceptHeader()
    {
        const string style = """{ "version": 8, "layers": [] }""";
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcStylesClient(req =>
        {
            captured = req;
            return Task.FromResult(
                TestHelpers.CreateRawResponse(style, "application/vnd.mapbox.style+json"));
        });

        var result = await client.GetStylesheetAsync("topographic");

        Assert.Equal("topographic", result.StyleId);
        Assert.Equal(OgcStyleEncoding.MapboxStyle, result.Encoding);
        Assert.Equal("application/vnd.mapbox.style+json", result.MediaType);
        Assert.Equal(style, result.Content);
        Assert.NotNull(captured);
        Assert.Equal("/ogc/styles/topographic", captured!.RequestUri!.PathAndQuery);
        Assert.Contains(
            captured.Headers.Accept,
            h => h.MediaType == "application/vnd.mapbox.style+json");
    }

    [Theory]
    [InlineData(OgcStyleEncoding.Sld10, "application/vnd.ogc.sld+xml", "1.0")]
    [InlineData(OgcStyleEncoding.Sld11, "application/vnd.ogc.sld+xml", "1.1")]
    public async Task GetStylesheetAsync_SldEncoding_SendsVersionedAcceptHeader(
        OgcStyleEncoding encoding, string expectedMediaType, string expectedVersion)
    {
        const string sld = "<StyledLayerDescriptor/>";
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcStylesClient(req =>
        {
            captured = req;
            return Task.FromResult(
                TestHelpers.CreateRawResponse(sld, $"{expectedMediaType};version={expectedVersion}"));
        });

        var result = await client.GetStylesheetAsync("topographic", encoding);

        Assert.Equal(encoding, result.Encoding);
        Assert.Equal(sld, result.Content);
        Assert.NotNull(captured);
        var accept = Assert.Single(captured!.Headers.Accept);
        Assert.Equal(expectedMediaType, accept.MediaType);
        Assert.Contains(
            accept.Parameters,
            p => p.Name == "version" && p.Value == expectedVersion);
    }

    [Fact]
    public async Task GetStylesheetAsync_NotAcceptable_ThrowsWithStatusCode()
    {
        var client = TestHelpers.CreateOgcStylesClient(_ =>
            Task.FromResult(TestHelpers.CreateProblemDetailsResponse(
                HttpStatusCode.NotAcceptable, "Not Acceptable", "Unsupported stylesheet media type.")));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetStylesheetAsync("topographic", OgcStyleEncoding.Sld11));

        Assert.Equal(HttpStatusCode.NotAcceptable, ex.StatusCode);
    }

    [Fact]
    public async Task GetStylesheetAsync_EmptyStyleId_Throws()
    {
        var client = TestHelpers.CreateOgcStylesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetStylesheetAsync("  "));
    }

    // ── GetStyleMetadataAsync ───────────────────────────────────────

    [Fact]
    public async Task GetStyleMetadataAsync_ReturnsMetadata()
    {
        var json = """
        {
            "id": "topographic",
            "title": "Topographic",
            "description": "Default topographic basemap",
            "keywords": ["basemap", "topo"],
            "license": "CC-BY-4.0",
            "version": "3",
            "links": [
                { "href": "/ogc/styles/topographic", "rel": "stylesheet", "type": "application/vnd.mapbox.style+json" }
            ]
        }
        """;
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcStylesClient(req =>
        {
            captured = req;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.GetStyleMetadataAsync("topographic");

        Assert.Equal("topographic", result.Id);
        Assert.Equal("Topographic", result.Title);
        Assert.Equal("Default topographic basemap", result.Description);
        Assert.NotNull(result.Keywords);
        Assert.Equal(2, result.Keywords!.Count);
        Assert.Equal("CC-BY-4.0", result.License);
        Assert.Equal("3", result.Version);
        Assert.Single(result.Links);
        Assert.NotNull(captured);
        Assert.Equal("/ogc/styles/topographic/metadata?f=json", captured!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetStyleMetadataAsync_NotFound_ThrowsWithStatusCode()
    {
        var client = TestHelpers.CreateOgcStylesClient(_ =>
            Task.FromResult(TestHelpers.CreateProblemDetailsResponse(
                HttpStatusCode.NotFound, "Not Found", "Style 'missing' not found.")));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetStyleMetadataAsync("missing"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── UpdateStyleAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpdateStyleAsync_PutsMapboxStyleJson()
    {
        const string style = """{ "version": 8, "name": "topo", "layers": [] }""";
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var client = TestHelpers.CreateOgcStylesClient(async req =>
        {
            captured = req;
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await client.UpdateStyleAsync("topographic", style);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.Equal("/ogc/styles/topographic", captured.RequestUri!.PathAndQuery);
        Assert.Equal(
            "application/vnd.mapbox.style+json",
            captured.Content!.Headers.ContentType!.MediaType);
        Assert.Equal(style, capturedBody);
        Assert.DoesNotContain(captured.Headers, h => h.Key == "Prefer");
    }

    [Fact]
    public async Task UpdateStyleAsync_Strict_SendsPreferHandlingStrict()
    {
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcStylesClient(req =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        await client.UpdateStyleAsync("topographic", """{ "version": 8 }""", strict: true);

        Assert.NotNull(captured);
        Assert.True(captured!.Headers.TryGetValues("Prefer", out var prefer));
        Assert.Contains("handling=strict", prefer!);
    }

    [Fact]
    public async Task UpdateStyleAsync_InvalidStyle_ThrowsWithStatusCode()
    {
        var client = TestHelpers.CreateOgcStylesClient(_ =>
            Task.FromResult(TestHelpers.CreateProblemDetailsResponse(
                HttpStatusCode.BadRequest, "Bad Request", "MapLibre style is invalid.")));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.UpdateStyleAsync("topographic", """{ "bad": true }""", strict: true));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateStyleAsync_EmptyBody_Throws()
    {
        var client = TestHelpers.CreateOgcStylesClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.UpdateStyleAsync("topographic", "   "));
    }
}
