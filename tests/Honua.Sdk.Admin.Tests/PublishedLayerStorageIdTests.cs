// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

/// <summary>
/// The server names the published layer id <c>storageLayerId</c> on the wire; older servers sent <c>layerId</c>.
/// </summary>
public sealed class PublishedLayerStorageIdTests
{
    private const string ConnectionId = "11111111-1111-1111-1111-111111111111";

    private static object ServerLayerSummary(int storageLayerId) => new
    {
        storageLayerId,
        layerName = "maui-parcels",
        schema = "public",
        table = "maui_parcels",
        geometryType = "Polygon",
        srid = 4326,
        primaryKey = "gid",
        fieldCount = 5,
        enabled = true,
        serviceName = "default",
    };

    [Fact]
    public async Task PublishLayerAsync_ReadsStorageLayerId()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateJsonResponse(ServerLayerSummary(7), HttpStatusCode.Created)));

        var result = await client.PublishLayerAsync(ConnectionId, new PublishLayerRequest
        {
            Schema = "public",
            Table = "maui_parcels",
            LayerName = "maui-parcels",
        });

        Assert.Equal(7, result.LayerId);
        Assert.Equal(7, result.StorageLayerId);
    }

    [Fact]
    public async Task SetLayerEnabledAsync_ReadsStorageLayerId()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateJsonResponse(ServerLayerSummary(5))));

        var result = await client.SetLayerEnabledAsync(ConnectionId, 5, true);

        Assert.Equal(5, result.LayerId);
    }

    [Fact]
    public async Task ListLayersAsync_ReadsStorageLayerIdFromPagedItems()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(TestHelpers.CreateJsonResponse(new
        {
            items = new[] { ServerLayerSummary(3), ServerLayerSummary(4) },
            page = 1,
            pageSize = 100,
            total = 2,
        })));

        var result = await client.ListLayersAsync(ConnectionId);

        Assert.Equal(new[] { 3, 4 }, result.Select(layer => layer.LayerId));
    }

    [Fact]
    public async Task SetServiceLayersEnabledAsync_ReadsStorageLayerId()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(
            TestHelpers.CreateJsonResponse(new[] { ServerLayerSummary(1), ServerLayerSummary(2) })));

        var result = await client.SetServiceLayersEnabledAsync(ConnectionId, enabled: true);

        Assert.Equal(new[] { 1, 2 }, result.Select(layer => layer.LayerId));
    }

    [Fact]
    public async Task LegacyLayerIdReplyStillReads()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(TestHelpers.CreateJsonResponse(
            new { layerId = 9, layerName = "legacy" },
            HttpStatusCode.Created)));

        var result = await client.PublishLayerAsync(ConnectionId, new PublishLayerRequest
        {
            Schema = "public",
            Table = "legacy",
            LayerName = "legacy",
        });

        Assert.Equal(9, result.LayerId);
        Assert.Equal(9, result.StorageLayerId);
    }
}
