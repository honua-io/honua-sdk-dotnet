// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class CatalogServicePageTests
{
    [Fact]
    public async Task ListServicesAsync_ReadsASecondPageAfterAFullPage()
    {
        var queries = new List<string>();
        var layerReads = new List<string>();
        var client = PagedClient(queries, layerReads, "catalog", present: false);
        var services = await client.ListServicesAsync();

        Assert.Equal(101, services.Count);
        Assert.Equal(2, queries.Count);
        Assert.Empty(layerReads);
        AssertPages(queries, name: null);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task LookupSelectsTheExactNameFromTheSecondPage(bool overlength, bool present, bool layer)
    {
        var name = overlength ? new string('n', 201) : "Atlas";
        var queries = new List<string>();
        var layerReads = new List<string>();
        var client = PagedClient(queries, layerReads, name, present);
        var found = layer
            ? (await client.GetLayerAsync(name, 4))?.ServiceName
            : (await client.GetServiceAsync(name))?.Name;

        Assert.Equal(present ? name : null, found);
        Assert.Equal(2, queries.Count);
        Assert.Equal(present && layer ? 1 : 0, layerReads.Count);
        AssertPages(queries, overlength ? null : name);
    }

    private static HonuaCatalogClient PagedClient(
        List<string> queries, List<string> layerReads, string name, bool present)
    {
        var handler = new MockHttpHandler(request =>
            Task.FromResult(Respond(request, queries, layerReads, name, present)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        return new HonuaCatalogClient(http);
    }

    private static HttpResponseMessage Respond(
        HttpRequestMessage request,
        List<string> queries,
        List<string> layerReads,
        string name,
        bool present)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.Contains("/FeatureServer/", StringComparison.Ordinal))
        {
            layerReads.Add(path);
            return TestHelpers.CreateRawJsonResponse(new
            {
                id = 4,
                name = "roads",
                geometryType = "esriGeometryPoint"
            });
        }

        var query = request.RequestUri.Query;
        queries.Add(query);
        var pageNumber = query.Contains("page=2", StringComparison.Ordinal) ? 2 : 1;
        return TestHelpers.CreateJsonResponse(new
        {
            items = PageItems(pageNumber, name, present),
            page = pageNumber,
            pageSize = 100,
            total = 101,
            layerTotal = 0
        });
    }

    private static object[] PageItems(int pageNumber, string name, bool present)
    {
        if (pageNumber == 1)
            return Enumerable.Range(0, 100).Select(index => Item(Partial(name, index))).ToArray();

        return [Item(present ? name : Partial(name, 100))];
    }

    private static string Partial(string name, int index)
        => name + "-" + index.ToString(CultureInfo.InvariantCulture);

    private static object Item(string serviceName) => new
    {
        serviceName,
        layerCount = 0,
        enabledProtocols = Array.Empty<string>()
    };

    private static void AssertPages(IReadOnlyList<string> queries, string? name)
    {
        Assert.Equal(2, queries.Count);
        AssertQuery(queries[0], "page=1", name);
        AssertQuery(queries[1], "page=2", name);
    }

    private static void AssertQuery(string query, string page, string? name)
    {
        var pairs = query.TrimStart('?').Split('&');
        Assert.Contains(page, pairs);
        Assert.Contains("pageSize=100", pairs);
        if (name is null)
            Assert.DoesNotContain(pairs, pair => pair.StartsWith("q=", StringComparison.Ordinal));
        else
            Assert.Contains("q=" + Uri.EscapeDataString(name), pairs);
    }
}
