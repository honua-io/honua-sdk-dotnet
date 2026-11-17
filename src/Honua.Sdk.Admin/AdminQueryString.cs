// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin;

internal static class AdminQueryString
{
    internal static string Format(params ReadOnlySpan<(string Key, string? Value)> parameters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrEmpty(value))
                parts.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value));
        }

        return parts.Count > 0 ? "?" + string.Join("&", parts) : string.Empty;
    }
}
