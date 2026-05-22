// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Source-oriented runtime handle over one protocol-backed feature provider.
/// </summary>
public interface IHonuaSource
{
    /// <summary>Serializable source descriptor.</summary>
    SourceDescriptor Descriptor { get; }

    /// <summary>Canonical capabilities supported by this source handle.</summary>
    IReadOnlyList<string> Capabilities { get; }

    /// <summary>
    /// Retrieves this source descriptor enriched with provider-neutral schema and discovered capabilities when available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current descriptor, or a provider-enriched descriptor when the backing client supports discovery.</returns>
    Task<SourceDescriptor> GetDescriptorAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Descriptor);

    /// <summary>Executes a feature query and returns one page.</summary>
    /// <param name="query">Source-oriented query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A provider-neutral result page.</returns>
    Task<FeatureQueryResult> QueryAsync(SourceQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Executes a feature query and returns provider-neutral result pages.</summary>
    /// <param name="query">Source-oriented query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral result pages.</returns>
    IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(SourceQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Drains feature query pages into one result envelope.</summary>
    /// <param name="query">Source-oriented query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A provider-neutral result with all returned records.</returns>
    Task<FeatureQueryResult> QueryAllAsync(SourceQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Queries feature/object identifiers for records matching a request.</summary>
    /// <param name="query">Source-oriented query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Feature or object identifiers represented as strings.</returns>
    Task<IReadOnlyList<string>> QueryObjectIdsAsync(SourceQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>Applies add, update, and delete feature edits against this source.</summary>
    /// <param name="request">Edit request. The source descriptor supplies provider-specific source identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral edit response.</returns>
    Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the native protocol client when the requested protocol matches this source.
    /// </summary>
    /// <param name="protocolId">Canonical protocol identifier or alias.</param>
    /// <returns>The native client object, or <see langword="null"/> when unavailable.</returns>
    object? Protocol(string protocolId);

    /// <summary>
    /// Returns the typed native protocol client when the requested protocol and client type match.
    /// </summary>
    /// <typeparam name="TClient">Expected native client type.</typeparam>
    /// <param name="protocolId">Canonical protocol identifier or alias. Defaults to this source's protocol.</param>
    /// <returns>The typed native client, or <see langword="null"/> when unavailable.</returns>
    TClient? Protocol<TClient>(string? protocolId = null)
        where TClient : class;
}
