// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

public sealed partial class HonuaAdminClient
{
    /// <summary>Lists administrative API-key metadata.</summary>
    public async Task<IReadOnlyList<AdminApiKeyResponse>> ListApiKeysAsync(CancellationToken cancellationToken = default)
        => await GetAsync($"{ApiPrefix}/api-keys/", HonuaAdminJsonContext.Default.ApiResponseAdminApiKeyResponseArray, cancellationToken).ConfigureAwait(false)
            ?? throw new HonuaAdminOperationException("Server returned no API-key list.", "ListApiKeys");

    /// <summary>Creates an API key and returns its secret once.</summary>
    public async Task<AdminApiKeySecretResponse> CreateApiKeyAsync(CreateAdminApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await PostAsync($"{ApiPrefix}/api-keys/", request, HonuaAdminJsonContext.Default.CreateAdminApiKeyRequest,
            HonuaAdminJsonContext.Default.ApiResponseAdminApiKeySecretResponse, cancellationToken).ConfigureAwait(false)
            ?? throw new HonuaAdminOperationException("Server returned no created API key.", "CreateApiKey");
    }

    /// <summary>Revokes an API key.</summary>
    public async Task<AdminApiKeyResponse> RevokeApiKeyAsync(Guid id, CancellationToken cancellationToken = default)
        => await PostAsync($"{ApiPrefix}/api-keys/{id:D}/revoke", HonuaAdminJsonContext.Default.ApiResponseAdminApiKeyResponse, cancellationToken).ConfigureAwait(false)
            ?? throw new HonuaAdminOperationException("Server returned no revoked API key.", "RevokeApiKey");
}
