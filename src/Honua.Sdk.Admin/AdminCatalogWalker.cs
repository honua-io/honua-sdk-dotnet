// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin;

internal static class AdminCatalogWalker
{
    internal const int DefaultMaxPages = 1000;

    internal readonly record struct Slice<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

    internal static async Task<IReadOnlyList<T>> ReadAllAsync<T>(
        Func<int, CancellationToken, Task<Slice<T>>> readPage,
        CancellationToken cancellationToken,
        int maxPages = DefaultMaxPages)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxPages, 1);

        var collected = new List<T>();
        var pageNumber = AdminCatalogQuery.DefaultPage;
        var pagesRead = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pagesRead >= maxPages)
            {
                throw new InvalidOperationException(
                    $"Admin catalog pagination safety limit reached ({maxPages} pages).");
            }

            var page = await readPage(pageNumber, cancellationToken).ConfigureAwait(false);
            pagesRead++;
            if (page.Items.Count > 0)
                collected.AddRange(page.Items);
            if (IsLast(page, collected.Count))
                return collected;

            var nextPage = page.Page + 1;
            if (nextPage <= pageNumber)
            {
                throw new InvalidOperationException(
                    $"Admin catalog pagination did not advance (requested page {pageNumber}, server reported page {page.Page}).");
            }

            pageNumber = nextPage;
        }
    }

    private static bool IsLast<T>(Slice<T> page, int collected) =>
        page.Items.Count == 0 || page.PageSize <= 0 || collected >= page.Total || page.Items.Count < page.PageSize;
}
