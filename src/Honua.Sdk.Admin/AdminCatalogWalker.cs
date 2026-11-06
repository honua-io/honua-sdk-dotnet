// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin;

internal static class AdminCatalogWalker
{
    internal readonly record struct Slice<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

    internal static async Task<IReadOnlyList<T>> ReadAllAsync<T>(
        Func<int, CancellationToken, Task<Slice<T>>> readPage,
        CancellationToken cancellationToken)
    {
        var collected = new List<T>();
        var pageNumber = AdminCatalogQuery.DefaultPage;
        while (true)
        {
            var page = await readPage(pageNumber, cancellationToken).ConfigureAwait(false);
            if (page.Items.Count > 0)
                collected.AddRange(page.Items);
            if (IsLast(page, collected.Count))
                return collected;
            pageNumber = page.Page + 1;
        }
    }

    private static bool IsLast<T>(Slice<T> page, int collected) =>
        page.Items.Count == 0 || collected >= page.Total || page.Items.Count < page.PageSize;
}
