// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests.Features;

public sealed class RequestModelsTests
{
    [Fact]
    public void QueryFeaturesRequest_HasExpectedDefaults()
    {
        var request = new QueryFeaturesRequest
        {
            ServiceId = "svc",
            LayerId = 0
        };

        Assert.Equal("svc", request.ServiceId);
        Assert.Equal(0, request.LayerId);
        Assert.Null(request.Where);
        Assert.Null(request.ObjectIds);
        Assert.Null(request.OutFields);
        Assert.True(request.ReturnGeometry);
        Assert.Null(request.ResultOffset);
        Assert.Null(request.ResultRecordCount);
        Assert.Null(request.OrderBy);
        Assert.False(request.ReturnDistinct);
        Assert.False(request.ReturnCountOnly);
        Assert.False(request.ReturnIdsOnly);
        Assert.False(request.ReturnExtentOnly);
        Assert.Equal("json", request.ResponseFormat);
    }

    [Fact]
    public void QueryFeaturesRequest_PropertiesRoundTrip()
    {
        var objectIds = new long[] { 1, 2, 3 };
        var outFields = new[] { "objectid", "name" };

        var request = new QueryFeaturesRequest
        {
            ServiceId = "catalog/parcels",
            LayerId = 4,
            Where = "name = 'foo'",
            ObjectIds = objectIds,
            OutFields = outFields,
            ReturnGeometry = false,
            ResultOffset = 50,
            ResultRecordCount = 100,
            OrderBy = "name ASC",
            ReturnDistinct = true,
            ReturnCountOnly = true,
            ReturnIdsOnly = true,
            ReturnExtentOnly = true,
            ResponseFormat = "geojson"
        };

        Assert.Equal("catalog/parcels", request.ServiceId);
        Assert.Equal(4, request.LayerId);
        Assert.Equal("name = 'foo'", request.Where);
        Assert.Same(objectIds, request.ObjectIds);
        Assert.Same(outFields, request.OutFields);
        Assert.False(request.ReturnGeometry);
        Assert.Equal(50, request.ResultOffset);
        Assert.Equal(100, request.ResultRecordCount);
        Assert.Equal("name ASC", request.OrderBy);
        Assert.True(request.ReturnDistinct);
        Assert.True(request.ReturnCountOnly);
        Assert.True(request.ReturnIdsOnly);
        Assert.True(request.ReturnExtentOnly);
        Assert.Equal("geojson", request.ResponseFormat);
    }

    [Fact]
    public void ApplyEditsRequest_HasExpectedDefaults()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0
        };

        Assert.Equal("svc", request.ServiceId);
        Assert.Equal(0, request.LayerId);
        Assert.Null(request.Adds);
        Assert.Null(request.Updates);
        Assert.Null(request.Deletes);
        Assert.Null(request.AddsJson);
        Assert.Null(request.UpdatesJson);
        Assert.Null(request.DeletesCsv);
        Assert.False(request.RollbackOnFailure);
        Assert.False(request.ForceWrite);
        Assert.Equal("json", request.ResponseFormat);
    }

    [Fact]
    public void ApplyEditsRequest_PropertiesRoundTrip()
    {
        var adds = new[] { new FeatureEditFeature { Id = "a" } };
        var updates = new[] { new FeatureEditFeature { ObjectId = 7 } };
        var deletes = new long[] { 10, 11 };

        var request = new ApplyEditsRequest
        {
            ServiceId = "catalog/parcels",
            LayerId = 2,
            Adds = adds,
            Updates = updates,
            Deletes = deletes,
            AddsJson = "[{\"attributes\":{}}]",
            UpdatesJson = "[]",
            DeletesCsv = "10,11",
            RollbackOnFailure = true,
            ForceWrite = true,
            ResponseFormat = "geojson"
        };

        Assert.Equal("catalog/parcels", request.ServiceId);
        Assert.Equal(2, request.LayerId);
        Assert.Same(adds, request.Adds);
        Assert.Same(updates, request.Updates);
        Assert.Same(deletes, request.Deletes);
        Assert.Equal("[{\"attributes\":{}}]", request.AddsJson);
        Assert.Equal("[]", request.UpdatesJson);
        Assert.Equal("10,11", request.DeletesCsv);
        Assert.True(request.RollbackOnFailure);
        Assert.True(request.ForceWrite);
        Assert.Equal("geojson", request.ResponseFormat);
    }

    [Fact]
    public void OgcItemsRequest_HasExpectedDefaults()
    {
        var request = new OgcItemsRequest { CollectionId = "parcels" };

        Assert.Equal("parcels", request.CollectionId);
        Assert.Null(request.Limit);
        Assert.Null(request.Offset);
        Assert.Null(request.PropertyNames);
        Assert.Null(request.CqlFilter);
        Assert.Equal("json", request.ResponseFormat);
    }

    [Fact]
    public void OgcItemsRequest_PropertiesRoundTrip()
    {
        var properties = new[] { "name", "owner" };

        var request = new OgcItemsRequest
        {
            CollectionId = "parcels",
            Limit = 25,
            Offset = 100,
            PropertyNames = properties,
            CqlFilter = "name = 'foo'",
            ResponseFormat = "geojson"
        };

        Assert.Equal("parcels", request.CollectionId);
        Assert.Equal(25, request.Limit);
        Assert.Equal(100, request.Offset);
        Assert.Same(properties, request.PropertyNames);
        Assert.Equal("name = 'foo'", request.CqlFilter);
        Assert.Equal("geojson", request.ResponseFormat);
    }

    [Fact]
    public void OgcCreateItemRequest_RequiresCollectionAndFeature()
    {
        var feature = ParseJson("""{"type":"Feature","properties":{}}""");

        var request = new OgcCreateItemRequest
        {
            CollectionId = "parcels",
            Feature = feature
        };

        Assert.Equal("parcels", request.CollectionId);
        Assert.Equal(JsonValueKind.Object, request.Feature.ValueKind);
        Assert.Equal("Feature", request.Feature.GetProperty("type").GetString());
    }

    [Fact]
    public void OgcReplaceItemRequest_RoundTripsAllProperties()
    {
        var feature = ParseJson("""{"type":"Feature","id":"abc"}""");

        var request = new OgcReplaceItemRequest
        {
            CollectionId = "parcels",
            FeatureId = "abc",
            Feature = feature
        };

        Assert.Equal("parcels", request.CollectionId);
        Assert.Equal("abc", request.FeatureId);
        Assert.Equal(JsonValueKind.Object, request.Feature.ValueKind);
        Assert.Equal("abc", request.Feature.GetProperty("id").GetString());
    }

    [Fact]
    public void OgcPatchItemRequest_RoundTripsAllProperties()
    {
        var patch = ParseJson("""{"properties":{"name":"updated"}}""");

        var request = new OgcPatchItemRequest
        {
            CollectionId = "parcels",
            FeatureId = "abc",
            Patch = patch
        };

        Assert.Equal("parcels", request.CollectionId);
        Assert.Equal("abc", request.FeatureId);
        Assert.Equal(JsonValueKind.Object, request.Patch.ValueKind);
        Assert.Equal("updated", request.Patch.GetProperty("properties").GetProperty("name").GetString());
    }

    [Fact]
    public void OgcDeleteItemRequest_RoundTripsAllProperties()
    {
        var request = new OgcDeleteItemRequest
        {
            CollectionId = "parcels",
            FeatureId = "abc"
        };

        Assert.Equal("parcels", request.CollectionId);
        Assert.Equal("abc", request.FeatureId);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
