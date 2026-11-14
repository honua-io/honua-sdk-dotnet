// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>Administrative API-key management.</summary>
public interface IHonuaAdminApiKeysClient
{
    /// <summary>Lists administrative API-key metadata.</summary>
    Task<IReadOnlyList<AdminApiKeyResponse>> ListApiKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates an API key and returns its secret once.</summary>
    Task<AdminApiKeySecretResponse> CreateApiKeyAsync(CreateAdminApiKeyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Revokes an API key.</summary>
    Task<AdminApiKeyResponse> RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default);
}
