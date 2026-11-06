// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

internal static class AdminCatalogQuery
{
    internal const int DefaultPage = 1;
    internal const int DefaultPageSize = 100;
    internal const int MaxPageSize = 100;
    internal const int MaxNameQueryLength = 200;

    internal static string ForServices(ServiceListQuery? query)
        => Format(
            query?.Page ?? DefaultPage,
            query?.PageSize ?? DefaultPageSize,
            query?.NameQuery,
            ("folderId", TrimmedOrNull(query?.FolderId)));

    internal static string ForLayers(LayerListQuery? query)
        => Format(
            query?.Page ?? DefaultPage,
            query?.PageSize ?? DefaultPageSize,
            query?.NameQuery,
            ("serviceName", TrimmedOrNull(query?.ServiceName)));

    private static string Format(int page, int pageSize, string? nameQuery, (string Key, string? Value) extra)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        var name = TrimmedOrNull(nameQuery);
        if (name is { Length: > MaxNameQueryLength })
            throw new ArgumentException("Name query is longer than 200 characters.", nameof(nameQuery));

        return AdminQueryString.Format(
            ("page", page.ToString(CultureInfo.InvariantCulture)),
            ("pageSize", pageSize.ToString(CultureInfo.InvariantCulture)),
            ("q", name),
            extra);
    }

    private static string? TrimmedOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
