// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class AdminCatalogQueryTests
{
    private const string ConnectionId = "11111111-1111-1111-1111-111111111111";

    [Theory]
    [InlineData(true, 0, 100, "page")]
    [InlineData(true, 1, 0, "pageSize")]
    [InlineData(false, 0, 100, "page")]
    [InlineData(false, 1, 0, "pageSize")]
    public async Task PageReadRejectsAnOutOfRangeQueryBeforeHttp(bool services, int page, int pageSize, string parameter)
    {
        var error = await RejectOutOfRange(services, page, pageSize);
        Assert.Equal(parameter, error.ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PageReadRejectsANameLongerThan200CharactersBeforeHttp(bool services)
    {
        var error = await RejectLongName(services);
        Assert.Equal("nameQuery", error.ParamName);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(true, 100)]
    [InlineData(false, 1)]
    [InlineData(false, 100)]
    public async Task PageReadReturnsOneFullPageAndEncodesFilters(bool services, int pageSize)
    {
        var seen = await ReadOnePage(services, pageSize, "a b&c", "a/b");
        var filter = services ? "folderId=a%2Fb" : "serviceName=a%2Fb";
        AssertPairs(seen, "page=1", "pageSize=" + pageSize.ToString(CultureInfo.InvariantCulture), "q=a%20b%26c", filter);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PageReadOmitsBlankNameAndFilter(bool services)
    {
        var seen = await ReadOnePage(services, 100, "   ", " \t ");
        AssertPairs(seen, "page=1", "pageSize=100");
        AssertAbsent(seen, "q", "folderId", "serviceName");
    }

    [Fact]
    public async Task LegacyServicesClientRejectsAPageRead()
    {
        IHonuaAdminServicesClient client = new LegacyServicesClient();
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => client.ListServicesPageAsync());
        Assert.Contains("HonuaAdminClient", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LegacyLayersClientRejectsAPageRead()
    {
        IHonuaAdminLayersClient client = new LegacyLayersClient();
        var error = await Assert.ThrowsAsync<NotSupportedException>(() => client.ListLayersPageAsync(ConnectionId));
        Assert.Contains("HonuaAdminClient", error.Message, StringComparison.Ordinal);
    }

    private static async Task<string> ReadOnePage(bool services, int pageSize, string? name, string? filter)
    {
        var calls = 0;
        string? seen = null;
        var client = TestHelpers.CreateClient(request =>
        {
            calls++;
            seen = request.RequestUri!.Query;
            return Task.FromResult(services ? ServiceBody(pageSize) : LayerBody(pageSize));
        });

        if (services)
            await ReadServices(client, pageSize, name, filter);
        else
            await ReadLayers(client, pageSize, name, filter);

        Assert.Equal(1, calls);
        return seen ?? string.Empty;
    }

    private static async Task ReadServices(HonuaAdminClient client, int pageSize, string? name, string? filter)
    {
        IHonuaAdminServicesClient api = client;
        var body = await api.ListServicesPageAsync(new ServiceListQuery
        {
            PageSize = pageSize,
            NameQuery = name,
            FolderId = filter
        });
        Assert.Equal(1, body.Page);
        Assert.Equal(pageSize, body.PageSize);
        Assert.Equal(pageSize, body.Items.Count);
        Assert.Equal(pageSize + 50, body.Total);
    }

    private static async Task ReadLayers(HonuaAdminClient client, int pageSize, string? name, string? filter)
    {
        IHonuaAdminLayersClient api = client;
        var body = await api.ListLayersPageAsync(ConnectionId, new LayerListQuery
        {
            PageSize = pageSize,
            NameQuery = name,
            ServiceName = filter
        });
        Assert.Equal(1, body.Page);
        Assert.Equal(pageSize, body.PageSize);
        Assert.Equal(pageSize, body.Items.Count);
        Assert.Equal(pageSize + 50, body.Total);
    }

    private static HttpResponseMessage ServiceBody(int pageSize)
        => TestHelpers.CreateJsonResponse(new
        {
            items = Enumerable.Range(0, pageSize).Select(index => new
            {
                serviceName = "svc-" + index.ToString(CultureInfo.InvariantCulture),
                layerCount = 1,
                enabledProtocols = Array.Empty<string>()
            }).ToArray(),
            page = 1,
            pageSize,
            total = pageSize + 50,
            layerTotal = 0
        });

    private static HttpResponseMessage LayerBody(int pageSize)
        => TestHelpers.CreateJsonResponse(new
        {
            items = Enumerable.Range(0, pageSize).Select(index => new
            {
                layerId = index + 1,
                layerName = "layer-" + index.ToString(CultureInfo.InvariantCulture),
                serviceName = "roads"
            }).ToArray(),
            page = 1,
            pageSize,
            total = pageSize + 50
        });

    private static Task<ArgumentOutOfRangeException> RejectOutOfRange(bool services, int page, int pageSize)
    {
        var client = RejectingClient();
        if (services)
        {
            return Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                client.ListServicesPageAsync(new ServiceListQuery { Page = page, PageSize = pageSize }));
        }

        return Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ListLayersPageAsync(ConnectionId, new LayerListQuery { Page = page, PageSize = pageSize }));
    }

    private static Task<ArgumentException> RejectLongName(bool services)
    {
        var client = RejectingClient();
        var name = new string('a', 201);
        if (services)
        {
            return Assert.ThrowsAsync<ArgumentException>(() =>
                client.ListServicesPageAsync(new ServiceListQuery { NameQuery = name }));
        }

        return Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListLayersPageAsync(ConnectionId, new LayerListQuery { NameQuery = name }));
    }

    private static HonuaAdminClient RejectingClient()
        => TestHelpers.CreateClient(_ =>
            throw new InvalidOperationException("The query is rejected before the request."));

    private static void AssertPairs(string query, params string[] expected)
    {
        var pairs = query.TrimStart('?').Split('&');
        foreach (var pair in expected)
            Assert.Contains(pair, pairs);
    }

    private static void AssertAbsent(string query, params string[] keys)
    {
        var pairs = query.TrimStart('?').Split('&');
        foreach (var key in keys)
            Assert.DoesNotContain(pairs, pair => pair.StartsWith(key + "=", StringComparison.Ordinal));
    }

    private static Task<T> Legacy<T>(params object?[] arguments)
    {
        _ = arguments.Length;
        throw new NotImplementedException();
    }

    private sealed class LegacyServicesClient : IHonuaAdminServicesClient
    {
        public Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken cancellationToken = default)
            => Legacy<IReadOnlyList<ServiceSummary>>(cancellationToken);

        public Task<ServiceSettingsResponse> GetServiceSettingsAsync(
            string serviceName, CancellationToken cancellationToken = default)
            => Legacy<ServiceSettingsResponse>(serviceName, cancellationToken);

        public Task<ServiceSettingsResponse> UpdateProtocolsAsync(
            string serviceName, IReadOnlyList<string> protocols, CancellationToken cancellationToken = default)
            => Legacy<ServiceSettingsResponse>(serviceName, protocols, cancellationToken);

        public Task<ServiceSettingsResponse> UpdateMapServerSettingsAsync(
            string serviceName, UpdateMapServerSettingsRequest request, CancellationToken cancellationToken = default)
            => Legacy<ServiceSettingsResponse>(serviceName, request, cancellationToken);

        public Task<ServiceSettingsResponse> UpdateAccessPolicyAsync(
            string serviceName, UpdateAccessPolicyRequest request, CancellationToken cancellationToken = default)
            => Legacy<ServiceSettingsResponse>(serviceName, request, cancellationToken);

        public Task<ServiceSettingsResponse> UpdateTimeInfoAsync(
            string serviceName, UpdateTimeInfoRequest request, CancellationToken cancellationToken = default)
            => Legacy<ServiceSettingsResponse>(serviceName, request, cancellationToken);

        public Task<LayerMetadataResponse> UpdateLayerMetadataAsync(
            string serviceName, int layerId, UpdateLayerMetadataRequest request, CancellationToken cancellationToken = default)
            => Legacy<LayerMetadataResponse>(serviceName, layerId, request, cancellationToken);
    }

    private sealed class LegacyLayersClient : IHonuaAdminLayersClient
    {
        public Task<IReadOnlyList<PublishedLayerSummary>> ListLayersAsync(
            string connectionId, string? serviceName = null, CancellationToken cancellationToken = default)
            => Legacy<IReadOnlyList<PublishedLayerSummary>>(connectionId, serviceName, cancellationToken);

        public Task<PublishedLayerSummary> PublishLayerAsync(
            string connectionId, PublishLayerRequest request, CancellationToken cancellationToken = default)
            => Legacy<PublishedLayerSummary>(connectionId, request, cancellationToken);

        public Task<PublishedLayerSummary> SetLayerEnabledAsync(
            string connectionId, int layerId, bool enabled, string? serviceName = null, CancellationToken cancellationToken = default)
            => Legacy<PublishedLayerSummary>(connectionId, layerId, enabled, serviceName, cancellationToken);

        public Task<IReadOnlyList<PublishedLayerSummary>> SetServiceLayersEnabledAsync(
            string connectionId, bool enabled, string? serviceName = null, CancellationToken cancellationToken = default)
            => Legacy<IReadOnlyList<PublishedLayerSummary>>(connectionId, enabled, serviceName, cancellationToken);

        public Task<TableDiscoveryResponse> DiscoverTablesAsync(
            string connectionId, CancellationToken cancellationToken = default)
            => Legacy<TableDiscoveryResponse>(connectionId, cancellationToken);
    }
}
