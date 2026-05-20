// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Conversion;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer.Conversion;


public class RequestConvertersTests
{
    [Fact]
    public void ToFeatureServerEditRequest_MapsFeaturesAndDeletes()
    {
        var add = new FeatureEditFeature
        {
            ObjectId = 1,
            Attributes = new Dictionary<string, JsonElement>
            {
                ["NAME"] = JsonSerializer.SerializeToElement("Alpha"),
            },
            Geometry = JsonSerializer.SerializeToElement(new { x = 10.0, y = 20.0 }),
        };
        var update = new FeatureEditFeature
        {
            ObjectId = 99,
            Attributes = new Dictionary<string, JsonElement>
            {
                ["OBJECTID"] = JsonSerializer.SerializeToElement(99),
                ["NAME"] = JsonSerializer.SerializeToElement("Beta"),
            },
        };

        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            Adds = [add],
            Updates = [update],
            Deletes = [3L, 4L],
            RollbackOnFailure = true,
            ForceWrite = true,
        };

        var edit = RequestConverters.ToFeatureServerEditRequest(request);

        Assert.NotNull(edit.Adds);
        Assert.Single(edit.Adds);
        Assert.Equal("Alpha", edit.Adds[0].Attributes!["NAME"].GetString());
        Assert.True(edit.Adds[0].Geometry.HasValue);

        Assert.NotNull(edit.Updates);
        Assert.Single(edit.Updates);
        Assert.Equal(99, edit.Updates[0].Attributes!["OBJECTID"].GetInt32());

