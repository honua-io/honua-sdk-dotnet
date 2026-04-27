// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Wfs.Models;
using Honua.Sdk.Wfs.Tests.Fixtures;

namespace Honua.Sdk.Wfs.Tests;

public sealed class PaginationTests
{
    [Fact]
    public async Task AutoPage_MultiplePages_YieldsAllFeatures()
    {
        var page1 = """
            {
                "type": "FeatureCollection",
                "numberMatched": 3,
                "numberReturned": 2,
                "features": [
                    { "type": "Feature", "id": "1", "geometry": null, "properties": { "name": "a" } },
                    { "type": "Feature", "id": "2", "geometry": null, "properties": { "name": "b" } }
                ]
            }
            """;

        var page2 = """
            {
                "type": "FeatureCollection",
                "numberMatched": 3,
                "numberReturned": 1,
                "features": [
                    { "type": "Feature", "id": "3", "geometry": null, "properties": { "name": "c" } }
                ]
            }
            """;

        var callCount = 0;
        var client = TestHelpers.CreateClient(req =>
        {
            callCount++;
            var query = req.RequestUri!.Query;

            if (query.Contains("STARTINDEX=0") || !query.Contains("STARTINDEX"))
            {
                return Task.FromResult(TestHelpers.CreateGeoJsonResponse(page1));
            }

            Assert.Contains("STARTINDEX=2", query);
            return Task.FromResult(TestHelpers.CreateGeoJsonResponse(page2));
        });

        var features = new List<WfsFeature>();
        await foreach (var feature in client.GetFeaturesAsyncEnumerable(
            new GetFeaturesRequest { TypeNames = "parcels", StartIndex = 0 }))
        {
            features.Add(feature);
        }

        Assert.Equal(3, features.Count);
        Assert.Equal("1", features[0].Id);
        Assert.Equal("2", features[1].Id);
        Assert.Equal("3", features[2].Id);
    }

    [Fact]
    public async Task AutoPage_EmptyFirstPage_YieldsNothing()
    {
        var emptyPage = """
            {
                "type": "FeatureCollection",
                "numberMatched": 0,
                "numberReturned": 0,
                "features": []
            }
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(emptyPage)));

        var features = new List<WfsFeature>();
        await foreach (var feature in client.GetFeaturesAsyncEnumerable(
            new GetFeaturesRequest { TypeNames = "parcels" }))
        {
            features.Add(feature);
        }

        Assert.Empty(features);
    }

    [Fact]
    public async Task AutoPage_NumberMatchedUnknown_StopsOnEmptyPage()
    {
        var page1 = """
            {
                "type": "FeatureCollection",
                "numberMatched": "unknown",
                "numberReturned": 1,
                "features": [
                    { "type": "Feature", "id": "1", "geometry": null, "properties": {} }
                ]
            }
            """;

        var page2 = """
            {
                "type": "FeatureCollection",
                "numberMatched": "unknown",
                "numberReturned": 0,
                "features": []
            }
            """;

        var requestIndex = 0;
        var client = TestHelpers.CreateClient(_ =>
        {
            var json = requestIndex++ == 0 ? page1 : page2;
            return Task.FromResult(TestHelpers.CreateGeoJsonResponse(json));
        });

        var features = new List<WfsFeature>();
        await foreach (var feature in client.GetFeaturesAsyncEnumerable(
            new GetFeaturesRequest { TypeNames = "parcels", StartIndex = 0 }))
        {
            features.Add(feature);
        }

        Assert.Single(features);
    }

    [Fact]
    public void HasMoreResults_AllReturned_ReturnsFalse()
    {
        var collection = new WfsFeatureCollection
        {
            NumberMatched = 5,
            NumberReturned = 5,
            Features = Enumerable.Range(1, 5)
                .Select(i => new WfsFeature { Id = i.ToString() })
                .ToList(),
        };

        Assert.False(collection.HasMoreResults);
    }

    [Fact]
    public void HasMoreResults_PartialReturn_ReturnsTrue()
    {
        var collection = new WfsFeatureCollection
        {
            NumberMatched = 100,
            NumberReturned = 10,
        };

        Assert.True(collection.HasMoreResults);
    }

    [Fact]
    public void HasMoreResults_UnknownTotal_ReturnsTrue()
    {
        var collection = new WfsFeatureCollection
        {
            NumberMatched = null,
            NumberReturned = 10,
        };

        Assert.True(collection.HasMoreResults);
    }

    [Fact]
    public void HasMoreResults_EmptyPage_ReturnsFalse()
    {
        var collection = new WfsFeatureCollection
        {
            NumberMatched = 100,
            NumberReturned = 0,
        };

        Assert.False(collection.HasMoreResults);
    }

    [Fact]
    public async Task AutoPage_Cancellation_StopsIteration()
    {
        var pageJson = """
            {
                "type": "FeatureCollection",
                "numberMatched": 1000,
                "numberReturned": 1,
                "features": [
                    { "type": "Feature", "id": "1", "geometry": null, "properties": {} }
                ]
            }
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(pageJson)));

        using var cts = new CancellationTokenSource();
        var count = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var feature in client.GetFeaturesAsyncEnumerable(
                new GetFeaturesRequest { TypeNames = "parcels", StartIndex = 0 }, cts.Token))
            {
                count++;
                if (count >= 2)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.True(count >= 1);
    }

    [Fact]
    public async Task AutoPage_MaxPagesExceeded_ThrowsInvalidOperationException()
    {
        var pageJson = """
            {
                "type": "FeatureCollection",
                "numberMatched": 10000,
                "numberReturned": 1,
                "features": [
                    { "type": "Feature", "id": "1", "geometry": null, "properties": {} }
                ]
            }
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(pageJson)));

        var count = 0;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var feature in client.GetFeaturesAsyncEnumerable(
                new GetFeaturesRequest { TypeNames = "parcels", StartIndex = 0 }))
            {
                count++;
            }
        });

        Assert.Equal(100, count);
        Assert.Contains("safety limit", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
