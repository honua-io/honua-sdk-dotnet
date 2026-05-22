// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Secure database connection administration plus encryption-key validation
/// and rotation.
/// </summary>
public interface IHonuaAdminConnectionsClient
{
    /// <summary>
    /// Lists all secure database connections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of connection summaries.</returns>
    Task<IReadOnlyList<SecureConnectionSummary>> ListConnectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed information about a secure database connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The connection details.</returns>
    Task<SecureConnectionDetail> GetConnectionAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new secure database connection.
    /// </summary>
    /// <param name="request">The connection creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created connection summary.</returns>
    Task<SecureConnectionSummary> CreateConnectionAsync(CreateSecureConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a draft connection before saving.
    /// </summary>
    /// <param name="request">The connection details to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<ConnectionTestResult> TestDraftConnectionAsync(CreateSecureConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing secure database connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="request">The connection update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated connection summary.</returns>
    Task<SecureConnectionSummary> UpdateConnectionAsync(string id, UpdateSecureConnectionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the health of an existing connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The test result.</returns>
    Task<ConnectionTestResult> TestConnectionAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a secure database connection.
    /// </summary>
    /// <param name="id">The connection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteConnectionAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the encryption service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    Task<EncryptionValidationResult> ValidateEncryptionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rotates the encryption key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The key rotation result.</returns>
    Task<KeyRotationResult> RotateEncryptionKeyAsync(CancellationToken cancellationToken = default);
}
