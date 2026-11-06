// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class AdminCatalogListTests
{
    [Fact]
    public async Task ListServicesPageAsync_ReadsOnePageWithoutRequestingTheRest()
    {
        var calls = 0;
        var client = TestHelpers.CreateClient(req =>
        {
            calls++;
            var query = req.RequestUri!.Query;
            Assert.Contains("page=2", query, StringComparison.Ordinal);
            Assert.Contains("pageSize=25", query, StringComparison.Ordinal);
            Assert.Contains("q=parks", query, StringComparison.Ordinal);
            Assert.Contains("folderId=root", query, StringComparison.Ordinal);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                items = new[]
                {
                    new { serviceName = "parks", description = "Parks", layerCount = 40, enabledProtocols = new[] { "FeatureServer" } }
                },
                page = 2,
                pageSize = 25,
                total = 2000,
                layerTotal = 40
            }));
        });

        var page = await client.ListServicesPageAsync(new ServiceListQuery
        {
            Page = 2,
            PageSize = 25,
            NameQuery = " parks ",
            FolderId = "root"
        });

        Assert.Equal(1, calls);
        Assert.Equal(2000, page.Total);
        Assert.Equal(40, page.LayerTotal);
        Assert.Equal("parks", Assert.Single(page.Items).ServiceName);
    }

    [Fact]
    public async Task ListServicesAsync_WalksPagesUntilTheTotalIsRead()
    {
        var calls = 0;
        var client = TestHelpers.CreateClient(req =>
        {
            calls++;
            var pageNumber = req.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal) ? 2 : 1;
            var count = pageNumber == 1 ? 100 : 1;
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                items = Enumerable.Range(0, count).Select(index => new
                {
                    serviceName = $"svc-{pageNumber}-{index}",
                    layerCount = 1,
                    enabledProtocols = Array.Empty<string>()
                }).ToArray(),
                page = pageNumber,
                pageSize = 100,
                total = 101,
                layerTotal = 101
            }));
        });

        var services = await client.ListServicesAsync();

        Assert.Equal(2, calls);
        Assert.Equal(101, services.Count);
        Assert.Equal("svc-1-0", services[0].ServiceName);
        Assert.Equal("svc-2-0", services[100].ServiceName);
    }

    [Fact]
    public async Task ListLayersPageAsync_SendsTheServiceAndNameQuery()
    {
        const string connectionId = "11111111-1111-1111-1111-111111111111";
        var calls = 0;
        var client = TestHelpers.CreateClient(req =>
        {
            calls++;
            var query = req.RequestUri!.Query;
            Assert.Contains("page=1", query, StringComparison.Ordinal);
            Assert.Contains("pageSize=10", query, StringComparison.Ordinal);
            Assert.Contains("q=roads", query, StringComparison.Ordinal);
            Assert.Contains("serviceName=transport", query, StringComparison.Ordinal);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                items = new[] { new { layerId = 7, layerName = "roads", serviceName = "transport" } },
                page = 1,
                pageSize = 10,
                total = 1
            }));
        });

        var page = await client.ListLayersPageAsync(connectionId, new LayerListQuery
        {
            Page = 1,
            PageSize = 10,
            NameQuery = "roads",
            ServiceName = "transport"
        });

        Assert.Equal(1, calls);
        Assert.Equal(1, page.Total);
        Assert.Equal(7, Assert.Single(page.Items).LayerId);
    }

    [Fact]
    public async Task ListServicesPageAsync_RejectsAPageSizeAboveTheCapBeforeHttp()
    {
        var calls = 0;
        var client = TestHelpers.CreateClient(_ =>
        {
            calls++;
            return Task.FromResult(TestHelpers.CreateJsonResponse(TestHelpers.EmptyServicePage()));
        });

        var error = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ListServicesPageAsync(new ServiceListQuery { PageSize = 101 }));

        Assert.Equal("pageSize", error.ParamName);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ListLayersPageAsync_RejectsANameQueryLongerThan200CharactersBeforeHttp()
    {
        var client = TestHelpers.CreateClient(_ =>
            throw new InvalidOperationException("The name query is rejected before the request."));

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListLayersPageAsync(
                "11111111-1111-1111-1111-111111111111",
                new LayerListQuery { NameQuery = new string('a', 201) }));

        Assert.Equal("nameQuery", error.ParamName);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("short")]
    [InlineData("full")]
    public async Task ListServicesAsync_StopsWithoutReadingAnotherPage(string stop)
    {
        var calls = 0;
        var count = stop switch
        {
            "full" => 100,
            "short" => 1,
            "empty" => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(stop), stop, "Unknown walk stop.")
        };
        var client = TestHelpers.CreateClient(_ =>
        {
            calls++;
            if (calls > 1)
                throw new InvalidOperationException("The walk requested another page.");

            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                items = Enumerable.Range(0, count).Select(index => new
                {
                    serviceName = $"svc-{index}",
                    layerCount = 1,
                    enabledProtocols = Array.Empty<string>()
                }).ToArray(),
                page = 1,
                pageSize = 100,
                total = stop == "full" ? 100 : 50,
                layerTotal = count
            }));
        });

        var services = await client.ListServicesAsync();

        Assert.Equal(1, calls);
        Assert.Equal(count, services.Count);
    }

    [Fact]
    public async Task ListLayersAsync_ReadsTheNextPageAndKeepsTheServiceName()
    {
        const string connectionId = "11111111-1111-1111-1111-111111111111";
        var calls = 0;
        var client = TestHelpers.CreateClient(req =>
        {
            calls++;
            var query = req.RequestUri!.Query;
            Assert.Contains("serviceName=roads", query, StringComparison.Ordinal);
            Assert.Contains(calls == 1 ? "page=1" : "page=2", query, StringComparison.Ordinal);
            var count = calls == 1 ? 100 : 1;
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                items = Enumerable.Range(0, count).Select(index => new
                {
                    layerId = index + 1,
                    layerName = $"layer-{calls}-{index}",
                    serviceName = "roads"
                }).ToArray(),
                page = calls,
                pageSize = 100,
                total = 101
            }));
        });

        var layers = await client.ListLayersAsync(connectionId, serviceName: "roads");

        Assert.Equal(2, calls);
        Assert.Equal(101, layers.Count);
        Assert.Equal("layer-2-0", layers[100].LayerName);
    }
}
