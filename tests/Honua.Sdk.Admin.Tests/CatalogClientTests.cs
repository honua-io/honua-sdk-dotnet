// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class CatalogClientTests
{
    private static HonuaCatalogClient CreateCatalogClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        return new HonuaCatalogClient(httpClient);
    }

    [Fact]
    public async Task ServiceListAndLookup_UseCanonicalServiceEndpointWithoutLegacyMetadataRoutes()
    {
        var requests = new List<string>();
        var client = CreateCatalogClient(req =>
        {
            var pathAndQuery = req.RequestUri!.PathAndQuery;
            requests.Add(pathAndQuery);
            return Task.FromResult(pathAndQuery switch
            {
                "/api/v1/admin/services/" => TestHelpers.CreateJsonResponse(new[]
                {
                    new
                    {
                        serviceName = "parks",
                        description = "Parks service",
                        layerCount = 1,
                        enabledProtocols = Array.Empty<string>()
                    }
                }),
                _ => TestHelpers.CreateErrorResponse(HttpStatusCode.NotFound, "not found")
            });
        });

        var services = await client.ListServicesAsync();
        var service = await client.GetServiceAsync("parks");

        Assert.Equal("parks", Assert.Single(services).Name);
        Assert.NotNull(service);
        Assert.Equal("parks", service.Name);
        Assert.Equal(2, requests.Count(path => path == "/api/v1/admin/services/"));
        Assert.DoesNotContain(requests, path => path.StartsWith("/api/v1/admin/metadata/resources", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListServicesAsync_WithMetadataFilter_PropagatesMetadataFailure()
    {
        var client = CreateCatalogClient(_ => Task.FromResult(
            TestHelpers.CreateErrorResponse(HttpStatusCode.NotFound, "metadata resources are unavailable")));

        var exception = await Assert.ThrowsAsync<HonuaAdminApiException>(() => client.ListServicesAsync(new CatalogQueryOptions
        {
            Owner = "gis"
        }));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task SearchAsync_ReturnsServicesLayersGroupsAndSavedSourceDescriptors()
    {
        var requests = new Dictionary<string, int>(StringComparer.Ordinal);
        var client = CreateCatalogClient(req =>
        {
            var pathAndQuery = req.RequestUri!.PathAndQuery;
            requests[pathAndQuery] = requests.GetValueOrDefault(pathAndQuery) + 1;
            return pathAndQuery switch
            {
                "/api/v1/admin/metadata/resources?kind=Service" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    MetadataResource("Service", "default", "parks", labels: new Dictionary<string, string>
                    {
                        ["tags"] = "public,reference",
                        ["owner"] = "gis"
                    })
                })),
                "/api/v1/admin/metadata/resources?kind=Layer" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    MetadataResource(
                        "Layer",
                        "default",
                        "parks-layer",
                        new { serviceName = "parks", layerId = 0, description = "Park asset layer" },
                        new Dictionary<string, string> { ["tags"] = "public", ["owner"] = "gis" })
                })),
                "/api/v1/admin/metadata/resources?kind=Group" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    MetadataResource(
                        "Group",
                        "default",
                        "field-ops",
                        new { description = "Field operations catalog group" },
                        new Dictionary<string, string> { ["tags"] = "operations", ["owner"] = "gis" })
                })),
                "/api/v1/admin/metadata/resources?kind=SourceDescriptor" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    MetadataResource(
                        "SourceDescriptor",
                        "default",
                        "parks-source",
                        new
                        {
                            sourceDescriptor = new
                            {
                                id = "parks-source",
                                protocol = "geoservices-feature-service",
                                locator = new { serviceId = "parks", layerId = 0 },
                                capabilities = new[] { "Query" }
                            }
                        },
                        new Dictionary<string, string> { ["tags"] = "saved", ["owner"] = "gis" })
                })),
                "/api/v1/admin/services/" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    new
                    {
                        serviceName = "parks",
                        description = "Parks service",
                        layerCount = 1,
                        enabledProtocols = new[] { "FeatureServer", "MapServer" }
                    }
                })),
                "/rest/services/parks/FeatureServer?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    serviceDescription = "Parks FeatureServer",
                    capabilities = "Query,Extract",
                    fullExtent = new
                    {
                        xmin = -158.3,
                        ymin = 21.2,
                        xmax = -157.6,
                        ymax = 21.8,
                        spatialReference = new { wkid = 4326, latestWkid = 4326 }
                    },
                    layers = new[] { new { id = 0, name = "Parks" } }
                })),
                "/rest/services/parks/FeatureServer/0?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    id = 0,
                    name = "Parks",
                    description = "Park points",
                    geometryType = "esriGeometryPoint",
                    capabilities = "Query,Create,Update",
                    hasAttachments = true,
                    supportsStatistics = true,
                    objectIdField = "OBJECTID",
                    extent = new
                    {
                        xmin = -158.1,
                        ymin = 21.3,
                        xmax = -157.7,
                        ymax = 21.6,
                        spatialReference = new { wkid = 4326, latestWkid = 4326 }
                    }
                })),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"message":"not found"}""")
                })
            };
        });

        var result = await client.SearchAsync();

        Assert.Equal(4, result.TotalCount);
        Assert.Null(result.NextOffset);

        var service = Assert.Single(result.Items, item => item.Kind == CatalogItemKind.Service);
        Assert.Equal("parks", service.Service!.Name);
        Assert.Contains("MapServer", service.ServiceTypes);
        Assert.Equal("gis", service.Owner);

        var layer = Assert.Single(result.Items, item => item.Kind == CatalogItemKind.Layer);
        Assert.Equal("Parks", layer.Layer!.Name);
        Assert.Equal("Point", layer.GeometryType);
        Assert.Contains("Attachments", layer.Capabilities);
        Assert.Equal("parks", layer.Layer.SourceDescriptor.Locator.ServiceId);
        Assert.Equal(0, layer.Layer.SourceDescriptor.Locator.LayerId);

        var group = Assert.Single(result.Items, item => item.Kind == CatalogItemKind.Group);
        Assert.Equal("field-ops", group.Group!.Name);

        var descriptor = Assert.Single(result.Items, item => item.Kind == CatalogItemKind.SourceDescriptor);
        Assert.Equal("parks-source", descriptor.SourceDescriptor!.Descriptor.Id);
        Assert.Equal(1, requests["/rest/services/parks/FeatureServer?f=json"]);
    }

    [Fact]
    public async Task SearchAsync_FiltersAndPaginatesCatalogItems()
    {
        var client = CreateCatalogClient(req =>
        {
            var pathAndQuery = req.RequestUri!.PathAndQuery;
            return pathAndQuery switch
            {
                "/api/v1/admin/metadata/resources?kind=Service" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/metadata/resources?kind=Layer" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    MetadataResource(
                        "Layer",
                        "default",
                        "parks-layer",
                        new { serviceName = "parks", layerId = 0 },
                        new Dictionary<string, string> { ["tags"] = "public" })
                })),
                "/api/v1/admin/metadata/resources?kind=Group" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/metadata/resources?kind=SourceDescriptor" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/services/" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    new
                    {
                        serviceName = "parks",
                        description = "Parks service",
                        layerCount = 1,
                        enabledProtocols = new[] { "FeatureServer" }
                    }
                })),
                "/rest/services/parks/FeatureServer?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    capabilities = "Query",
                    layers = new[] { new { id = 0, name = "Parks" } }
                })),
                "/rest/services/parks/FeatureServer/0?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    id = 0,
                    name = "Parks",
                    geometryType = "esriGeometryPoint",
                    capabilities = "Query",
                    hasAttachments = true
                })),
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("""{"message":"not found"}""")
                })
            };
        });

        var result = await client.SearchAsync(new CatalogQueryOptions
        {
            Kinds = [CatalogItemKind.Layer],
            Query = "park",
            ServiceTypes = ["FeatureServer"],
            Tags = ["public"],
            GeometryTypes = ["Point"],
            Capabilities = ["Attachments"],
            Limit = 1
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(CatalogItemKind.Layer, item.Kind);
        Assert.Equal(1, result.TotalCount);
        Assert.Null(result.NextOffset);
    }

    [Fact]
    public async Task SearchAsync_LayerOnlyLimitedQuerySkipsMissingLayerDetailsBeforePaging()
    {
        var requests = new Dictionary<string, int>(StringComparer.Ordinal);
        var client = CreateCatalogClient(req =>
        {
            var pathAndQuery = req.RequestUri!.PathAndQuery;
            requests[pathAndQuery] = requests.GetValueOrDefault(pathAndQuery) + 1;
            return pathAndQuery switch
            {
                "/api/v1/admin/metadata/resources?kind=Service" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/metadata/resources?kind=Layer" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/metadata/resources?kind=Group" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/metadata/resources?kind=SourceDescriptor" => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())),
                "/api/v1/admin/services/" => Task.FromResult(TestHelpers.CreateJsonResponse(new[]
                {
                    new
                    {
                        serviceName = "alpha",
                        layerCount = 2,
                        enabledProtocols = new[] { "FeatureServer" }
                    },
                    new
                    {
                        serviceName = "beta",
                        layerCount = 2,
                        enabledProtocols = new[] { "FeatureServer" }
                    }
                })),
                "/rest/services/alpha/FeatureServer?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    capabilities = "Query",
                    layers = new[] { new { id = 0, name = "Alpha zero" }, new { id = 1, name = "Alpha one" } }
                })),
                "/rest/services/beta/FeatureServer?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    capabilities = "Query",
                    layers = new[] { new { id = 0, name = "Beta zero" }, new { id = 1, name = "Beta one" } }
                })),
                "/rest/services/alpha/FeatureServer/0?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    id = 0,
                    name = "Alpha zero",
                    geometryType = "esriGeometryPoint",
                    capabilities = "Query"
                })),
                "/rest/services/beta/FeatureServer/0?f=json" => Task.FromResult(TestHelpers.CreateRawJsonResponse(new
                {
                    id = 0,
                    name = "Beta zero",
                    geometryType = "esriGeometryPoint",
                    capabilities = "Query"
                })),
                _ => Task.FromResult(TestHelpers.CreateRawJsonResponse(new { message = "not found" }, HttpStatusCode.NotFound))
            };
        });

        var result = await client.SearchAsync(new CatalogQueryOptions
        {
            Kinds = [CatalogItemKind.Layer],
            SortBy = CatalogSortBy.ServiceName,
            Limit = 2
        });

        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal("alpha", item.ServiceName);
                Assert.Equal(0, item.LayerId);
            },
            item =>
            {
                Assert.Equal("beta", item.ServiceName);
                Assert.Equal(0, item.LayerId);
            });
        Assert.Equal(2, result.TotalCount);
        Assert.Null(result.NextOffset);
        Assert.Equal(1, requests["/rest/services/alpha/FeatureServer?f=json"]);
        Assert.Equal(1, requests["/rest/services/beta/FeatureServer?f=json"]);
        Assert.Equal(1, requests["/rest/services/alpha/FeatureServer/0?f=json"]);
        Assert.Equal(1, requests["/rest/services/alpha/FeatureServer/1?f=json"]);
        Assert.Equal(1, requests["/rest/services/beta/FeatureServer/0?f=json"]);
        Assert.Equal(1, requests["/rest/services/beta/FeatureServer/1?f=json"]);
    }

    [Fact]
    public async Task GetSourceDescriptorAsync_ReturnsNullForMissingResource()
    {
        var client = CreateCatalogClient(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("""{"message":"Resource not found."}""")
        }));

        var result = await client.GetSourceDescriptorAsync("default", "missing");

        Assert.Null(result);
    }

    private static object MetadataResource(
        string kind,
        string ns,
        string name,
        object? spec = null,
        Dictionary<string, string>? labels = null)
        => new
        {
            apiVersion = "honua.io/v1alpha1",
            kind,
            metadata = new
            {
                id = $"{ns}/{kind}/{name}",
                name,
                @namespace = ns,
                labels,
                createdAt = DateTimeOffset.Parse("2026-04-30T00:00:00Z", CultureInfo.InvariantCulture)
            },
            spec = spec ?? new { description = $"{name} description" }
        };
}
