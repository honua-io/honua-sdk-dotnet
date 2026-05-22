// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Migration toolkit helpers for admin clients.
/// </summary>
public static class HonuaAdminMigrationClientExtensions
{
    /// <summary>
    /// Scans a supported source environment and returns the migration source inventory artifact.
    /// </summary>
    /// <param name="client">The admin client.</param>
    /// <param name="request">The migration inventory scan request.</param>
    /// <param name="exportJson">When true, requests the server's JSON attachment form with <c>export=json</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The migration source inventory artifact returned by the server.</returns>
    public static Task<MigrationSourceInventoryArtifact> ScanMigrationSourceAsync(
        this IHonuaAdminClient client,
        MigrationInventoryScanRequest request,
        bool exportJson = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client is HonuaAdminClient honuaClient)
        {
            return honuaClient.ScanMigrationSourceAsync(request, exportJson, cancellationToken);
        }

        throw new NotSupportedException("This IHonuaAdminClient implementation does not support migration source scanning.");
    }
}