        Assert.Equal([3L, 4L], edit.Deletes);
        Assert.True(edit.RollbackOnFailure);
        Assert.True(edit.ForceWrite);
    }

    [Fact]
    public void ToFeatureServerEditRequest_ParsesJsonFallbacks()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            AddsJson = """[{"attributes":{"OBJECTID":1,"NAME":"X"}}]""",
            DeletesCsv = "10, 11, 12",
        };

        var edit = RequestConverters.ToFeatureServerEditRequest(request);

        Assert.NotNull(edit.Adds);
        Assert.Single(edit.Adds);
        Assert.Equal("X", edit.Adds[0].Attributes!["NAME"].GetString());
        Assert.Equal([10L, 11L, 12L], edit.Deletes);
    }

    [Fact]
    public void ToFeatureServerEditRequest_ThrowsOnInvalidJson()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            AddsJson = "{not-json",
        };

        Assert.Throws<ArgumentException>(() => RequestConverters.ToFeatureServerEditRequest(request));
    }

    [Fact]
    public void ToFeatureServerEditRequest_ThrowsOnNonNumericDeletesCsv()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            DeletesCsv = "1,abc,3",
        };

        Assert.Throws<ArgumentException>(() => RequestConverters.ToFeatureServerEditRequest(request));
    }

    [Fact]
    public void ToFeatureServerEditFormParameters_SerializesRequiredFields()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            Adds =
            [
                new FeatureEditFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["NAME"] = JsonSerializer.SerializeToElement("Alpha"),
                    },
                },
            ],
            Deletes = [1L, 2L],
            RollbackOnFailure = true,
            ForceWrite = false,
        };

        var form = RequestConverters.ToFeatureServerEditFormParameters(request);

        Assert.Equal("json", form["f"]);
        Assert.Equal("true", form["rollbackOnFailure"]);
        Assert.False(form.ContainsKey("forceWrite"));
        Assert.Equal("1,2", form["deletes"]);
        Assert.Contains("Alpha", form["adds"]);
    }

    [Fact]
    public void ToFeatureServerFeature_ClonesAttributes()
    {
        var feature = new FeatureEditFeature
        {
            Attributes = new Dictionary<string, JsonElement>
            {
                ["NAME"] = JsonSerializer.SerializeToElement("Alpha"),
            },
            Geometry = JsonSerializer.SerializeToElement(new { x = 1.0, y = 2.0 }),
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.Equal("Alpha", converted.Attributes!["NAME"].GetString());
        Assert.True(converted.Geometry.HasValue);
    }

    [Fact]
    public void ToFeatureServerFeature_ProjectsGeoJsonPointToXy()
    {
        var feature = new FeatureEditFeature
        {
            Geometry = JsonSerializer.SerializeToElement(new
            {
                type = "Point",
                coordinates = new[] { -122.0, 37.5 },
            }),
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.True(converted.Geometry.HasValue);
        var geometry = converted.Geometry!.Value;
        Assert.Equal(JsonValueKind.Object, geometry.ValueKind);
        Assert.False(geometry.TryGetProperty("type", out _));
        Assert.Equal(-122.0, geometry.GetProperty("x").GetDouble());
        Assert.Equal(37.5, geometry.GetProperty("y").GetDouble());
        Assert.False(geometry.TryGetProperty("z", out _));
    }

    [Fact]
    public void ToFeatureServerFeature_ProjectsGeoJsonPointWithZ()
    {
        var feature = new FeatureEditFeature
        {
            Geometry = JsonSerializer.SerializeToElement(new
            {
                type = "Point",
                coordinates = new[] { -122.0, 37.5, 12.25 },
            }),
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.True(converted.Geometry.HasValue);
        var geometry = converted.Geometry!.Value;
        Assert.Equal(-122.0, geometry.GetProperty("x").GetDouble());
        Assert.Equal(37.5, geometry.GetProperty("y").GetDouble());
        Assert.Equal(12.25, geometry.GetProperty("z").GetDouble());
    }

    [Fact]
    public void ToFeatureServerFeature_PassesThroughFeatureServerShapedPoint()
    {
        var feature = new FeatureEditFeature
        {
            Geometry = JsonSerializer.SerializeToElement(new
            {
                x = -122.0,
                y = 37.5,
                spatialReference = new { wkid = 4326 },
            }),
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.True(converted.Geometry.HasValue);
        var geometry = converted.Geometry!.Value;
        Assert.Equal(-122.0, geometry.GetProperty("x").GetDouble());
        Assert.Equal(37.5, geometry.GetProperty("y").GetDouble());
        Assert.Equal(4326, geometry.GetProperty("spatialReference").GetProperty("wkid").GetInt32());
    }

    [Fact]
    public void ToFeatureServerFeature_ProjectsGeoJsonLineStringToPaths()
    {
        var feature = new FeatureEditFeature
        {
            Geometry = JsonSerializer.SerializeToElement(new
            {
                type = "LineString",
                coordinates = new[]
                {
                    new[] { 0.0, 0.0 },
                    new[] { 1.0, 1.0 },
                    new[] { 2.0, 0.0 },
                },
            }),
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.True(converted.Geometry.HasValue);
        var geometry = converted.Geometry!.Value;
        Assert.False(geometry.TryGetProperty("type", out _));
        var paths = geometry.GetProperty("paths");
        Assert.Equal(JsonValueKind.Array, paths.ValueKind);
        Assert.Equal(1, paths.GetArrayLength());
        var path = paths[0];
        Assert.Equal(3, path.GetArrayLength());
        Assert.Equal(0.0, path[0][0].GetDouble());
        Assert.Equal(0.0, path[0][1].GetDouble());
        Assert.Equal(2.0, path[2][0].GetDouble());
        Assert.Equal(0.0, path[2][1].GetDouble());
    }

    [Fact]
    public void ToFeatureServerFeature_ProjectsGeoJsonPolygonToRings()
    {
        var feature = new FeatureEditFeature
        {
            Geometry = JsonSerializer.SerializeToElement(new
            {
                type = "Polygon",
                coordinates = new[]
                {
                    new[]
                    {
                        new[] { 0.0, 0.0 },
                        new[] { 0.0, 1.0 },
                        new[] { 1.0, 1.0 },
                        new[] { 1.0, 0.0 },
                        new[] { 0.0, 0.0 },
                    },
                },
            }),
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.True(converted.Geometry.HasValue);
        var geometry = converted.Geometry!.Value;
        Assert.False(geometry.TryGetProperty("type", out _));
        var rings = geometry.GetProperty("rings");
        Assert.Equal(JsonValueKind.Array, rings.ValueKind);
        Assert.Equal(1, rings.GetArrayLength());
        var ring = rings[0];
        Assert.Equal(5, ring.GetArrayLength());
        Assert.Equal(0.0, ring[0][0].GetDouble());
        Assert.Equal(0.0, ring[0][1].GetDouble());
        var lastIndex = ring.GetArrayLength() - 1;
        Assert.Equal(0.0, ring[lastIndex][0].GetDouble());
        Assert.Equal(0.0, ring[lastIndex][1].GetDouble());
    }

    [Fact]
    public void ToFeatureServerFeature_LeavesNullGeometryNull()
    {
        var feature = new FeatureEditFeature
        {
            Attributes = new Dictionary<string, JsonElement>
            {
                ["NAME"] = JsonSerializer.SerializeToElement("Alpha"),
            },
        };

        var converted = RequestConverters.ToFeatureServerFeature(feature);

        Assert.False(converted.Geometry.HasValue);
    }

    [Fact]
    public void ToFeatureServerDeleteObjectIds_CombinesNumericAndStringIds()
    {
        var request = new FeatureEditRequest
        {
            DeleteObjectIds = [1L, 2L],
            DeleteIds = ["3", "4"],
        };

        var ids = RequestConverters.ToFeatureServerDeleteObjectIds(request);

        Assert.Equal([1L, 2L, 3L, 4L], ids);
    }

    [Fact]
    public void ToFeatureServerDeleteObjectIds_ReturnsNullWhenEmpty()
    {
        var request = new FeatureEditRequest();
        Assert.Null(RequestConverters.ToFeatureServerDeleteObjectIds(request));
    }

    [Fact]
    public void ToFeatureServerDeleteObjectIds_ThrowsOnNonNumericId()
    {
        var request = new FeatureEditRequest
        {
            DeleteIds = ["abc"],
        };

        Assert.Throws<ArgumentException>(() => RequestConverters.ToFeatureServerDeleteObjectIds(request));
    }

    [Theory]
    [InlineData(null, FeatureServerFormat.Json)]
    [InlineData("", FeatureServerFormat.Json)]
    [InlineData("json", FeatureServerFormat.Json)]
    [InlineData("GEOJSON", FeatureServerFormat.GeoJson)]
    [InlineData("pbf", FeatureServerFormat.Pbf)]
    [InlineData("flatgeobuf", FeatureServerFormat.FlatGeobuf)]
    [InlineData("parquet", FeatureServerFormat.Parquet)]
    public void ToFeatureServerQueryParams_MapsFormat(string? format, FeatureServerFormat expected)
    {
        var request = new QueryFeaturesRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            ResponseFormat = format ?? "json",
        };

        var qp = RequestConverters.ToFeatureServerQueryParams(request);
        Assert.Equal(expected, qp.Format);
    }

    [Fact]
    public void ToFeatureServerQueryParams_MapsAllFields()
    {
        var request = new QueryFeaturesRequest
        {
            ServiceId = "svc",
            LayerId = 1,
            Where = "STATUS='Open'",
            ObjectIds = [1L, 2L],
            OutFields = ["NAME", "STATUS"],
            ReturnGeometry = false,
            ResultOffset = 25,
            ResultRecordCount = 50,
            OrderBy = "NAME ASC",
            ReturnDistinct = true,
            ReturnCountOnly = true,
            ReturnIdsOnly = true,
            ReturnExtentOnly = true,
        };

        var qp = RequestConverters.ToFeatureServerQueryParams(request);

        Assert.Equal("STATUS='Open'", qp.Where);
        Assert.Equal([1L, 2L], qp.ObjectIds);
        Assert.Equal("NAME,STATUS", qp.OutFields);
        Assert.False(qp.ReturnGeometry);
        Assert.Equal(25, qp.ResultOffset);
        Assert.Equal(50, qp.ResultRecordCount);
        Assert.Equal("NAME ASC", qp.OrderByFields);
        Assert.True(qp.ReturnDistinctValues);
        Assert.True(qp.ReturnCountOnly);
        Assert.True(qp.ReturnIdsOnly);
        Assert.True(qp.ReturnExtentOnly);
    }

    [Fact]
    public void ToFeatureServerQueryParams_DefaultsOutFieldsToWildcard()
    {
        var request = new QueryFeaturesRequest { ServiceId = "svc", LayerId = 0 };

        var qp = RequestConverters.ToFeatureServerQueryParams(request);

        Assert.Equal("*", qp.OutFields);
    }

    [Fact]
    public void ToJsonDocument_QueryResponse_RoundTrips()
    {
        var response = new FeatureServerQueryResponse
        {
            ObjectIdFieldName = "OBJECTID",
            ExceededTransferLimit = true,
        };

        using var doc = RequestConverters.ToJsonDocument(response);
        Assert.Equal("OBJECTID", doc.RootElement.GetProperty("objectIdFieldName").GetString());
        Assert.True(doc.RootElement.GetProperty("exceededTransferLimit").GetBoolean());
    }

    [Fact]
    public void ToJsonDocument_EditResponse_RoundTrips()
    {
        var response = new FeatureServerEditResponse
        {
            AddResults = [new FeatureServerEditResult { ObjectId = 1, Success = true }],
        };

        using var doc = RequestConverters.ToJsonDocument(response);
        Assert.Equal(1L, doc.RootElement.GetProperty("addResults")[0].GetProperty("objectId").GetInt64());
        Assert.True(doc.RootElement.GetProperty("addResults")[0].GetProperty("success").GetBoolean());
    }
}

