// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Reflection;
using System.Text;
using Honua.Sdk.Abstractions.Console.Share;
using Honua.Sdk.ConsoleShare.Exceptions;
using Honua.Sdk.ConsoleShare.Extensions;
using Honua.Sdk.ConsoleShare.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ConsoleShare.Tests;

public sealed class HonuaConsoleShareOpenDataClientTests
{
    [Fact]
    public async Task GetPageAsync_UnwrapsEnvelopeAndDeserializesProjection()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "success": true,
                  "data": {
                    "page": {
                      "itemId": "layer-7",
                      "title": "Parcels",
                      "tags": ["cadastre"],
                      "distributions": [
                        { "accessUrl": "https://example.org/parcels.geojson", "format": "GeoJSON" }
                      ],
                      "spatialExtent": { "west": -160.3, "south": 18.9, "east": -154.8, "north": 22.3 }
                    },
                    "eligibility": {
                      "itemId": "layer-7",
                      "itemType": "layer",
                      "eligible": true,
                      "reasonCode": "eligible",
                      "reason": "Item is public-indexed and distributable.",
                      "accessTier": "public-indexed",
                      "hasPage": true
                    },
                    "stacPublication": {
                      "itemId": "layer-7",
                      "status": "published",
                      "collectionId": "col-1",
                      "revision": 3
                    },
                    "dcatValidation": { "isValid": true, "issues": [] }
                  },
                  "timestamp": "2026-06-18T00:00:00Z"
                }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var response = await client.GetPageAsync("layer-7");

        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/api/v1/console/content/layer-7/open-data", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("layer-7", response.Page.ItemId);
        Assert.Equal("Parcels", response.Page.Title);
        Assert.Equal("cadastre", Assert.Single(response.Page.Tags));
        Assert.Equal("https://example.org/parcels.geojson", Assert.Single(response.Page.Distributions).AccessUrl);
        Assert.Equal(-160.3, response.Page.SpatialExtent?.West);
        Assert.True(response.Eligibility.Eligible);
        Assert.Equal(HonuaConsoleContentItemType.Layer, response.Eligibility.ItemType);
        Assert.Equal(HonuaConsoleShareAccessTier.PublicIndexed, response.Eligibility.AccessTier);
        Assert.Equal(HonuaConsoleStacPublicationStatus.Published, response.StacPublication.Status);
        Assert.Equal("col-1", response.StacPublication.CollectionId);
        Assert.Equal(3, response.StacPublication.Revision);
        Assert.True(response.DcatValidation.IsValid);
    }

    [Fact]
    public async Task UpdatePageAsync_SerializesRequestBodyAndUsesPut()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = CreateHttpClient(async request =>
        {
            captured = request;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return CreateJsonResponse(MinimalPageEnvelope("layer-7"), HttpStatusCode.OK);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var response = await client.UpdatePageAsync("layer-7", new HonuaUpdateOpenDataPageRequest
        {
            Title = "Parcels",
            License = "CC-BY-4.0",
            Tags = ["cadastre"],
            Distributions = [new HonuaOpenDataDistribution { AccessUrl = "https://example.org/p.geojson" }],
        });

        Assert.Equal(HttpMethod.Put, captured?.Method);
        Assert.Equal("/api/v1/console/content/layer-7/open-data", captured?.RequestUri?.PathAndQuery);
        Assert.Contains("\"title\":\"Parcels\"", body, StringComparison.Ordinal);
        Assert.Contains("\"license\":\"CC-BY-4.0\"", body, StringComparison.Ordinal);
        Assert.Contains("\"accessUrl\":\"https://example.org/p.geojson\"", body, StringComparison.Ordinal);
        Assert.Equal("layer-7", response.Page.ItemId);
    }

    [Fact]
    public async Task GetEligibilityAsync_DeserializesIneligibleDecision()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "success": true,
              "data": {
                "itemId": "layer-9",
                "itemType": "layer",
                "eligible": false,
                "reasonCode": "not-public-indexed",
                "reason": "Item is not public-indexed.",
                "accessTier": "private",
                "hasPage": false
              }
            }
            """));
        var client = new HonuaConsoleShareOpenDataClient(http);

        var eligibility = await client.GetEligibilityAsync("layer-9");

        Assert.False(eligibility.Eligible);
        Assert.Equal("not-public-indexed", eligibility.ReasonCode);
        Assert.Equal(HonuaConsoleShareAccessTier.Private, eligibility.AccessTier);
        Assert.False(eligibility.HasPage);
    }

    [Fact]
    public async Task PreviewDcatAsync_DeserializesCatalogAndValidation()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "success": true,
                  "data": {
                    "catalog": {
                      "@context": "https://project-open-data.cio.gov/v1.1/schema/catalog.jsonld",
                      "@type": "dcat:Catalog",
                      "dataset": [
                        {
                          "@type": "dcat:Dataset",
                          "identifier": "layer-7",
                          "title": "Parcels",
                          "accessLevel": "public",
                          "distribution": [
                            { "@type": "dcat:Distribution", "accessURL": "https://example.org/p.geojson", "format": "GeoJSON" }
                          ]
                        }
                      ]
                    },
                    "validation": {
                      "isValid": false,
                      "issues": [ { "field": "license", "severity": "warning", "message": "License recommended." } ]
                    }
                  }
                }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var export = await client.PreviewDcatAsync("layer-7");

        Assert.Equal("/api/v1/console/content/layer-7/open-data/dcat", captured?.RequestUri?.PathAndQuery);
        var dataset = Assert.Single(export.Catalog.Dataset);
        Assert.Equal("layer-7", dataset.Identifier);
        Assert.Equal("https://example.org/p.geojson", Assert.Single(dataset.Distribution!).AccessUrl);
        Assert.False(export.Validation.IsValid);
        var issue = Assert.Single(export.Validation.Issues);
        Assert.Equal(HonuaConsoleOpenDataValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public async Task PublishStacAsync_PostsToPublishRoute()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                { "success": true, "data": { "itemId": "layer-7", "status": "published", "collectionId": "col-1", "revision": 1 } }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var state = await client.PublishStacAsync("layer-7");

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/api/v1/console/content/layer-7/open-data/stac/publish", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(HonuaConsoleStacPublicationStatus.Published, state.Status);
        Assert.Equal("col-1", state.CollectionId);
    }

    [Fact]
    public async Task PublishStacAsync_Conflict_ThrowsApiExceptionWithProblemDetail()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            { "title": "Validation failed", "detail": "Open-data page has blocking errors." }
            """,
            HttpStatusCode.Conflict));
        var client = new HonuaConsoleShareOpenDataClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareApiException>(() => client.PublishStacAsync("layer-7"));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("Open-data page has blocking errors.", ex.Message);
    }

    [Fact]
    public async Task UnpublishStacAsync_DeletesStacRoute()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse("""{ "success": true, "data": { "itemId": "layer-7", "status": "unpublished", "revision": 2 } }""");
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var state = await client.UnpublishStacAsync("layer-7");

        Assert.Equal(HttpMethod.Delete, captured?.Method);
        Assert.Equal("/api/v1/console/content/layer-7/open-data/stac", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(HonuaConsoleStacPublicationStatus.Unpublished, state.Status);
    }

    [Fact]
    public async Task GetPublicDatasetAsync_UnwrapsEnvelopeFromPublicRoute()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse("""{ "success": true, "data": { "itemId": "layer-7", "title": "Parcels", "tags": [], "distributions": [], "provenanceRefs": [] } }""");
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var page = await client.GetPublicDatasetAsync("layer-7");

        Assert.Equal("/api/v1/open-data/datasets/layer-7", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("layer-7", page.ItemId);
        Assert.Equal("Parcels", page.Title);
    }

    [Fact]
    public async Task GetPublicDatasetAsync_NotFound_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """{ "success": false, "message": "Open-data dataset not found." }""",
            HttpStatusCode.NotFound));
        var client = new HonuaConsoleShareOpenDataClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareApiException>(() => client.GetPublicDatasetAsync("private-1"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetPublicDataJsonAsync_ReadsRawCatalogDocument()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "@context": "https://project-open-data.cio.gov/v1.1/schema/catalog.jsonld",
                  "@type": "dcat:Catalog",
                  "conformsTo": "https://project-open-data.cio.gov/v1.1/schema",
                  "dataset": [ { "@type": "dcat:Dataset", "identifier": "layer-7", "title": "Parcels", "accessLevel": "public" } ]
                }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var catalog = await client.GetPublicDataJsonAsync("layer-7");

        Assert.Equal("/api/v1/open-data/datasets/layer-7/data.json", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("dcat:Catalog", catalog.Type);
        Assert.Equal("layer-7", Assert.Single(catalog.Dataset).Identifier);
    }

    [Fact]
    public async Task GetPublicSchemaOrgAsync_ReadsRawJsonLd()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "@context": "https://schema.org",
              "@type": "Dataset",
              "name": "Parcels",
              "spatialCoverage": { "@type": "Place", "geo": { "@type": "GeoShape", "box": "18.9 -160.3 22.3 -154.8" } }
            }
            """));
        var client = new HonuaConsoleShareOpenDataClient(http);

        var dataset = await client.GetPublicSchemaOrgAsync("layer-7");

        Assert.Equal("Dataset", dataset.Type);
        Assert.Equal("Parcels", dataset.Name);
        Assert.Equal("18.9 -160.3 22.3 -154.8", dataset.SpatialCoverage?.Geo.Box);
    }

    [Fact]
    public async Task GetPublicStacCatalogAsync_ReadsRootCatalog()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "stac_version": "1.0.0",
                  "type": "Catalog",
                  "id": "honua-open-data",
                  "description": "Published open-data items.",
                  "links": [ { "rel": "self", "href": "https://h/api/v1/open-data/stac" } ]
                }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var catalog = await client.GetPublicStacCatalogAsync();

        Assert.Equal("/api/v1/open-data/stac", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("1.0.0", catalog.StacVersion);
        Assert.Equal("honua-open-data", catalog.Id);
        Assert.Equal("self", Assert.Single(catalog.Links).Rel);
    }

    [Fact]
    public async Task GetPublicStacCollectionAsync_ReadsCollectionWithExtent()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "stac_version": "1.0.0",
                  "type": "Collection",
                  "id": "col-1",
                  "description": "Parcels",
                  "license": "CC-BY-4.0",
                  "extent": {
                    "spatial": { "bbox": [ [ -160.3, 18.9, -154.8, 22.3 ] ] },
                    "temporal": { "interval": [ [ "2020-01-01T00:00:00Z", null ] ] }
                  },
                  "links": []
                }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var collection = await client.GetPublicStacCollectionAsync("col-1");

        Assert.Equal("/api/v1/open-data/stac/collections/col-1", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("col-1", collection.Id);
        Assert.Equal("CC-BY-4.0", collection.License);
        Assert.Equal(-160.3, collection.Extent.Spatial.Bbox[0][0]);
        Assert.Null(collection.Extent.Temporal.Interval[0][1]);
    }

    [Fact]
    public async Task GetPublicStacItemAsync_ReadsRepresentativeItem()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "stac_version": "1.0.0",
                  "type": "Feature",
                  "id": "col-1",
                  "collection": "col-1",
                  "bbox": [ -160.3, 18.9, -154.8, 22.3 ],
                  "geometry": { "type": "Polygon", "coordinates": [ [ [ -160.3, 18.9 ], [ -154.8, 18.9 ], [ -154.8, 22.3 ], [ -160.3, 22.3 ], [ -160.3, 18.9 ] ] ] },
                  "properties": { "title": "Parcels" },
                  "links": []
                }
                """);
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var item = await client.GetPublicStacItemAsync("col-1", "col-1");

        Assert.Equal("/api/v1/open-data/stac/collections/col-1/items/col-1", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("Feature", item.Type);
        Assert.Equal("col-1", item.Collection);
        Assert.Equal("Polygon", item.Geometry?.Type);
        Assert.Equal("Parcels", item.Properties["title"]);
    }

    [Fact]
    public async Task GetPageAsync_MalformedBody_ThrowsContractException()
    {
        using var http = CreateHttpClient(_ => JsonResponse("not-json", HttpStatusCode.OK));
        var client = new HonuaConsoleShareOpenDataClient(http);

        await Assert.ThrowsAsync<HonuaConsoleShareContractException>(() => client.GetPageAsync("layer-7"));
    }

    [Fact]
    public async Task GetPageAsync_EmptyEnvelope_ThrowsContractException()
    {
        using var http = CreateHttpClient(_ => JsonResponse("""{ "success": true }"""));
        var client = new HonuaConsoleShareOpenDataClient(http);

        await Assert.ThrowsAsync<HonuaConsoleShareContractException>(() => client.GetPageAsync("layer-7"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageAsync_BlankItemId_ThrowsArgumentException(string itemId)
    {
        using var http = CreateHttpClient(_ => JsonResponse("{}"));
        var client = new HonuaConsoleShareOpenDataClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageAsync(itemId));
    }

    [Fact]
    public void AddHonuaConsoleShareOpenData_RegistersClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaConsoleShareOpenData(options =>
        {
            options.BaseAddress = new Uri("https://honua.example");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaConsoleShareOpenDataClient>();

        Assert.IsType<HonuaConsoleShareOpenDataClient>(client);
    }

    [Fact]
    public void AddHonuaConsoleShareOpenData_WithRetryEnabled_LeavesTimeoutToResiliencePipeline()
    {
        var services = new ServiceCollection();
        services.AddHonuaConsoleShareOpenData(options =>
        {
            options.BaseAddress = new Uri("https://honua.example");
            options.EnableRetry = true;
            options.Timeout = TimeSpan.FromSeconds(42);
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaConsoleShareOpenDataClient>();

        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, GetHttpClient(client).Timeout);
    }

    private static string MinimalPageEnvelope(string itemId)
        => $$"""
            {
              "success": true,
              "data": {
                "page": { "itemId": "{{itemId}}", "tags": [], "distributions": [], "provenanceRefs": [] },
                "eligibility": { "itemId": "{{itemId}}", "itemType": "layer", "eligible": true, "reasonCode": "eligible", "reason": "ok", "accessTier": "public-indexed", "hasPage": true },
                "stacPublication": { "itemId": "{{itemId}}", "status": "unpublished", "revision": 0 },
                "dcatValidation": { "isValid": true, "issues": [] }
              }
            }
            """;

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler))
        {
            BaseAddress = new Uri("https://honua.example"),
        };

    private static HttpClient GetHttpClient(object client)
    {
        var field = client.GetType().GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        return Assert.IsType<HttpClient>(field!.GetValue(client));
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => CreateHttpClient(request => Task.FromResult(handler(request)));

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}
