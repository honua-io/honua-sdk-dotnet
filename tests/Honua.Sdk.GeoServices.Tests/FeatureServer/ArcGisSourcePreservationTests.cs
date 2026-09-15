// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

/// <summary>
/// Preservation gaps found by the #341 installed-package certification against the 2026.1
/// candidate (certification/geoservices-source-import): typed metadata that parsed but dropped
/// source members, Z/M that could not be requested, and offset paging that yielded duplicates
/// from a source ignoring <c>resultOffset</c>. Fixtures are raw ArcGIS REST JSON; every source
/// member must survive the typed model, so recognition without preservation fails.
/// </summary>
public sealed class ArcGisSourcePreservationTests
{
    private const string ServiceJson = """
        {
          "currentVersion": 11.3,
          "serviceDescription": "Parcels",
          "maxRecordCount": 2000,
          "capabilities": "Query",
          "units": "esriMeters",
          "layers": [ { "id": 0, "name": "Parcels", "type": "Feature Layer", "geometryType": "esriGeometryPolygon" } ],
          "tables": [ { "id": 3, "name": "Inspections", "type": "Table" } ]
        }
        """;

    private const string LayerJson = """
        {
          "id": 0,
          "name": "Parcels",
          "type": "Feature Layer",
          "geometryType": "esriGeometryPolygon",
          "hasZ": true,
          "hasM": false,
          "supportsPagination": false,
          "advancedQueryCapabilities": { "supportsPagination": false, "supportsOrderBy": true },
          "objectIdField": "OBJECTID",
          "globalIdField": "PARCEL_GUID",
          "displayField": "NAME",
          "typeIdField": "ZONE",
          "types": [ { "id": 1, "name": "Residential", "domains": {}, "templates": [] } ],
          "subtypes": [ { "code": 1, "name": "Residential", "defaultValues": { "ZONE": 1 }, "domains": {} } ],
          "relationships": [
            {
              "id": 0,
              "name": "Parcel_Inspections",
              "relatedTableId": 3,
              "cardinality": "esriRelCardinalityOneToMany",
              "role": "esriRelRoleOrigin",
              "keyField": "PARCEL_GUID",
              "composite": false
            }
          ],
          "drawingInfo": {
            "renderer": {
              "type": "uniqueValue",
              "field1": "ZONE",
              "uniqueValueInfos": [ { "value": "1", "label": "Residential", "symbol": { "type": "esriSFS", "color": [ 10, 20, 30, 255 ] } } ]
            },
            "transparency": 25,
            "labelingInfo": [
              {
                "labelExpressionInfo": { "expression": "$feature.NAME" },
                "labelPlacement": "esriServerPolygonPlacementAlwaysHorizontal",
                "minScale": 25000
              }
            ]
          },
          "timeInfo": {
            "startTimeField": "CREATED",
            "endTimeField": null,
            "timeExtent": [ 1577836800000, null ],
            "timeReference": { "timeZone": "UTC", "respectsDaylightSaving": false },
            "timeInterval": 1,
            "timeIntervalUnits": "esriTimeUnitsDays"
          },
          "fields": [
            { "name": "OBJECTID", "type": "esriFieldTypeOID", "alias": "OBJECTID", "sqlType": "sqlTypeOther", "nullable": false, "editable": false, "defaultValue": null, "modelName": "OBJECTID" },
            { "name": "PARCEL_GUID", "type": "esriFieldTypeGlobalID", "alias": "Parcel GUID", "sqlType": "sqlTypeOther", "length": 38, "nullable": false, "editable": false, "defaultValue": null, "modelName": "PARCEL_GUID" }
          ],
          "templates": [],
          "maxRecordCount": 2000,
          "x-vendorExtension": { "k": [ 1, 2.5, "three", null ] }
        }
        """;

