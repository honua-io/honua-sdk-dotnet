// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Admin;

/// <summary>
/// Server configuration documentation retrieval.
/// </summary>
public interface IHonuaAdminConfigClient
{
    /// <summary>
    /// Gets the server configuration documentation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configuration as a JSON element.</returns>
    Task<JsonElement> GetConfigAsync(CancellationToken cancellationToken = default);
}
