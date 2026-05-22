// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>
/// Client contract for the server-side replica sync API used to create replicas,
/// extract feature changes, synchronize, and unregister replicas.
/// </summary>
public interface IReplicaSyncClient
{
    /// <summary>
    /// Creates a new server-side replica for the specified service.
    /// </summary>
    /// <param name="serviceId">The feature service identifier.</param>
    /// <param name="replicaName">A name for the new replica.</param>
    /// <param name="layerIds">Optional layer IDs to include. When <see langword="null"/>, all layers are included.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The created replica ID and initial server generation number.</returns>
    Task<CreateReplicaResult> CreateReplicaAsync(
        string serviceId,
        string replicaName,
        IReadOnlyList<int>? layerIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts feature changes (adds, updates, deletes) from the server since the last synchronization.
    /// </summary>
    /// <param name="serviceId">The feature service identifier.</param>
    /// <param name="replicaId">The replica ID obtained from <see cref="CreateReplicaAsync"/>.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>Layer-level change sets and the current server generation number.</returns>
    Task<ExtractChangesResult> ExtractChangesAsync(
        string serviceId,
        string replicaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges received changes and advances the replica's server generation number.
    /// </summary>
    /// <param name="serviceId">The feature service identifier.</param>
    /// <param name="replicaId">The replica ID.</param>
    /// <param name="syncDirection">Sync direction (for example, <c>"download"</c>).</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    /// <returns>The updated replica ID and server generation number.</returns>
    Task<SynchronizeResult> SynchronizeReplicaAsync(
        string serviceId,
        string replicaId,
        string syncDirection = "download",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a replica from the server, freeing server-side resources.
    /// </summary>
    /// <param name="serviceId">The feature service identifier.</param>
    /// <param name="replicaId">The replica ID to unregister.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
    Task UnRegisterReplicaAsync(
        string serviceId,
        string replicaId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of creating a new server-side replica.
/// </summary>
/// <param name="ReplicaId">The unique identifier assigned to the replica by the server.</param>
/// <param name="ServerGen">The initial server generation number.</param>
public sealed record CreateReplicaResult(string ReplicaId, long ServerGen);

/// <summary>
/// Result of synchronizing a replica.
/// </summary>
/// <param name="ReplicaId">The replica identifier.</param>
/// <param name="ServerGen">The updated server generation number after synchronization.</param>
public sealed record SynchronizeResult(string ReplicaId, long ServerGen);

/// <summary>
/// Contains layer-level change sets extracted from the server.
/// </summary>
public sealed class ExtractChangesResult
{
    /// <summary>
    /// Per-layer change sets containing added, updated, and deleted features.
    /// </summary>
    public required IReadOnlyList<LayerChangeSet> LayerChanges { get; init; }

    /// <summary>
    /// The server generation number at the time changes were extracted.
    /// </summary>
    public long ServerGen { get; init; }
}

/// <summary>
/// Feature changes for a single layer within an <see cref="ExtractChangesResult"/>.
/// </summary>
public sealed class LayerChangeSet
{
    /// <summary>
    /// The numeric layer ID.
    /// </summary>
    public int LayerId { get; init; }

    /// <summary>
    /// JSON representations of newly added features, or <see langword="null"/> if none.
    /// </summary>
    public IReadOnlyList<string>? AddFeaturesJson { get; init; }

    /// <summary>
    /// JSON representations of updated features, or <see langword="null"/> if none.
    /// </summary>
    public IReadOnlyList<string>? UpdateFeaturesJson { get; init; }

    /// <summary>
    /// Object IDs of deleted features, or <see langword="null"/> if none.
    /// </summary>
    public IReadOnlyList<long>? DeleteIds { get; init; }
}

/// <summary>
/// Exception thrown when a replica sync request fails, including server-side error envelopes
/// returned with an HTTP success status.
/// </summary>
public sealed class ReplicaSyncException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplicaSyncException"/> class.
    /// </summary>
    public ReplicaSyncException()
        : base("Replica sync request failed.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplicaSyncException"/> class.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    public ReplicaSyncException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplicaSyncException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="innerException">The inner exception that caused this exception.</param>
    public ReplicaSyncException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplicaSyncException"/> class with HTTP and server error details.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="statusCode">The HTTP status code returned by the server, if available.</param>
    /// <param name="serverErrorCode">The server-reported error code, if available.</param>
    public ReplicaSyncException(string message, HttpStatusCode? statusCode, int? serverErrorCode)
        : base(message)
    {
        StatusCode = statusCode;
        ServerErrorCode = serverErrorCode;
    }

    /// <summary>
    /// The HTTP status code returned by the server, if available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The server-reported error code from a GeoServices-style error envelope, if available.
    /// </summary>
    public int? ServerErrorCode { get; }
}
