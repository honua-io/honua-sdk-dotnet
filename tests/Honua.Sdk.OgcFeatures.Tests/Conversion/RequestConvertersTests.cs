// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures.Conversion;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures.Tests.Conversion;


public class RequestConvertersTests
{
    [Fact]
    public void ToOgcFeature_PopulatesIdGeometryAndProperties()
    {
        var feature = new FeatureEditFeature
        {
            Id = "abc",
            Attributes = new Dictionary<string, JsonElement>
            {
                ["NAME"] = JsonSerializer.SerializeToElement("Alpha"),
            },
            Geometry = JsonSerializer.SerializeToElement(new { type = "Point", coordinates = new[] { 1.0, 2.0 } }),
        };

        var ogc = RequestConverters.ToOgcFeature(feature);

        Assert.True(ogc.Id.HasValue);
        Assert.Equal("abc", ogc.Id!.Value.GetString());
        Assert.True(ogc.Geometry.HasValue);
        Assert.Equal("Alpha", ogc.Properties!["NAME"].GetString());
    }

    [Fact]
    public void ToOgcFeature_FallsBackToObjectIdWhenNoId()
    {
        var feature = new FeatureEditFeature
        {
            ObjectId = 99,
            Attributes = new Dictionary<string, JsonElement>(),
        };

        var ogc = RequestConverters.ToOgcFeature(feature);

        Assert.True(ogc.Id.HasValue);
        Assert.Equal("99", ogc.Id!.Value.GetString());
    }

    [Fact]
    public void ToOgcFeature_OmitsIdWhenNeitherIdNorObjectIdSet()
    {
        var feature = new FeatureEditFeature
        {
            Attributes = new Dictionary<string, JsonElement>(),
        };

        var ogc = RequestConverters.ToOgcFeature(feature);

        Assert.Null(ogc.Id);
    }

    [Fact]
    public void ToOgcFeature_AcceptsExplicitFeatureId()
    {
        var feature = new FeatureEditFeature
        {
            ObjectId = 1,
            Attributes = new Dictionary<string, JsonElement>(),
        };

        var ogc = RequestConverters.ToOgcFeature(feature, featureId: "override");

        Assert.Equal("override", ogc.Id!.Value.GetString());
    }

    [Fact]
    public void ToOgcFeatureId_FormatsInvariant()
    {
        Assert.Equal("12345", RequestConverters.ToOgcFeatureId(12345L));
    }

    [Theory]
    [InlineData(null, OgcFeaturesFormat.Json)]
    [InlineData("", OgcFeaturesFormat.Json)]
    [InlineData("json", OgcFeaturesFormat.Json)]
    [InlineData("GEOJSON", OgcFeaturesFormat.GeoJson)]
    [InlineData("html", OgcFeaturesFormat.Html)]
    [InlineData("gml", OgcFeaturesFormat.Gml)]
    [InlineData("csv", OgcFeaturesFormat.Csv)]
    [InlineData("flatgeobuf", OgcFeaturesFormat.FlatGeobuf)]
    [InlineData("parquet", OgcFeaturesFormat.Parquet)]
    public void ToOgcItemsParams_MapsFormat(string? format, OgcFeaturesFormat expected)
    {
        var request = new OgcItemsRequest
        {
            CollectionId = "c",
            ResponseFormat = format ?? "json",
        };

        var qp = RequestConverters.ToOgcItemsParams(request);
        Assert.Equal(expected, qp.Format);
    }

    [Fact]
    public void ToOgcItemsParams_JoinsPropertyNames()
    {
        var request = new OgcItemsRequest
        {
            CollectionId = "c",
            Limit = 10,
            Offset = 20,
            PropertyNames = ["name", "status"],
            CqlFilter = "status='Open'",
        };

        var qp = RequestConverters.ToOgcItemsParams(request);

        Assert.Equal(10, qp.Limit);
        Assert.Equal(20, qp.Offset);
        Assert.Equal("name,status", qp.Properties);
        Assert.Equal("status='Open'", qp.Filter);
    }

    [Fact]
    public void ToOgcItemsParams_LeavesPropertiesNullWhenEmpty()
    {
        var request = new OgcItemsRequest { CollectionId = "c" };

        var qp = RequestConverters.ToOgcItemsParams(request);

        Assert.Null(qp.Properties);
    }

    [Fact]
    public void ToOgcFeature_ReturnsSameInstanceWhenAlreadyOgcFeature()
    {
        var feature = new OgcFeature { Type = "Feature" };

        var result = RequestConverters.ToOgcFeature((object)feature);

        Assert.Same(feature, result);
    }

    [Fact]
    public void ToOgcFeature_AcceptsJsonElement()
    {
        var element = JsonSerializer.SerializeToElement(new
        {
            type = "Feature",
            properties = new { name = "Alpha" },
        });

        var result = RequestConverters.ToOgcFeature((object)element);

        Assert.Equal("Alpha", result.Properties!["name"].GetString());
    }

    [Fact]
    public void ToOgcFeature_AcceptsJsonDocument()
    {
        using var doc = JsonDocument.Parse("""{"type":"Feature","properties":{"name":"Beta"}}""");

        var result = RequestConverters.ToOgcFeature((object)doc);

        Assert.Equal("Beta", result.Properties!["name"].GetString());
    }

    [Fact]
    public void ToOgcFeature_AcceptsFeatureEditFeature()
    {
        var edit = new FeatureEditFeature
        {
            Id = "1",
            Attributes = new Dictionary<string, JsonElement>
            {
                ["status"] = JsonSerializer.SerializeToElement("Open"),
            },
        };

        var result = RequestConverters.ToOgcFeature((object)edit);

        Assert.Equal("Open", result.Properties!["status"].GetString());
    }

    [Fact]
    public void ToJsonElement_ClonesJsonElement()
    {
        var element = JsonSerializer.SerializeToElement(new { a = 1 });
        var clone = RequestConverters.ToJsonElement((object)element);
        Assert.Equal(1, clone.GetProperty("a").GetInt32());
    }

    [Fact]
    public void ToJsonElement_ClonesJsonDocument()
    {
        using var doc = JsonDocument.Parse("""{"a":1}""");
        var clone = RequestConverters.ToJsonElement((object)doc);
        Assert.Equal(1, clone.GetProperty("a").GetInt32());
    }

    [Fact]
    public void ToJsonDocument_CollectionsEnvelope_RoundTrips()
    {
        IReadOnlyList<OgcCollection> collections =
        [
            new OgcCollection { Id = "c1", Title = "Collection 1" },
        ];

        using var doc = RequestConverters.ToJsonDocument(collections);

        Assert.Equal("c1", doc.RootElement.GetProperty("collections")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void ToJsonDocument_FeatureCollection_RoundTrips()
    {
        var collection = new OgcFeatureCollection
        {
            Type = "FeatureCollection",
            Features = [new OgcFeature { Type = "Feature" }],
        };

        using var doc = RequestConverters.ToJsonDocument(collection);

        Assert.Equal("FeatureCollection", doc.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ToJsonDocument_Feature_RoundTrips()
    {
        var feature = new OgcFeature
        {
            Type = "Feature",
            Properties = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Alpha"),
            },
        };

        using var doc = RequestConverters.ToJsonDocument(feature);

        Assert.Equal("Alpha", doc.RootElement.GetProperty("properties").GetProperty("name").GetString());
    }
}

