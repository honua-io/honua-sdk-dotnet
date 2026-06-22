// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

public class HonuaFeatureServerClientTests
{
    // ── GetServiceInfoAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetServiceInfoAsync_ReturnsServiceInfo()
    {
        var json = """
        {
            "serviceDescription": "Test Service",
            "maxRecordCount": 1000,
            "capabilities": "Query",
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
            "layers": [{ "id": 0, "name": "TestLayer" }]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetServiceInfoAsync("myService");

        Assert.Equal("Test Service", result.ServiceDescription);
        Assert.Equal(1000, result.MaxRecordCount);
        Assert.Equal("Query", result.Capabilities);
        Assert.NotNull(result.Layers);
        Assert.Single(result.Layers);
        Assert.Equal("TestLayer", result.Layers[0].Name);
    }

    // ── GetLayerInfoAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetLayerInfoAsync_ReturnsLayerInfo()
    {
        var json = """
        {
            "id": 0,
            "name": "Points",
            "geometryType": "esriGeometryPoint",
            "objectIdField": "OBJECTID",
            "maxRecordCount": 2000,
            "supportsStatistics": true,
            "fields": [
                { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                { "name": "NAME", "type": "esriFieldTypeString", "nullable": true, "length": 255 }
            ]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetLayerInfoAsync("myService", 0);

        Assert.Equal("Points", result.Name);
        Assert.Equal("esriGeometryPoint", result.GeometryType);
        Assert.Equal("OBJECTID", result.ObjectIdField);
        Assert.True(result.SupportsStatistics);
        Assert.NotNull(result.Fields);
        Assert.Equal(2, result.Fields.Count);
    }

    [Fact]
    public async Task GetDescriptorAsync_SharedAbstraction_MapsLayerSchemaAndCapabilities()
    {
        var json = """
        {
            "id": 0,
            "name": "Parks",
            "geometryType": "esriGeometryPoint",
            "objectIdField": "OBJECTID",
            "globalIdField": "GLOBALID",
            "capabilities": "Query,Create,Update,Delete,Uploads,Sync",
            "supportsStatistics": true,
            "supportsAdvancedQueries": true,
            "hasAttachments": true,
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
            "extent": {
                "xmin": -180,
                "ymin": -90,
                "xmax": 180,
                "ymax": 90,
                "spatialReference": { "wkid": 4326 }
            },
            "timeInfo": {
                "startTimeField": "start_at",
                "endTimeField": "end_at",
                "trackIdField": "track_id",
                "timeReference": { "timeZone": "UTC" }
            },
            "fields": [
                { "name": "OBJECTID", "type": "esriFieldTypeOID", "alias": "Object ID", "nullable": false, "editable": false },
                {
                    "name": "STATUS",
                    "type": "esriFieldTypeString",
                    "alias": "Status",
                    "nullable": false,
                    "length": 20,
                    "editable": true,
                    "defaultValue": "open",
                    "domain": { "type": "codedValue", "codedValues": [{ "name": "Open", "code": "open" }] }
                }
            ]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("/rest/services/parks/FeatureServer/0", req.RequestUri?.ToString());
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var descriptor = await ((IHonuaFeatureDescriptorClient)client).GetDescriptorAsync(new SourceDescriptor
        {
            Id = "parks",
            Protocol = FeatureProtocolIds.GeoServicesFeatureService,
            Locator = new SourceLocator { ServiceId = "parks", LayerId = 0 }
        });

        Assert.NotNull(descriptor.Schema);
        Assert.Equal("OBJECTID", descriptor.Schema.ObjectIdField);
        Assert.Equal("GLOBALID", descriptor.Schema.GlobalIdField);
        Assert.Equal(FeatureSpatialGeometryType.Point, descriptor.Schema.GeometryType);
        Assert.Equal("EPSG:4326", descriptor.Schema.SpatialReference);
        Assert.Equal("EPSG:4326", descriptor.Schema.Extent?.Crs);
        Assert.Equal("start_at", descriptor.Schema.TimeInfo?.StartTimeField);
        Assert.Equal("end_at", descriptor.Schema.TimeInfo?.EndTimeField);
        Assert.True(descriptor.Schema.EditCapabilities?.SupportsAdds);
        Assert.True(descriptor.Schema.EditCapabilities?.SupportsUpdates);
        Assert.True(descriptor.Schema.EditCapabilities?.SupportsDeletes);
        Assert.True(descriptor.Schema.AttachmentCapabilities?.SupportsList);
        Assert.True(descriptor.Schema.AttachmentCapabilities?.SupportsDownload);
        Assert.True(descriptor.Schema.AttachmentCapabilities?.SupportsAdd);
        Assert.True(descriptor.Schema.AttachmentCapabilities?.SupportsUpdate);
        Assert.True(descriptor.Schema.AttachmentCapabilities?.SupportsDelete);
        Assert.Contains(FeatureCapabilities.QueryAggregate, descriptor.Capabilities);
        Assert.Contains(FeatureCapabilities.Attachments, descriptor.Capabilities);
        Assert.DoesNotContain(FeatureCapabilities.Offline, descriptor.Capabilities);
        Assert.DoesNotContain(FeatureCapabilities.TimeFilter, descriptor.Capabilities);
        Assert.DoesNotContain(FeatureCapabilities.SpatialRelationships, descriptor.Capabilities);
        var status = Assert.Single(descriptor.Schema.Fields, field => field.Name == "STATUS");
        Assert.Equal("Status", status.Alias);
        Assert.Equal("esriFieldTypeString", status.Type);
        Assert.False(status.Nullable);
        Assert.Equal(20, status.Length);
        Assert.True(status.Editable);
        Assert.True(status.Required);
        Assert.Equal("open", status.DefaultValue?.GetString());
        Assert.Equal("codedValue", status.Domain?.GetProperty("type").GetString());
    }

    // ── QueryAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_ReturnsFeatures()
    {
        var json = """
        {
            "objectIdFieldName": "OBJECTID",
            "features": [
                { "attributes": { "OBJECTID": 1, "NAME": "Test" }, "geometry": { "x": -118.0, "y": 34.0 } },
                { "attributes": { "OBJECTID": 2, "NAME": "Test2" }, "geometry": { "x": -117.0, "y": 33.0 } }
            ],
            "exceededTransferLimit": false
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.QueryAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.NotNull(result.Features);
        Assert.Equal(2, result.Features.Count);
        Assert.False(result.ExceededTransferLimit);
    }

    [Fact]
    public async Task QueryAsync_SerializesQueryParams()
    {
        string? capturedUrl = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new FeatureServerQueryParams
        {
            Where = "POP > 100",
            OutFields = "NAME,POP",
            ReturnGeometry = false,
            ResultOffset = 10,
            ResultRecordCount = 5,
            OrderByFields = "POP DESC",
            OutSR = 3857,
            ReturnDistinctValues = true,
            ReturnCountOnly = true,
            ReturnIdsOnly = true,
            ReturnExtentOnly = true,
        };

        await client.QueryAsync("svc", 0, query);

        Assert.NotNull(capturedUrl);
        Assert.Contains("where=POP", capturedUrl);
        Assert.Contains("outFields=NAME%2CPOP", capturedUrl);
        Assert.Contains("returnGeometry=false", capturedUrl);
        Assert.Contains("resultOffset=10", capturedUrl);
        Assert.Contains("resultRecordCount=5", capturedUrl);
        Assert.Contains("orderByFields=POP", capturedUrl);
        Assert.Contains("outSR=3857", capturedUrl);
        Assert.Contains("returnDistinctValues=true", capturedUrl);
        Assert.Contains("returnCountOnly=true", capturedUrl);
        Assert.Contains("returnIdsOnly=true", capturedUrl);
        Assert.Contains("returnExtentOnly=true", capturedUrl);
    }

    [Fact]
    public async Task QueryAsync_SpatialFilter_IncludesParams()
    {
        string? capturedUrl = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new FeatureServerQueryParams
        {
            SpatialFilter = new FeatureServerSpatialFilter
            {
                Geometry = """{"xmin":-118,"ymin":33,"xmax":-117,"ymax":34}""",
                GeometryType = "esriGeometryEnvelope",
                SpatialRel = SpatialRelationship.Contains,
            }
        };

        await client.QueryAsync("svc", 0, query);

        Assert.NotNull(capturedUrl);
        Assert.Contains("geometry=", capturedUrl);
        Assert.Contains("geometryType=esriGeometryEnvelope", capturedUrl);
        Assert.Contains("spatialRel=esriSpatialRelContains", capturedUrl);
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_SerializesProviderNeutralParams()
    {
        string? capturedUrl = null;
        var json = """
        {
            "objectIdFieldName": "OBJECTID",
            "count": 12,
            "objectIds": [7, 8],
            "extent": {
                "xmin": -180,
                "ymin": -90,
                "xmax": 180,
                "ymax": 90,
                "spatialReference": { "wkid": 4326 }
            },
            "features": [
                { "attributes": { "OBJECTID": 7, "NAME": "Point A" }, "geometry": { "x": -118.0, "y": 34.0 } }
            ],
            "exceededTransferLimit": false
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });
        var start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2024, 1, 31, 0, 0, 0, TimeSpan.Zero);

        var result = await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            Filter = "POP > 100",
            FilterLanguage = FeatureFilterLanguage.SqlWhere,
            OutFields = ["NAME", "POP"],
            ReturnGeometry = false,
            Offset = 5,
            Limit = 10,
            OrderBy = "POP DESC",
            ReturnDistinct = true,
            ReturnCountOnly = true,
            ReturnIdsOnly = true,
            ReturnExtentOnly = true,
            TimeFilter = new FeatureTimeFilter
            {
                Start = start,
                End = end,
                Relation = FeatureTimeRelation.Within
            },
            OutStatistics =
            [
                new FeatureQueryStatistic
                {
                    OnField = "POP",
                    StatisticType = FeatureStatisticType.Sum,
                    OutField = "SUM_POP"
                }
            ],
            GroupBy = ["STATE"],
            Having = "SUM_POP > 10",
            Bbox = new FeatureBoundingBox { MinX = -118, MinY = 33, MaxX = -117, MaxY = 34, Crs = "EPSG:4326" },
            OutputCrs = "EPSG:3857",
        });

        Assert.NotNull(capturedUrl);
        var decodedUrl = WebUtility.UrlDecode(capturedUrl);
        Assert.Contains("where=POP", capturedUrl);
        Assert.Contains("outFields=NAME%2CPOP", capturedUrl);
        Assert.Contains("returnGeometry=false", capturedUrl);
        Assert.Contains("resultOffset=5", capturedUrl);
        Assert.Contains("resultRecordCount=10", capturedUrl);
        Assert.Contains("orderByFields=POP", capturedUrl);
        Assert.Contains("returnDistinctValues=true", capturedUrl);
        Assert.Contains("returnCountOnly=true", capturedUrl);
        Assert.Contains("returnIdsOnly=true", capturedUrl);
        Assert.Contains("returnExtentOnly=true", capturedUrl);
        Assert.Contains($"time={start.ToUnixTimeMilliseconds()},{end.ToUnixTimeMilliseconds()}", decodedUrl);
        Assert.Contains("timeRelation=esriTimeRelationWithin", capturedUrl);
        Assert.Contains("\"statisticType\":\"sum\"", decodedUrl);
        Assert.Contains("\"onStatisticField\":\"POP\"", decodedUrl);
        Assert.Contains("\"outStatisticFieldName\":\"SUM_POP\"", decodedUrl);
        Assert.Contains("groupByFieldsForStatistics=STATE", capturedUrl);
        Assert.Contains("having=SUM_POP > 10", decodedUrl);
        Assert.Contains("geometryType=esriGeometryEnvelope", capturedUrl);
        Assert.Contains("inSR=4326", capturedUrl);
        Assert.Contains("outSR=3857", capturedUrl);
        Assert.Equal("geoservices-featureserver", result.ProviderName);
        Assert.Equal(12, result.NumberMatched);
        Assert.Equal([7L, 8L], result.ObjectIds);
        Assert.NotNull(result.Extent);
        Assert.Equal("EPSG:4326", result.Extent.Crs);
        Assert.Single(result.Features);
        Assert.Equal("7", result.Features[0].Id);
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_Crs84MapsToWkid4326()
    {
        string? capturedUrl = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            Bbox = new FeatureBoundingBox
            {
                MinX = -118,
                MinY = 33,
                MaxX = -117,
                MaxY = 34,
                Crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
            },
            OutputCrs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        });

        Assert.NotNull(capturedUrl);
        Assert.Contains("inSR=4326", capturedUrl);
        Assert.Contains("outSR=4326", capturedUrl);
        Assert.DoesNotContain("1384", capturedUrl);
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_MapsExplicitSpatialFilter()
    {
        string? capturedUrl = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            SpatialFilter = new FeatureSpatialFilter
            {
                Geometry = JsonSerializer.SerializeToElement(new { x = -118.0, y = 34.0 }),
                GeometryType = FeatureSpatialGeometryType.Point,
                Crs = "EPSG:4326",
                Relationship = FeatureSpatialRelationship.Contains
            }
        });

        Assert.NotNull(capturedUrl);
        var decodedUrl = WebUtility.UrlDecode(capturedUrl);
        Assert.Contains("geometryType=esriGeometryPoint", capturedUrl);
        Assert.Contains("spatialRel=esriSpatialRelContains", capturedUrl);
        Assert.Contains("\"x\":-118", decodedUrl);
        Assert.Contains("\"y\":34", decodedUrl);
        Assert.Contains("inSR=4326", capturedUrl);
    }

    [Fact]
    public async Task HonuaSourceFacade_QueriesFeatureServerClientAndMatchesProviderAlias()
    {
        string? capturedUrl = null;
        var json = """
        {
            "objectIdFieldName": "OBJECTID",
            "features": [
                { "attributes": { "OBJECTID": 7, "NAME": "Point A" } }
            ],
            "exceededTransferLimit": false
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parks",
                Protocol = FeatureProtocolIds.GeoServicesFeatureService,
                Locator = new SourceLocator { ServiceId = "svc", LayerId = 0 }
            },
            client,
            client,
            client);

        var result = await source.QueryAsync(new SourceQuery { Where = "POP > 100", Limit = 10 });
        var ids = await source.QueryObjectIdsAsync();

        Assert.NotNull(capturedUrl);
        Assert.Contains("/rest/services/svc/FeatureServer/0/query", capturedUrl);
        Assert.Equal("geoservices-featureserver", result.ProviderName);
        Assert.Equal(["7"], ids);
        Assert.Contains(FeatureCapabilities.ApplyEdits, source.Capabilities);
        Assert.Same(client, source.Protocol<HonuaFeatureServerClient>("geoservices-featureserver"));
    }

    // ── Feature edits ───────────────────────────────────────────────

    [Fact]
    public async Task ApplyEditsAsync_PostsApplyEditsFormAndReturnsResults()
    {
        HttpMethod? capturedMethod = null;
        string? capturedPath = null;
        Dictionary<string, string?>? capturedForm = null;
        var json = """
        {
            "addResults": [{ "objectId": 101, "success": true }],
            "updateResults": [
                {
                    "objectId": 102,
                    "success": false,
                    "error": { "code": 400, "description": "Update rejected" }
                }
            ],
            "deleteResults": [{ "objectId": 103, "success": true }],
            "editMoment": 123456789
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(async req =>
        {
            capturedMethod = req.Method;
            capturedPath = req.RequestUri?.AbsolutePath;
            capturedForm = await ParseFormAsync(req).ConfigureAwait(false);
            return TestHelpers.CreateRawJsonResponse(json);
        });

        var response = await client.ApplyEditsAsync("svc", 0, new FeatureServerEditRequest
        {
            Adds =
            [
                new FeatureServerFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["NAME"] = JsonValue("New Park"),
                        ["ACTIVE"] = JsonValue(true),
                    },
                    Geometry = JsonObject("""{"x":1.25,"y":2.5}"""),
                }
            ],
            Updates =
            [
                new FeatureServerFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["OBJECTID"] = JsonValue(102),
                        ["NAME"] = JsonValue("Renamed Park"),
                    },
                }
            ],
            Deletes = [103],
            RollbackOnFailure = false,
            ForceWrite = true,
        });

        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("/rest/services/svc/FeatureServer/0/applyEdits", capturedPath);
        Assert.NotNull(capturedForm);
        Assert.Equal("json", capturedForm!["f"]);
        Assert.Equal("false", capturedForm["rollbackOnFailure"]);
        Assert.Equal("true", capturedForm["forceWrite"]);
        Assert.Equal("103", capturedForm["deletes"]);

        using var adds = JsonDocument.Parse(capturedForm["adds"]!);
        Assert.Equal("New Park", adds.RootElement[0].GetProperty("attributes").GetProperty("NAME").GetString());
        Assert.True(adds.RootElement[0].GetProperty("attributes").GetProperty("ACTIVE").GetBoolean());
        Assert.Equal(1.25, adds.RootElement[0].GetProperty("geometry").GetProperty("x").GetDouble());

        using var updates = JsonDocument.Parse(capturedForm["updates"]!);
        Assert.Equal(102, updates.RootElement[0].GetProperty("attributes").GetProperty("OBJECTID").GetInt64());

        Assert.Equal(123456789, response.EditMoment);
        Assert.Single(response.AddResults);
        Assert.True(response.AddResults[0].Success);
        Assert.False(response.UpdateResults[0].Success);
        Assert.Equal(400, response.UpdateResults[0].Error?.Code);
        Assert.Equal("Update rejected", response.UpdateResults[0].Error?.Description);
        Assert.Equal(103, response.DeleteResults[0].ObjectId);
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_InjectsObjectIdFieldAndMapsResults()
    {
        var callCount = 0;
        Dictionary<string, string?>? capturedForm = null;
        var client = TestHelpers.CreateFeatureServerClient(async req =>
        {
            callCount++;
            if (req.Method == HttpMethod.Get)
            {
                return TestHelpers.CreateRawJsonResponse("""{ "id": 0, "objectIdField": "OBJECTID", "capabilities": "Query,Create,Update,Delete" }""");
            }

            capturedForm = await ParseFormAsync(req).ConfigureAwait(false);
            return TestHelpers.CreateRawJsonResponse("""
            {
                "addResults": [{ "objectId": 201, "success": true }],
                "updateResults": [{ "objectId": 202, "success": true }],
                "deleteResults": [{ "objectId": 203, "success": true }]
            }
            """);
        });

        var response = await ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            Adds =
            [
                new FeatureEditFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["NAME"] = JsonValue("New Park"),
                    },
                }
            ],
            Updates =
            [
                new FeatureEditFeature
                {
                    ObjectId = 202,
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["NAME"] = JsonValue("Updated Park"),
                    },
                }
            ],
            DeleteIds = ["203"],
            ForceWrite = true,
        });

        Assert.Equal(2, callCount);
        Assert.NotNull(capturedForm);
        Assert.Equal("203", capturedForm!["deletes"]);
        Assert.Equal("true", capturedForm["forceWrite"]);

        using var updates = JsonDocument.Parse(capturedForm["updates"]!);
        var attributes = updates.RootElement[0].GetProperty("attributes");
        Assert.Equal("Updated Park", attributes.GetProperty("NAME").GetString());
        Assert.Equal(202, attributes.GetProperty("OBJECTID").GetInt64());

        Assert.Equal("geoservices-featureserver", response.ProviderName);
        Assert.True(response.Succeeded);
        Assert.Equal(201, response.AddResults[0].ObjectId);
        Assert.Equal(202, response.UpdateResults[0].ObjectId);
        Assert.Equal(203, response.DeleteResults[0].ObjectId);
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_NonNumericDeleteId_Throws()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""{}""")));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
            {
                Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
                DeleteIds = ["abc"],
            }));

        Assert.Contains("numeric", ex.Message);
    }

    [Fact]
    public async Task ApplyEditsAsync_EmptyRequest_Throws()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""{}""")));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ApplyEditsAsync("svc", 0, new FeatureServerEditRequest()));

        Assert.Contains("At least one", ex.Message);
    }

    [Fact]
    public async Task AttachmentOperationsAsync_SharedAbstraction_UsesFeatureServerAttachmentEndpoints()
    {
        var client = TestHelpers.CreateFeatureServerClient(async req =>
        {
            var path = req.RequestUri?.AbsolutePath;
            if (req.Method == HttpMethod.Get &&
                path == "/rest/services/parks/FeatureServer/0/42/attachments")
            {
                Assert.Equal("?f=json", req.RequestUri?.Query);
                return TestHelpers.CreateRawJsonResponse("""
                {
                    "attachmentInfos": [
                        {
                            "id": 7,
                            "parentObjectId": 42,
                            "globalId": "attachment-global-id",
                            "name": "photo.txt",
                            "contentType": "text/plain",
                            "size": 5,
                            "keywords": "field",
                            "url": "http://localhost/files/7"
                        }
                    ]
                }
                """);
            }

            if (req.Method == HttpMethod.Get &&
                path == "/rest/services/parks/FeatureServer/0/42/attachments/7")
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encoding.UTF8.GetBytes("photo"))
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = "\"photo.txt\""
                };
                return response;
            }

            if (req.Method == HttpMethod.Post &&
                path == "/rest/services/parks/FeatureServer/0/42/addAttachment")
            {
                var body = await req.Content!.ReadAsStringAsync();
                Assert.Contains("name=f", body);
                Assert.Contains("name=keywords", body);
                Assert.Contains("field", body);
                Assert.Contains("name=attachment", body);
                Assert.Contains("filename=photo.txt", body);
                Assert.Contains("text/plain", body);
                return TestHelpers.CreateRawJsonResponse("""
                { "addAttachmentResult": { "objectId": 8, "globalId": "added-global-id", "success": true } }
                """);
            }

            if (req.Method == HttpMethod.Post &&
                path == "/rest/services/parks/FeatureServer/0/42/updateAttachment")
            {
                var body = await req.Content!.ReadAsStringAsync();
                Assert.Contains("name=attachmentId", body);
                Assert.Contains("7", body);
                return TestHelpers.CreateRawJsonResponse("""
                { "updateAttachmentResult": { "objectId": 7, "globalId": "updated-global-id", "success": true } }
                """);
            }

            if (req.Method == HttpMethod.Post &&
                path == "/rest/services/parks/FeatureServer/0/42/deleteAttachments")
            {
                var body = await req.Content!.ReadAsStringAsync();
                Assert.Contains("f=json", body);
                Assert.Contains("attachmentIds=7", body);
                return TestHelpers.CreateRawJsonResponse("""
                { "deleteAttachmentResults": [{ "objectId": 7, "success": true }] }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {req.Method} {req.RequestUri}");
        });

        var source = new FeatureSource { ServiceId = "parks", LayerId = 0 };
        var attachments = (IHonuaFeatureAttachmentClient)client;

        var listed = await attachments.ListAttachmentsAsync(new FeatureAttachmentListRequest
        {
            Source = source,
            ObjectId = 42
        });

        var info = Assert.Single(listed);
        Assert.Equal(7, info.AttachmentId);
        Assert.Equal(42, info.ParentObjectId);
        Assert.Equal("attachment-global-id", info.GlobalId);
        Assert.Equal("photo.txt", info.Name);
        Assert.Equal("text/plain", info.ContentType);
        Assert.Equal(5, info.Size);
        Assert.Equal("field", info.Keywords);
        Assert.Equal(new Uri("http://localhost/files/7"), info.Url);

        var downloaded = await attachments.DownloadAttachmentAsync(new FeatureAttachmentDownloadRequest
        {
            Source = source,
            ObjectId = 42,
            AttachmentId = 7
        });
        using var reader = new StreamReader(downloaded.Content, Encoding.UTF8);
        Assert.Equal("photo", await reader.ReadToEndAsync());
        Assert.Equal("photo.txt", downloaded.Info.Name);
        Assert.Equal("text/plain", downloaded.Info.ContentType);

        using var addContent = new MemoryStream(Encoding.UTF8.GetBytes("photo"));
        var addResult = await attachments.AddAttachmentAsync(new FeatureAttachmentAddRequest
        {
            Source = source,
            ObjectId = 42,
            Name = "photo.txt",
            ContentType = "text/plain",
            Content = addContent,
            Keywords = "field"
        });
        Assert.True(addResult.Succeeded);
        Assert.Equal(8, addResult.AttachmentId);
        Assert.Equal("added-global-id", addResult.GlobalId);
        Assert.True(addContent.CanRead);

        using var updateContent = new MemoryStream(Encoding.UTF8.GetBytes("photo2"));
        var updateResult = await attachments.UpdateAttachmentAsync(new FeatureAttachmentUpdateRequest
        {
            Source = source,
            ObjectId = 42,
            AttachmentId = 7,
            Name = "photo.txt",
            ContentType = "text/plain",
            Content = updateContent,
            Keywords = "field"
        });
        Assert.True(updateResult.Succeeded);
        Assert.Equal(7, updateResult.AttachmentId);
        Assert.Equal("updated-global-id", updateResult.GlobalId);
        Assert.True(updateContent.CanRead);

        var deleteResult = await attachments.DeleteAttachmentAsync(new FeatureAttachmentDeleteRequest
        {
            Source = source,
            ObjectId = 42,
            AttachmentId = 7
        });
        Assert.True(deleteResult.Succeeded);
        Assert.Equal(7, deleteResult.AttachmentId);
    }

    [Fact]
    public async Task DownloadAttachmentAsync_HttpError_ThrowsWithStatusCode()
    {
        // Verifies the status code is read correctly (EnsureSuccess runs before the response
        // is disposed) when the download responds with a non-success status.
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.Forbidden, "Access denied")));

        var attachments = (IHonuaFeatureAttachmentClient)client;

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => attachments.DownloadAttachmentAsync(new FeatureAttachmentDownloadRequest
            {
                Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
                ObjectId = 42,
                AttachmentId = 7
            }));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task GetEditCapabilitiesAsync_ParsesLayerCapabilities()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""
            { "id": 0, "objectIdField": "OBJECTID", "capabilities": "Query,Create,Update,Delete" }
            """)));

        var capabilities = await client.GetEditCapabilitiesAsync("svc", 0);

        Assert.True(capabilities.SupportsAdds);
        Assert.True(capabilities.SupportsUpdates);
        Assert.True(capabilities.SupportsDeletes);
        Assert.True(capabilities.SupportsRollbackOnFailure);
        Assert.Equal("GeoServices FeatureServer applyEdits", capabilities.NativeSurface);
    }

    [Fact]
    public void AddHonuaFeatureServer_RegistersEditClients()
    {
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = new Uri("http://localhost:5000");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var editClient = Assert.Single(provider.GetServices<IHonuaFeatureEditClient>());
        var featureServerEditClient = Assert.Single(provider.GetServices<IHonuaFeatureServerEditClient>());
        var attachmentClient = Assert.Single(provider.GetServices<IHonuaFeatureAttachmentClient>());

        Assert.Equal("geoservices-featureserver", editClient.ProviderName);
        Assert.True(editClient.EditCapabilities.SupportsAdds);
        Assert.True(editClient.EditCapabilities.SupportsUpdates);
        Assert.False(editClient.EditCapabilities.SupportsPatches);
        Assert.True(editClient.EditCapabilities.SupportsDeletes);
        Assert.IsType<HonuaFeatureServerClient>(featureServerEditClient);
        Assert.Equal("geoservices-featureserver", attachmentClient.ProviderName);
        Assert.True(attachmentClient.AttachmentCapabilities.SupportsList);
        Assert.True(attachmentClient.AttachmentCapabilities.SupportsDownload);
        Assert.True(attachmentClient.AttachmentCapabilities.SupportsAdd);
        Assert.True(attachmentClient.AttachmentCapabilities.SupportsUpdate);
        Assert.True(attachmentClient.AttachmentCapabilities.SupportsDelete);
    }

    // ── GetFeatureAsync ────────────────────────────────────────────

    [Fact]
    public async Task GetFeatureAsync_ReturnsSingleFeatureByObjectId()
    {
        string? capturedUrl = null;
        var json = """
        {
            "objectIdFieldName": "OBJECTID",
            "features": [
                { "attributes": { "OBJECTID": 42, "NAME": "Target" } }
            ],
            "exceededTransferLimit": false
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var feature = await client.GetFeatureAsync(
            "svc",
            0,
            42,
            new FeatureServerQueryParams { OutFields = "NAME", ReturnGeometry = false });

        Assert.NotNull(feature);
        Assert.NotNull(capturedUrl);
        Assert.Contains("objectIds=42", capturedUrl);
        Assert.Contains("outFields=NAME", capturedUrl);
        Assert.Contains("returnGeometry=false", capturedUrl);
        Assert.Contains("resultRecordCount=1", capturedUrl);
        Assert.Equal("Target", feature.Attributes?["NAME"].GetString());
    }

    [Fact]
    public async Task GetFeatureAsync_ReturnsNullWhenNoFeatureReturned()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""{ "features": [] }""")));

        var feature = await client.GetFeatureAsync("svc", 0, 42);

        Assert.Null(feature);
    }

    // ── QueryCountAsync ─────────────────────────────────────────────

    [Fact]
    public async Task QueryCountAsync_ReturnsCount()
    {
        var json = """{ "count": 42 }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("returnCountOnly=true", req.RequestUri?.ToString() ?? "");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var count = await client.QueryCountAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.Equal(42, count);
    }

    // ── QueryIdsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task QueryIdsAsync_ReturnsIds()
    {
        var json = """{ "objectIds": [1, 2, 3, 10] }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("returnIdsOnly=true", req.RequestUri?.ToString() ?? "");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var ids = await client.QueryIdsAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.Equal(4, ids.Count);
        Assert.Equal(1, ids[0]);
        Assert.Equal(10, ids[3]);
    }

    // ── QueryExtentAsync ────────────────────────────────────────────

    [Fact]
    public async Task QueryExtentAsync_ReturnsExtent()
    {
        var json = """
        {
            "extent": {
                "xmin": -118.5, "ymin": 33.5, "xmax": -117.5, "ymax": 34.5,
                "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
            }
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("returnExtentOnly=true", req.RequestUri?.ToString() ?? "");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var extent = await client.QueryExtentAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.Equal(-118.5, extent.Xmin);
        Assert.Equal(34.5, extent.Ymax);
        Assert.NotNull(extent.SpatialReference);
        Assert.Equal(4326, extent.SpatialReference.Wkid);
    }

    // ── QueryPagesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task QueryPagesAsync_AutoPages()
    {
        var callCount = 0;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            callCount++;
            var json = callCount switch
            {
                1 => """
                {
                    "features": [{ "attributes": { "ID": 1 } }, { "attributes": { "ID": 2 } }],
                    "exceededTransferLimit": true
                }
                """,
                2 => """
                {
                    "features": [{ "attributes": { "ID": 3 } }],
                    "exceededTransferLimit": false
                }
                """,
                _ => """{ "features": [], "exceededTransferLimit": false }"""
            };
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryPagesAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal(2, pages[0].Features?.Count);
        Assert.Single(pages[1].Features!);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task QueryPagesAsync_StopsOnEmptyFeatures()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            var json = """{ "features": [], "exceededTransferLimit": false }""";
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryPagesAsync("svc", 0, new FeatureServerQueryParams()))
        {
            pages.Add(page);
        }

        Assert.Single(pages);
    }

    [Fact]
    public async Task QueryPagesAsync_ContinuesIdsOnlyPagesWhenTransferLimitExceeded()
    {
        var callCount = 0;
        var requestUrls = new List<string>();
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            callCount++;
            requestUrls.Add(req.RequestUri?.ToString() ?? "");
            var json = callCount switch
            {
                1 => """
                {
                    "objectIds": [1, 2],
                    "exceededTransferLimit": true
                }
                """,
                2 => """
                {
                    "objectIds": [3],
                    "exceededTransferLimit": false
                }
                """,
                _ => """{ "objectIds": [], "exceededTransferLimit": false }"""
            };
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryPagesAsync(
                           "svc",
                           0,
                           new FeatureServerQueryParams { ReturnIdsOnly = true }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal([1L, 2L], pages[0].ObjectIds);
        Assert.Equal([3L], pages[1].ObjectIds);
        Assert.Contains("returnIdsOnly=true", requestUrls[0]);
        Assert.Contains("resultOffset=2", requestUrls[1]);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task QueryPagesAsync_ServerIgnoresOffset_StopsAtMaxAutoPagesWithoutInfiniteLoop()
    {
        // Adversarial server: always returns the same page-1 with exceededTransferLimit=true,
        // ignoring resultOffset. Must terminate at the MaxAutoPages cap (100), not loop forever.
        var callCount = 0;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            callCount++;
            var json = """
            {
                "features": [{ "attributes": { "ID": 1 } }, { "attributes": { "ID": 2 } }],
                "exceededTransferLimit": true
            }
            """;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var page in client.QueryPagesAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" }))
            {
                pages.Add(page);
            }
        });

        // Bounded by MaxAutoPages (100): exactly 100 pages are yielded before the cap throws,
        // proving the loop terminates instead of running forever on duplicates.
        Assert.Equal(100, pages.Count);
        Assert.Equal(100, callCount);
    }

    [Fact]
    public async Task QueryPagesAsync_NonFinalEmptyPageWithExceededLimit_StopsWithoutDuplicates()
    {
        // A non-final page that returns 0 features while exceededTransferLimit=true must not
        // advance forever; the non-advancing cursor terminates paging cleanly.
        var callCount = 0;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            callCount++;
            var json = callCount switch
            {
                1 => """
                {
                    "features": [{ "attributes": { "ID": 1 } }],
                    "exceededTransferLimit": true
                }
                """,
                // Non-final page: 0 features but still exceededTransferLimit=true.
                _ => """{ "features": [], "exceededTransferLimit": true }"""
            };
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryPagesAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" }))
        {
            pages.Add(page);
        }

        // Page 1 (1 feature, exceeded) is yielded and continues; page 2 (0 features) is yielded
        // because the continuation signal is evaluated first, then the non-advancing cursor stops.
        Assert.Equal(2, pages.Count);
        Assert.Single(pages[0].Features!);
        Assert.Empty(pages[1].Features!);
        Assert.Equal(2, callCount);
    }

    // ── QueryStatisticsAsync ────────────────────────────────────────

    [Fact]
    public async Task QueryStatisticsAsync_ReturnsStatistics()
    {
        var json = """
        {
            "features": [
                { "attributes": { "COUNT_OBJECTID": 100, "STATE": "CA" } },
                { "attributes": { "COUNT_OBJECTID": 50, "STATE": "NY" } }
            ]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.QueryStatisticsAsync("svc", 0, new FeatureServerStatisticsParams
        {
            OutStatistics = """[{"statisticType":"count","onStatisticField":"OBJECTID","outStatisticFieldName":"COUNT_OBJECTID"}]""",
            GroupByFieldsForStatistics = "STATE"
        });

        Assert.NotNull(result.Features);
        Assert.Equal(2, result.Features.Count);
    }

    // ── ValidateSqlAsync ────────────────────────────────────────────

    [Fact]
    public async Task ValidateSqlAsync_ReturnsValidation()
    {
        var json = """{ "isValidSQL": true }""";
        HttpMethod? capturedMethod = null;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedMethod = req.Method;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.ValidateSqlAsync("svc", 0, "NAME = 'Test'");

        Assert.True(result.IsValidSql);
        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    // ── GeoServices 200-with-error ──────────────────────────────────

    [Fact]
    public async Task QueryAsync_GeoServicesError_ThrowsWithDetails()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoServicesErrorResponse(
                400, "Invalid WHERE clause", ["Syntax error at position 5"])));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("svc", 0, new FeatureServerQueryParams { Where = "BAD SQL" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(400, ex.GeoServicesErrorCode);
        Assert.Equal("Invalid WHERE clause", ex.Message);
        Assert.NotNull(ex.Details);
        Assert.Single(ex.Details);
        Assert.Contains("Syntax error", ex.Details[0]);
    }

    // ── HTTP error ──────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_HttpError_Throws()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.Forbidden, "Access denied")));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("svc", 0, new FeatureServerQueryParams()));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task GetServiceInfoAsync_NotFound_Throws()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not Found")
            }));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.GetServiceInfoAsync("noSuchService"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    // ── POST fallback for long queries ──────────────────────────────

    [Fact]
    public async Task QueryAsync_LongQuery_UsesPOST()
    {
        HttpMethod? capturedMethod = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedMethod = req.Method;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        // Generate a long WHERE clause to exceed the POST fallback threshold
        var longWhere = "OBJECTID IN (" + string.Join(",", Enumerable.Range(1, 500)) + ")";
        await client.QueryAsync("svc", 0, new FeatureServerQueryParams { Where = longWhere });

        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    // ── QueryRawAsync ───────────────────────────────────────────────

    [Fact]
    public async Task QueryRawAsync_ReturnsRawResponse()
    {
        string? capturedUrl = null;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x01, 0x02, 0x03])
            });
        });

        using var response = await client.QueryRawAsync("svc", 0,
            new FeatureServerQueryParams { Format = FeatureServerFormat.FlatGeobuf });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedUrl);
        Assert.Contains("f=flatgeobuf", capturedUrl);
    }

    private static JsonElement JsonValue<T>(T value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement JsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task<Dictionary<string, string?>> ParseFormAsync(HttpRequestMessage request)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
        return body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => WebUtility.UrlDecode(pair[0])!,
                pair => pair.Length > 1 ? WebUtility.UrlDecode(pair[1]) : null);
    }
}
