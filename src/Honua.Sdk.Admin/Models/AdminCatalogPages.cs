// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>Selects one page of the admin service list.</summary>
public sealed class ServiceListQuery
{
    /// <summary>One-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of services on the page, from 1 to 100.</summary>
    public int PageSize { get; init; } = 100;

    /// <summary>Case-insensitive name match. Blank is omitted.</summary>
    public string? NameQuery { get; init; }

    /// <summary>Folder id. Blank omits the filter, and root selects services with no folder.</summary>
    public string? FolderId { get; init; }
}

/// <summary>One page of admin services and the count of every match.</summary>
public sealed class ServiceListPage
{
    /// <summary>Services on this page.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<ServiceSummary> Items { get; init; } = [];

    /// <summary>One-based page number returned by the server.</summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>Page size returned by the server.</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    /// <summary>Matching services, including those on other pages.</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>Sum of layer counts for every matching service.</summary>
    [JsonPropertyName("layerTotal")]
    public int LayerTotal { get; init; }
}

/// <summary>Selects one page of layers published on a connection.</summary>
public sealed class LayerListQuery
{
    /// <summary>One-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Number of layers on the page, from 1 to 100.</summary>
    public int PageSize { get; init; } = 100;

    /// <summary>Case-insensitive layer name match. Blank is omitted.</summary>
    public string? NameQuery { get; init; }

    /// <summary>Restricts the page to one service. Blank omits the filter.</summary>
    public string? ServiceName { get; init; }
}

/// <summary>One page of published layers and the count of every match.</summary>
public sealed class PublishedLayerPage
{
    /// <summary>Layers on this page.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<PublishedLayerSummary> Items { get; init; } = [];

    /// <summary>One-based page number returned by the server.</summary>
    [JsonPropertyName("page")]
    public int Page { get; init; }

    /// <summary>Page size returned by the server.</summary>
    [JsonPropertyName("pageSize")]
    public int PageSize { get; init; }

    /// <summary>Matching layers, including those on other pages.</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }
}