    [Fact]
    public async Task GetServiceInfoAsync_TablesAndUnmodelledMembers_ArePreserved()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ => Task.FromResult(TestHelpers.CreateRawJsonResponse(ServiceJson)));

        var service = await client.GetServiceInfoAsync("Parcels");

        var table = Assert.Single(service.Tables!);
        Assert.Equal(3, table.Id);
        Assert.Equal("Inspections", table.Name);
        Assert.Equal("Table", table.AdditionalProperties!["type"].GetString());
        Assert.Equal("esriGeometryPolygon", service.Layers![0].AdditionalProperties!["geometryType"].GetString());
        Assert.Equal("esriMeters", service.AdditionalProperties!["units"].GetString());
        Assert.Equal("11.3", service.AdditionalProperties["currentVersion"].GetRawText());
        AssertEverySourceMemberPreserved(ServiceJson, service);
    }

    [Fact]
    public async Task GetLayerInfoAsync_DrawingInfoRelationshipsSubtypesAndPaging_ArePreservedLosslessly()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ => Task.FromResult(TestHelpers.CreateRawJsonResponse(LayerJson)));

        var layer = await client.GetLayerInfoAsync("Parcels", 0);

        Assert.Equal("Feature Layer", layer.Type);
        Assert.True(layer.HasZ);
        Assert.False(layer.HasM);
        Assert.False(layer.SupportsPagination);
        Assert.False(layer.AdvancedQueryCapabilities!.Value.GetProperty("supportsPagination").GetBoolean());
        Assert.Equal("ZONE", layer.TypeIdField);
        Assert.Equal("Residential", layer.Types!.Value[0].GetProperty("name").GetString());
        Assert.Equal(1, layer.Subtypes!.Value[0].GetProperty("defaultValues").GetProperty("ZONE").GetInt32());
        Assert.Equal("PARCEL_GUID", layer.Relationships!.Value[0].GetProperty("keyField").GetString());
        Assert.Equal(3, layer.Relationships!.Value[0].GetProperty("relatedTableId").GetInt32());
        Assert.Equal(
            "$feature.NAME",
            layer.DrawingInfo!.Value.GetProperty("labelingInfo")[0].GetProperty("labelExpressionInfo").GetProperty("expression").GetString());
        Assert.Equal(1577836800000L, layer.TimeInfo!.TimeExtent!.Value[0].GetInt64());
        Assert.Equal(JsonValueKind.Null, layer.TimeInfo!.TimeExtent!.Value[1].ValueKind);
        Assert.Equal("esriTimeUnitsDays", layer.TimeInfo.AdditionalProperties!["timeIntervalUnits"].GetString());
        Assert.Equal("sqlTypeOther", layer.Fields![0].AdditionalProperties!["sqlType"].GetString());
        Assert.Equal("PARCEL_GUID", layer.Fields[1].AdditionalProperties!["modelName"].GetString());
        Assert.Equal("NAME", layer.AdditionalProperties!["displayField"].GetString());
        Assert.Equal("""[ 1, 2.5, "three", null ]""", layer.AdditionalProperties["x-vendorExtension"].GetProperty("k").GetRawText());
        AssertEverySourceMemberPreserved(LayerJson, layer);
    }

    [Fact]
    public async Task QueryAsync_ReturnZAndReturnM_AreSentOnlyWhenRequested()
    {
        var requests = new List<string>();
        var client = TestHelpers.CreateFeatureServerClient(request =>
        {
            requests.Add(request.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse("""{ "features": [] }"""));
        });

        await client.QueryAsync("Parcels", 0, new FeatureServerQueryParams { ReturnZ = true, ReturnM = true });
        await client.QueryAsync("Parcels", 0, new FeatureServerQueryParams { ReturnZ = false });
        await client.QueryAsync("Parcels", 0, new FeatureServerQueryParams());

        Assert.Contains("returnZ=true", requests[0], StringComparison.Ordinal);
        Assert.Contains("returnM=true", requests[0], StringComparison.Ordinal);
        Assert.Contains("returnZ=false", requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain("returnM", requests[1], StringComparison.Ordinal);
        Assert.DoesNotContain("returnZ", requests[2], StringComparison.Ordinal);
        Assert.DoesNotContain("returnM", requests[2], StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryPagesAsync_SourceRepeatsPage_FailsBeforeYieldingDuplicates()
    {
        var calls = 0;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            calls++;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(
                """{ "objectIdFieldName": "OBJECTID", "features": [ { "attributes": { "OBJECTID": 1 } }, { "attributes": { "OBJECTID": 2 } } ], "exceededTransferLimit": true }"""));
        });

        var yielded = new List<long>();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var page in client.QueryPagesAsync("Parcels", 0, new FeatureServerQueryParams { ResultRecordCount = 2 }))
            {
                yielded.AddRange(page.Features!.Select(feature => feature.Attributes!["OBJECTID"].GetInt64()));
            }
        });

        Assert.Equal([1L, 2L], yielded);
        Assert.Equal(2, calls);
        Assert.Contains("QueryAllFeaturesByObjectIdBatchesAsync", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryPagesAsync_SourceOverlapsPreviousPage_FailsBeforeYieldingDuplicates()
    {
        var client = TestHelpers.CreateFeatureServerClient(request =>
        {
            var json = request.RequestUri!.Query.Contains("resultOffset=2", StringComparison.Ordinal)
                ? """{ "objectIdFieldName": "OBJECTID", "features": [ { "attributes": { "OBJECTID": 2 } }, { "attributes": { "OBJECTID": 3 } } ], "exceededTransferLimit": true }"""
                : """{ "objectIdFieldName": "OBJECTID", "features": [ { "attributes": { "OBJECTID": 1 } }, { "attributes": { "OBJECTID": 2 } } ], "exceededTransferLimit": true }""";
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.QueryPagesAsync("Parcels", 0, new FeatureServerQueryParams { ResultRecordCount = 2 }))
            {
                pages++;
            }
        });

        Assert.Equal(1, pages);
    }

    [Fact]
    public async Task QueryPagesAsync_RepeatedPageWithoutObjectIdField_FailsBeforeYieldingDuplicates()
    {
        var calls = 0;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            calls++;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(
                """{ "features": [ { "attributes": { "NAME": "a" }, "geometry": { "x": 1, "y": 2 } } ], "exceededTransferLimit": true }"""));
        });

        var pages = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in client.QueryPagesAsync("Parcels", 0, new FeatureServerQueryParams { OutFields = "NAME", ResultRecordCount = 1 }))
            {
                pages++;
            }
        });

        Assert.Equal(1, pages);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task QueryPagesAsync_DistinctPagesWithoutObjectIdField_Complete()
    {
        var client = TestHelpers.CreateFeatureServerClient(request =>
        {
            var json = request.RequestUri!.Query.Contains("resultOffset=1", StringComparison.Ordinal)
                ? """{ "features": [ { "attributes": { "NAME": "b" } } ], "exceededTransferLimit": false }"""
                : """{ "features": [ { "attributes": { "NAME": "a" } } ], "exceededTransferLimit": true }""";
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var names = new List<string>();
        await foreach (var page in client.QueryPagesAsync("Parcels", 0, new FeatureServerQueryParams { OutFields = "NAME", ResultRecordCount = 1 }))
        {
            names.AddRange(page.Features!.Select(feature => feature.Attributes!["NAME"].GetString()!));
        }

        Assert.Equal(["a", "b"], names);
    }

    private static void AssertEverySourceMemberPreserved<T>(string sourceJson, T model)
    {
        var decoded = JsonSerializer.SerializeToNode(model, new JsonSerializerOptions(JsonSerializerDefaults.General));
        var lost = new List<string>();
        CollectLoss(JsonNode.Parse(sourceJson), decoded, "$", lost);
        Assert.True(lost.Count == 0, $"Source members lost by {typeof(T).Name}: {string.Join("; ", lost)}");
    }

    private static void CollectLoss(JsonNode? source, JsonNode? decoded, string path, List<string> lost)
    {
        switch (source)
        {
            case null:
                if (decoded is not null && decoded.GetValueKind() != JsonValueKind.Null)
                {
                    lost.Add($"{path}: null became {decoded.ToJsonString()}");
                }

                break;
            case JsonObject sourceObject:
                if (decoded is not JsonObject decodedObject)
                {
                    lost.Add($"{path}: object not preserved");
                    break;
                }

                foreach (var (name, value) in sourceObject)
                {
                    if (decodedObject.TryGetPropertyValue(name, out var decodedValue))
                    {
                        CollectLoss(value, decodedValue, $"{path}.{name}", lost);
                    }
                    else
                    {
                        lost.Add($"{path}.{name}: dropped");
                    }
                }

                break;
            case JsonArray sourceArray:
                if (decoded is not JsonArray decodedArray || decodedArray.Count != sourceArray.Count)
                {
                    lost.Add($"{path}: array not preserved");
                    break;
                }

                for (var i = 0; i < sourceArray.Count; i++)
                {
                    CollectLoss(sourceArray[i], decodedArray[i], $"{path}[{i}]", lost);
                }

                break;
            default:
                if (!JsonNode.DeepEquals(source, decoded))
                {
                    lost.Add($"{path}: {source.ToJsonString()} became {decoded?.ToJsonString() ?? "(dropped)"}");
                }

                break;
        }
    }
}
