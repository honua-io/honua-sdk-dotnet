// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Tests.Fixtures;

namespace Honua.Sdk.OgcFeatures.Tests.OgcFeatures;

public class HonuaOgcFeaturesClientTests
{
    // ── GetLandingPageAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetLandingPageAsync_ReturnsLandingPage()
    {
        var json = """
        {
            "title": "Honua OGC Features",
            "description": "Feature access API",
            "links": [
                { "href": "/ogc/features", "rel": "self", "type": "application/json" }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetLandingPageAsync();

        Assert.Equal("Honua OGC Features", result.Title);
        Assert.NotNull(result.Links);
        Assert.Single(result.Links);
    }

    // ── GetConformanceAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetConformanceAsync_ReturnsConformance()
    {
        var json = """
        {
            "conformsTo": [
                "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
                "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson"
            ]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetConformanceAsync();

        Assert.NotNull(result.ConformsTo);
        Assert.Equal(2, result.ConformsTo.Count);
    }

    // ── ListCollectionsAsync ────────────────────────────────────────

    [Fact]
    public async Task ListCollectionsAsync_ReturnsCollections()
    {
        var json = """
        {
            "collections": [
                { "id": "buildings", "title": "Buildings" },
                { "id": "roads", "title": "Roads" }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.ListCollectionsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("buildings", result[0].Id);
        Assert.Equal("Roads", result[1].Title);
    }

    // ── GetCollectionAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetCollectionAsync_ReturnsCollection()
    {
        var json = """
        {
            "id": "buildings",
            "title": "Buildings",
            "description": "Building footprints",
            "crs": ["http://www.opengis.net/def/crs/OGC/1.3/CRS84"],
            "storageCrs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "extent": {
                "spatial": {
                    "bbox": [[-180, -90, 180, 90]],
                    "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                }
            }
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetCollectionAsync("buildings");

        Assert.Equal("buildings", result.Id);
        Assert.Equal("Buildings", result.Title);
        Assert.NotNull(result.Extent?.Spatial?.Bbox);
    }

    // ── GetQueryablesAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetQueryablesAsync_ReturnsQueryables()
    {
        var json = """
        {
            "type": "object",
            "title": "Buildings queryables",
            "properties": {
                "name": { "type": "string" },
                "height": { "type": "number" }
            },
            "required": ["name"]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetQueryablesAsync("buildings");

        Assert.Equal("object", result.Type);
        Assert.NotNull(result.Properties);
        Assert.Equal(2, result.Properties.Count);
        Assert.NotNull(result.Required);
        Assert.Single(result.Required);
    }

    [Fact]
    public async Task GetDescriptorAsync_SharedAbstraction_MapsCollectionAndQueryables()
    {
        var collectionJson = """
        {
            "id": "buildings",
            "title": "Buildings",
            "storageCrs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
            "extent": {
                "spatial": {
                    "bbox": [[-180, -90, 180, 90]],
                    "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                }
            }
        }
        """;
        var queryablesJson = """
        {
            "type": "object",
            "properties": {
                "id": { "type": "string", "title": "Identifier" },
                "name": { "type": "string", "title": "Name", "maxLength": 100 },
                "status": { "type": "string", "enum": ["open", "closed"], "default": "open" }
            },
            "required": ["id", "name"]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            var url = req.RequestUri?.ToString();
            return Task.FromResult(url?.Contains("/queryables", StringComparison.Ordinal) == true
                ? TestHelpers.CreateRawJsonResponse(queryablesJson)
                : TestHelpers.CreateRawJsonResponse(collectionJson));
        });

        var descriptor = await ((IHonuaFeatureDescriptorClient)client).GetDescriptorAsync(new SourceDescriptor
        {
            Id = "buildings",
            Protocol = FeatureProtocolIds.OgcFeatures,
            Locator = new SourceLocator { CollectionId = "buildings" }
        });

        Assert.NotNull(descriptor.Schema);
        Assert.Equal("id", descriptor.Schema.PrimaryKey);
        Assert.Equal("http://www.opengis.net/def/crs/OGC/1.3/CRS84", descriptor.Schema.SpatialReference);
        Assert.Equal(-180, descriptor.Schema.Extent?.MinX);
        Assert.Contains(FeatureCapabilities.QueryObjectIds, descriptor.Capabilities);
        Assert.DoesNotContain(FeatureCapabilities.TimeFilter, descriptor.Capabilities);
        Assert.Contains(FeatureCapabilities.ApplyEdits, descriptor.Capabilities);
        Assert.DoesNotContain(FeatureCapabilities.QueryAggregate, descriptor.Capabilities);
        var name = Assert.Single(descriptor.Schema.Fields, field => field.Name == "name");
        Assert.Equal("Name", name.Alias);
        Assert.Equal("string", name.Type);
        Assert.False(name.Nullable);
        Assert.True(name.Required);
        Assert.Equal(100, name.Length);
        var status = Assert.Single(descriptor.Schema.Fields, field => field.Name == "status");
        Assert.True(status.Nullable);
        Assert.Equal("open", status.DefaultValue?.GetString());
        Assert.Equal(JsonValueKind.Array, status.Domain?.ValueKind);
    }

    // ── GetItemsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_ReturnsFeatureCollection()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "numberMatched": 100,
            "numberReturned": 2,
            "features": [
                {
                    "type": "Feature",
                    "id": "1",
                    "geometry": { "type": "Point", "coordinates": [-118.0, 34.0] },
                    "properties": { "name": "Building A" }
                },
                {
                    "type": "Feature",
                    "id": "2",
                    "geometry": { "type": "Point", "coordinates": [-117.0, 33.0] },
                    "properties": { "name": "Building B" }
                }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetItemsAsync("buildings");

        Assert.Equal("FeatureCollection", result.Type);
        Assert.Equal(100, result.NumberMatched);
        Assert.Equal(2, result.NumberReturned);
        Assert.NotNull(result.Features);
        Assert.Equal(2, result.Features.Count);
    }

    [Fact]
    public async Task GetItemsAsync_SerializesQueryParams()
    {
        string? capturedUrl = null;
        var json = """{ "type": "FeatureCollection", "features": [] }""";
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new OgcItemsParams
        {
            Limit = 10,
            Offset = 20,
            Bbox = [-118.5, 33.5, -117.5, 34.5],
            Datetime = "2020-01-01/2020-12-31",
            Filter = "name LIKE 'Test%'",
            FilterLang = "cql2-text",
            Sortby = "+name",
            Properties = "name,height",
        };

        await client.GetItemsAsync("buildings", query);

        Assert.NotNull(capturedUrl);
        Assert.Contains("limit=10", capturedUrl);
        Assert.Contains("offset=20", capturedUrl);
        Assert.Contains("bbox=-118.5", capturedUrl);
        Assert.Contains("datetime=", capturedUrl);
        Assert.Contains("filter=", capturedUrl);
        Assert.Contains("filter-lang=cql2-text", capturedUrl);
        Assert.Contains("sortby=", capturedUrl);
        Assert.Contains("properties=name%2Cheight", capturedUrl);
    }

    [Fact]
    public async Task GetItemsAsync_SharedAbstraction_SerializesProviderNeutralParams()
    {
        string? capturedUrl = null;
        var json = """
        {
            "type": "FeatureCollection",
            "numberMatched": 1,
            "numberReturned": 1,
            "features": [
                {
                    "type": "Feature",
                    "id": "building-7",
                    "geometry": { "type": "Point", "coordinates": [-118.0, 34.0] },
                    "properties": { "name": "Building A" }
                }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });
        var start = new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2024, 2, 29, 0, 0, 0, TimeSpan.Zero);

        var result = await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { CollectionId = "buildings" },
            Filter = "height > 10",
            FilterLanguage = FeatureFilterLanguage.Cql2Text,
            FeatureIds = ["building-7"],
            OutFields = ["name", "height"],
            Limit = 5,
            Offset = 10,
            OrderBy = "+name",
            TimeFilter = new FeatureTimeFilter { Start = start, End = end },
            Bbox = new FeatureBoundingBox
            {
                MinX = -118.5,
                MinY = 33.5,
                MaxX = -117.5,
                MaxY = 34.5,
                Crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
            },
            OutputCrs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        });

        Assert.NotNull(capturedUrl);
        var decodedUrl = WebUtility.UrlDecode(capturedUrl);
        Assert.Contains("/ogc/features/collections/buildings/items", capturedUrl);
        Assert.Contains("limit=5", capturedUrl);
        Assert.Contains("offset=10", capturedUrl);
        Assert.Contains($"datetime={start:O}/{end:O}", decodedUrl);
        Assert.Contains("filter=", capturedUrl);
        Assert.Contains("filter-lang=cql2-text", capturedUrl);
        Assert.Contains("ids=building-7", capturedUrl);
        Assert.Contains("properties=name%2Cheight", capturedUrl);
        Assert.Contains("sortby=", capturedUrl);
        Assert.Contains("bbox=", capturedUrl);
        Assert.Contains("bbox-crs=", capturedUrl);
        Assert.Contains("crs=", capturedUrl);
        Assert.Equal("ogc-features", result.ProviderName);
        Assert.Single(result.Features);
        Assert.Equal("building-7", result.Features[0].Id);
    }

    [Fact]
    public async Task GetItemsAsync_SharedAbstraction_ProviderDefaultFilter_DoesNotSendFilterLang()
    {
        string? capturedUrl = null;
        var json = """{ "type": "FeatureCollection", "features": [] }""";
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { CollectionId = "buildings" },
            Filter = "height > 10",
        });

        Assert.NotNull(capturedUrl);
        Assert.Contains("filter=", capturedUrl);
        Assert.DoesNotContain("filter-lang=", capturedUrl);
    }

    [Fact]
    public async Task GetItemsAsync_SharedAbstraction_UnsupportedQueryModeThrows()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
            {
                Source = new FeatureSource { CollectionId = "buildings" },
                ReturnCountOnly = true,
            }));

        Assert.Contains("count-only", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetItemsAsync_SharedAbstraction_UnsupportedStatisticsThrows()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
            {
                Source = new FeatureSource { CollectionId = "buildings" },
                OutStatistics =
                [
                    new FeatureQueryStatistic
                    {
                        OnField = "height",
                        StatisticType = FeatureStatisticType.Max,
                        OutField = "MAX_HEIGHT"
                    }
                ],
            }));

        Assert.Contains("statistics", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetItemsAsync_SharedAbstraction_UnsupportedExplicitSpatialFilterThrows()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
            {
                Source = new FeatureSource { CollectionId = "buildings" },
                SpatialFilter = new FeatureSpatialFilter
                {
                    Geometry = JsonSerializer.SerializeToElement(new { x = -118.0, y = 34.0 }),
                    GeometryType = FeatureSpatialGeometryType.Point,
                    Crs = "EPSG:4326",
                    Relationship = FeatureSpatialRelationship.Intersects
                },
            }));

        Assert.Contains("spatial filters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HonuaSourceFacade_QueriesOgcFeaturesClientAndExposesEditCapabilities()
    {
        string? capturedUrl = null;
        var json = """
        {
            "type": "FeatureCollection",
            "numberMatched": 1,
            "numberReturned": 1,
            "features": [
                {
                    "type": "Feature",
                    "id": "building-7",
                    "properties": { "name": "Building A" }
                }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "buildings",
                Protocol = FeatureProtocolIds.OgcFeatures,
                Locator = new SourceLocator { CollectionId = "buildings" }
            },
            client,
            client,
            client);

        var result = await source.QueryAsync(new SourceQuery { Where = "height > 10", Limit = 5 });
        var ids = await source.QueryObjectIdsAsync();

        Assert.NotNull(capturedUrl);
        Assert.Contains("/ogc/features/collections/buildings/items", capturedUrl);
        Assert.Equal("ogc-features", result.ProviderName);
        Assert.Equal(["building-7"], ids);
        Assert.Contains(FeatureCapabilities.ApplyEdits, source.Capabilities);
        Assert.Same(client, source.Protocol<HonuaOgcFeaturesClient>());
    }

    [Fact]
    public async Task CreateItemAsync_PostsGeoJsonFeature()
    {
        string? capturedBody = null;
        var client = TestHelpers.CreateOgcFeaturesClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Contains("/ogc/features/collections/buildings/items", req.RequestUri?.ToString());
            Assert.Equal("application/geo+json", req.Content?.Headers.ContentType?.MediaType);
            capturedBody = await req.Content!.ReadAsStringAsync();

            return TestHelpers.CreateRawJsonResponse("""
            {
                "type": "Feature",
                "id": "created-1",
                "properties": { "name": "Created" }
            }
            """, HttpStatusCode.Created);
        });

        var result = await client.CreateItemAsync(
            "buildings",
            new OgcFeature
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Created")
                }
            });

        Assert.Contains("\"properties\"", capturedBody);
        Assert.Equal("created-1", result.Id?.GetString());
    }

    [Fact]
    public async Task UpdateItemAsync_PutsGeoJsonFeature()
    {
        string? capturedBody = null;
        var client = TestHelpers.CreateOgcFeaturesClient(async req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.Contains("/ogc/features/collections/buildings/items/building-7", req.RequestUri?.ToString());
            capturedBody = await req.Content!.ReadAsStringAsync();

            return TestHelpers.CreateRawJsonResponse("""
            {
                "type": "Feature",
                "id": "building-7",
                "properties": { "name": "Updated" }
            }
            """);
        });

        var result = await client.UpdateItemAsync(
            "buildings",
            "building-7",
            new OgcFeature
            {
                Properties = new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Updated")
                }
            });

        Assert.Contains("\"Updated\"", capturedBody);
        Assert.Equal("building-7", result.Id?.GetString());
    }

    [Fact]
    public async Task PatchItemAsync_SendsJsonMergePatch()
    {
        string? capturedBody = null;
        string? capturedMediaType = null;
        var client = TestHelpers.CreateOgcFeaturesClient(async req =>
        {
            Assert.Equal(HttpMethod.Patch, req.Method);
            Assert.Contains("/ogc/features/collections/buildings/items/building-7", req.RequestUri?.ToString());
            capturedMediaType = req.Content?.Headers.ContentType?.MediaType;
            capturedBody = await req.Content!.ReadAsStringAsync();

            return TestHelpers.CreateRawJsonResponse("""
            {
                "type": "Feature",
                "id": "building-7",
                "properties": { "name": "Patched" }
            }
            """);
        });

        var patch = JsonSerializer.SerializeToElement(new
        {
            properties = new
            {
                name = "Patched"
            }
        });

        var patchClient = (IHonuaOgcFeaturesPatchClient)client;
        var result = await patchClient.PatchItemAsync("buildings", "building-7", patch);

        Assert.Equal("application/merge-patch+json", capturedMediaType);
        Assert.Contains("\"Patched\"", capturedBody);
        Assert.Equal("building-7", result.Id?.GetString());
    }

    [Fact]
    public async Task DeleteItemAsync_SendsDelete()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.Contains("/ogc/features/collections/buildings/items/building-7", req.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });

        await client.DeleteItemAsync("buildings", "building-7");
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_AppliesSequentialEdits()
    {
        var call = 0;
        string? capturedPatchBody = null;
        string? capturedPatchMediaType = null;
        var client = TestHelpers.CreateOgcFeaturesClient(async req =>
        {
            call++;
            return call switch
            {
                1 => TestHelpers.CreateRawJsonResponse(
                    """{ "type": "Feature", "id": "created-1", "properties": {} }""",
                    HttpStatusCode.Created),
                2 => TestHelpers.CreateRawJsonResponse(
                    """{ "type": "Feature", "id": "updated-1", "properties": {} }"""),
                3 => await CapturePatchAsync(req),
                4 => new HttpResponseMessage(HttpStatusCode.NoContent),
                _ => throw new InvalidOperationException("Unexpected edit request.")
            };
        });

        var response = await ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
        {
            Source = new FeatureSource { CollectionId = "buildings" },
            Adds =
            [
                new FeatureEditFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonSerializer.SerializeToElement("Created")
                    }
                }
            ],
            Updates =
            [
                new FeatureEditFeature
                {
                    Id = "updated-1",
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonSerializer.SerializeToElement("Updated")
                    }
                }
            ],
            Patches =
            [
                new FeatureEditPatch
                {
                    Id = "patched-1",
                    Patch = JsonSerializer.SerializeToElement(new
                    {
                        properties = new
                        {
                            status = "open"
                        }
                    })
                }
            ],
            DeleteIds = ["deleted-1"],
            RollbackOnFailure = false
        });

        Assert.Equal("ogc-features", response.ProviderName);
        Assert.True(response.Succeeded);
        Assert.Equal("created-1", Assert.Single(response.AddResults).Id);
        Assert.Equal("updated-1", Assert.Single(response.UpdateResults).Id);
        Assert.Equal("patched-1", Assert.Single(response.PatchResults).Id);
        Assert.Equal("application/merge-patch+json", capturedPatchMediaType);
        Assert.Contains("\"status\"", capturedPatchBody);
        Assert.Equal("deleted-1", Assert.Single(response.DeleteResults).Id);

        async Task<HttpResponseMessage> CapturePatchAsync(HttpRequestMessage req)
        {
            Assert.Equal(HttpMethod.Patch, req.Method);
            capturedPatchMediaType = req.Content?.Headers.ContentType?.MediaType;
            capturedPatchBody = await req.Content!.ReadAsStringAsync();
            return TestHelpers.CreateRawJsonResponse(
                """{ "type": "Feature", "id": "patched-1", "properties": {} }""");
        }
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_MapsHttpErrorsToEditResults()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""
            {
                "type": "https://example.test/problems/edit-rejected",
                "title": "Edit rejected",
                "detail": "Geometry is outside the collection extent."
            }
            """, HttpStatusCode.BadRequest)));

        var response = await ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
        {
            Source = new FeatureSource { CollectionId = "buildings" },
            Adds =
            [
                new FeatureEditFeature
                {
                    Id = "candidate-1",
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonSerializer.SerializeToElement("Rejected")
                    }
                }
            ]
        });

        var result = Assert.Single(response.AddResults);
        Assert.False(response.Succeeded);
        Assert.False(result.Succeeded);
        Assert.Equal("candidate-1", result.Id);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.Error?.Code);
        Assert.Contains("outside", result.Error?.Message);
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_MapsMalformedPatchResponseToEditResults()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            Assert.Equal(HttpMethod.Patch, req.Method);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(
                "{ this is not valid JSON }"));
        });

        var response = await ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
        {
            Source = new FeatureSource { CollectionId = "buildings" },
            Patches =
            [
                new FeatureEditPatch
                {
                    Id = "patched-1",
                    Patch = JsonSerializer.SerializeToElement(new { properties = new { name = "Patched" } })
                }
            ]
        });

        var result = Assert.Single(response.PatchResults);
        Assert.False(response.Succeeded);
        Assert.False(result.Succeeded);
        Assert.Equal("patched-1", result.Id);
        Assert.Equal((int)HttpStatusCode.OK, result.Error?.Code);
        Assert.Contains("deserialize patched", result.Error?.Message);
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_ValidatesUpdateIdsBeforeWrites()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
            {
                Source = new FeatureSource { CollectionId = "buildings" },
                Adds =
                [
                    new FeatureEditFeature
                    {
                        Attributes = new Dictionary<string, JsonElement>
                        {
                            ["name"] = JsonSerializer.SerializeToElement("Created")
                        }
                    }
                ],
                Updates =
                [
                    new FeatureEditFeature
                    {
                        Attributes = new Dictionary<string, JsonElement>
                        {
                            ["name"] = JsonSerializer.SerializeToElement("Missing ID")
                        }
                    }
                ],
                RollbackOnFailure = false
            }));

        Assert.Contains("updates require", ex.Message);
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_RollbackBatch_Throws()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ => throw new InvalidOperationException("HTTP should not be called."));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
            {
                Source = new FeatureSource { CollectionId = "buildings" },
                Adds = [new FeatureEditFeature()],
                DeleteIds = ["building-7"],
                RollbackOnFailure = true
            }));

        Assert.Contains("rollback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── GetItemAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetItemAsync_ReturnsSingleFeature()
    {
        var json = """
        {
            "type": "Feature",
            "id": "42",
            "geometry": { "type": "Point", "coordinates": [-118.0, 34.0] },
            "properties": { "name": "Building A", "height": 50 }
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetItemAsync("buildings", "42");

        Assert.Equal("Feature", result.Type);
        Assert.NotNull(result.Properties);
        Assert.Equal("Building A", result.Properties["name"].GetString());
    }

    // ── GetItemsPagesAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetItemsPagesAsync_FollowsNextLinks()
    {
        var callCount = 0;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            callCount++;
            var json = callCount switch
            {
                1 => """
                {
                    "type": "FeatureCollection",
                    "features": [{ "type": "Feature", "id": "1", "properties": {} }],
                    "links": [{ "href": "http://localhost:5000/ogc/features/collections/test/items?offset=1", "rel": "next" }]
                }
                """,
                2 => """
                {
                    "type": "FeatureCollection",
                    "features": [{ "type": "Feature", "id": "2", "properties": {} }],
                    "links": []
                }
                """,
                _ => """{ "type": "FeatureCollection", "features": [] }"""
            };
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<OgcFeatureCollection>();
        await foreach (var page in client.GetItemsPagesAsync("test"))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetItemsPagesAsync_EmptyIntermediatePageWithNextLink_ContinuesPaging()
    {
        // A server may return an empty intermediate page that still advertises a rel=next
        // link. Pagination must follow the link rather than stopping on the empty page,
        // otherwise the trailing features are silently dropped.
        var callCount = 0;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            callCount++;
            var json = callCount switch
            {
                1 => """
                {
                    "type": "FeatureCollection",
                    "features": [{ "type": "Feature", "id": "1", "properties": {} }],
                    "links": [{ "href": "http://localhost:5000/ogc/features/collections/test/items?offset=1", "rel": "next" }]
                }
                """,
                2 => """
                {
                    "type": "FeatureCollection",
                    "features": [],
                    "links": [{ "href": "http://localhost:5000/ogc/features/collections/test/items?offset=2", "rel": "next" }]
                }
                """,
                3 => """
                {
                    "type": "FeatureCollection",
                    "features": [{ "type": "Feature", "id": "3", "properties": {} }],
                    "links": []
                }
                """,
                _ => """{ "type": "FeatureCollection", "features": [] }"""
            };
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<OgcFeatureCollection>();
        await foreach (var page in client.GetItemsPagesAsync("test"))
        {
            pages.Add(page);
        }

        // The empty page is not yielded but its next link is followed to reach page 3.
        Assert.Equal(3, callCount);
        Assert.Equal(2, pages.Count);
        Assert.Equal("1", pages[0].Features![0].Id!.Value.GetString());
        Assert.Equal("3", pages[1].Features![0].Id!.Value.GetString());
    }

    [Fact]
    public async Task GetItemsPagesAsync_StopsWhenNoNextLink()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "features": [{ "type": "Feature", "id": "1", "properties": {} }],
            "links": [{ "href": "/some/other", "rel": "self" }]
        }
        """;
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var pages = new List<OgcFeatureCollection>();
        await foreach (var page in client.GetItemsPagesAsync("test"))
        {
            pages.Add(page);
        }

        Assert.Single(pages);
    }

    [Fact]
    public async Task GetItemsPagesAsync_RejectsNextLinkToDifferentOrigin()
    {
        var callCount = 0;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            callCount++;
            var json = callCount == 1
                ? """
                {
                    "type": "FeatureCollection",
                    "features": [{ "type": "Feature", "id": "1", "properties": {} }],
                    "links": [{ "href": "https://evil.example.com/ogc/features/collections/test/items?offset=1", "rel": "next" }]
                }
                """
                : """{ "type": "FeatureCollection", "features": [] }""";
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<OgcFeatureCollection>();
        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(async () =>
        {
            await foreach (var page in client.GetItemsPagesAsync("test"))
            {
                pages.Add(page);
            }
        });

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Contains("different origin", ex.Message);
        Assert.Single(pages); // First page was yielded before error
    }

    // ── RFC 7807 Problem Details ────────────────────────────────────

    [Fact]
    public async Task GetItemsAsync_ProblemDetails_ThrowsWithDetails()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateProblemDetailsResponse(
                HttpStatusCode.BadRequest,
                "Invalid filter",
                "CQL2 syntax error at position 5",
                "https://example.com/problems/invalid-filter")));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetItemsAsync("buildings", new OgcItemsParams { Filter = "BAD CQL" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("CQL2 syntax error at position 5", ex.Message);
        Assert.Equal("Invalid filter", ex.ProblemTitle);
        Assert.Equal("CQL2 syntax error at position 5", ex.ProblemDetail);
        Assert.Equal("https://example.com/problems/invalid-filter", ex.ProblemType);
    }

    // ── HTTP error ──────────────────────────────────────────────────

    [Fact]
    public async Task GetCollectionAsync_NotFound_Throws()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not Found")
            }));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetCollectionAsync("noSuchCollection"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetConformanceAsync_ServerError_Throws()
    {
        var client = TestHelpers.CreateOgcFeaturesClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(
                HttpStatusCode.InternalServerError, "Internal server error")));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetConformanceAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    // ── GetItemsRawAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetItemsRawAsync_ReturnsRawResponse()
    {
        string? capturedUrl = null;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<gml:FeatureCollection/>", System.Text.Encoding.UTF8, "application/gml+xml")
            });
        });

        using var response = await client.GetItemsRawAsync(
            "buildings",
            new OgcItemsParams { Format = OgcFeaturesFormat.Gml });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedUrl);
        Assert.Contains("f=gml", capturedUrl);
    }

    [Fact]
    public async Task GetItemsRawAsync_SerializesCsvFormat()
    {
        string? capturedUrl = null;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("id,name", System.Text.Encoding.UTF8, "text/csv")
            });
        });

        using var response = await client.GetItemsRawAsync(
            "buildings",
            new OgcItemsParams { Format = OgcFeaturesFormat.Csv });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedUrl);
        Assert.Contains("f=csv", capturedUrl);
    }
}
