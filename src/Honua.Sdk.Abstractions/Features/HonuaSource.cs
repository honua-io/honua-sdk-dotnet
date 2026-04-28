// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Default <see cref="IHonuaSource"/> implementation over existing query and edit clients.
/// </summary>
public sealed class HonuaSource : IHonuaSource
{
    private readonly IHonuaFeatureQueryClient _queryClient;
    private readonly IHonuaFeatureEditClient? _editClient;
    private readonly object? _nativeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSource"/> class.
    /// </summary>
    /// <param name="descriptor">Serializable source descriptor.</param>
    /// <param name="queryClient">Provider-neutral query client.</param>
    /// <param name="editClient">Optional provider-neutral edit client.</param>
    /// <param name="nativeClient">Optional native protocol client for <see cref="Protocol(string)"/>.</param>
    public HonuaSource(
        SourceDescriptor descriptor,
        IHonuaFeatureQueryClient queryClient,
        IHonuaFeatureEditClient? editClient = null,
        object? nativeClient = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(queryClient);

        Descriptor = descriptor;
        _queryClient = queryClient;
        _editClient = editClient;
        _nativeClient = nativeClient ?? queryClient;
        Capabilities = BuildCapabilities(descriptor, queryClient, editClient);
    }

    /// <inheritdoc />
    public SourceDescriptor Descriptor { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities { get; }

    /// <inheritdoc />
    public Task<FeatureQueryResult> QueryAsync(SourceQuery? query = null, CancellationToken ct = default)
    {
        EnsureCapability(FeatureCapabilities.Query);
        return _queryClient.QueryAsync(BuildQueryRequest(query), ct);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        SourceQuery? query = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        EnsureCapability(FeatureCapabilities.Stream);

        await foreach (var page in _queryClient.QueryPagesAsync(BuildQueryRequest(query), ct).ConfigureAwait(false))
        {
            yield return page;
        }
    }

    /// <inheritdoc />
    public async Task<FeatureQueryResult> QueryAllAsync(SourceQuery? query = null, CancellationToken ct = default)
    {
        EnsureCapability(FeatureCapabilities.Query);

        var limit = query?.Limit;
        var providerName = _queryClient.ProviderName;
        var features = new List<FeatureRecord>();
        long? numberMatched = null;
        string? objectIdFieldName = null;
        var sawPage = false;

        await foreach (var page in _queryClient.QueryPagesAsync(BuildQueryRequest(query), ct).ConfigureAwait(false))
        {
            sawPage = true;
            providerName = page.ProviderName;
            var remaining = limit.HasValue ? limit.Value - features.Count : int.MaxValue;
            if (remaining <= 0)
            {
                break;
            }

            features.AddRange(page.Features.Take(remaining));
            numberMatched ??= page.NumberMatched;
            objectIdFieldName ??= page.ObjectIdFieldName;

            if (limit.HasValue && features.Count >= limit.Value)
            {
                break;
            }
        }

        if (!sawPage)
        {
            return new FeatureQueryResult
            {
                ProviderName = providerName,
                Features = [],
                NumberReturned = 0
            };
        }

        return new FeatureQueryResult
        {
            ProviderName = providerName,
            Features = features,
            NumberMatched = numberMatched,
            NumberReturned = features.Count,
            ObjectIdFieldName = objectIdFieldName
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> QueryObjectIdsAsync(SourceQuery? query = null, CancellationToken ct = default)
    {
        EnsureCapability(FeatureCapabilities.QueryObjectIds);

        var objectIdQuery = query is null
            ? new SourceQuery { ReturnGeometry = false }
            : query with { ReturnGeometry = false };
        var request = BuildQueryRequest(objectIdQuery);
        var limit = query?.Limit;
        var idFieldName = Descriptor.Schema?.PrimaryKey;
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var page in _queryClient.QueryPagesAsync(request, ct).ConfigureAwait(false))
        {
            idFieldName ??= page.ObjectIdFieldName;
            foreach (var feature in page.Features)
            {
                if (ResolveFeatureId(feature, idFieldName) is { } id && seen.Add(id))
                {
                    ids.Add(id);
                    if (limit.HasValue && ids.Count >= limit.Value)
                    {
                        return ids;
                    }
                }
            }
        }

        return ids;
    }

    /// <inheritdoc />
    public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCapability(FeatureCapabilities.ApplyEdits);

        if (_editClient is null || !SupportsEditProtocol(Descriptor, _editClient))
        {
            throw new NotSupportedException(BuildUnsupportedMessage(FeatureCapabilities.ApplyEdits));
        }

        return _editClient.ApplyEditsAsync(request with { Source = Descriptor.ToFeatureSource() }, ct);
    }

    /// <inheritdoc />
    public object? Protocol(string protocolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolId);
        return MatchesProtocol(protocolId) ? _nativeClient : null;
    }

    /// <inheritdoc />
    public TClient? Protocol<TClient>(string? protocolId = null)
        where TClient : class
    {
        var native = Protocol(protocolId ?? Descriptor.Protocol);
        return native as TClient;
    }

    private static IReadOnlyList<string> BuildCapabilities(
        SourceDescriptor descriptor,
        IHonuaFeatureQueryClient queryClient,
        IHonuaFeatureEditClient? editClient)
    {
        var declared = descriptor.Capabilities.Count > 0
            ? descriptor.Capabilities
            : FeatureProtocolCapabilities.DefaultsFor(descriptor.Protocol);
        var capabilities = new HashSet<string>(declared, StringComparer.Ordinal);

        if (!SupportsQueryProtocol(descriptor, queryClient))
        {
            capabilities.Remove(FeatureCapabilities.Query);
            capabilities.Remove(FeatureCapabilities.QueryObjectIds);
            capabilities.Remove(FeatureCapabilities.Stream);
        }

        if (editClient?.EditCapabilities is { } editCapabilities &&
            SupportsEditProtocol(descriptor, editClient) &&
            (editCapabilities.SupportsAdds || editCapabilities.SupportsUpdates || editCapabilities.SupportsDeletes))
        {
            if (declared.Contains(FeatureCapabilities.ApplyEdits, StringComparer.Ordinal) || descriptor.Capabilities.Count == 0)
            {
                capabilities.Add(FeatureCapabilities.ApplyEdits);
            }
        }
        else
        {
            capabilities.Remove(FeatureCapabilities.ApplyEdits);
        }

        return FeatureCapabilities.All.Where(capabilities.Contains).ToList();
    }

    private static bool SupportsQueryProtocol(SourceDescriptor descriptor, IHonuaFeatureQueryClient queryClient)
        => FeatureProtocolIds.Matches(descriptor.Protocol, queryClient.ProviderName) ||
           FeatureProtocolIds.Matches(descriptor.CanonicalProtocol, queryClient.ProviderName);

    private static bool SupportsEditProtocol(SourceDescriptor descriptor, IHonuaFeatureEditClient editClient)
        => FeatureProtocolIds.Matches(descriptor.Protocol, editClient.ProviderName) ||
           FeatureProtocolIds.Matches(descriptor.CanonicalProtocol, editClient.ProviderName);

    private FeatureQueryRequest BuildQueryRequest(SourceQuery? query)
        => (query ?? new SourceQuery()).ToFeatureQueryRequest(Descriptor.ToFeatureSource());

    private void EnsureCapability(string capability)
    {
        if (FeatureCapabilities.Contains(Capabilities, capability))
        {
            return;
        }

        throw new NotSupportedException(BuildUnsupportedMessage(capability));
    }

    private string BuildUnsupportedMessage(string capability)
        => $"Source '{Descriptor.Id}' using protocol '{Descriptor.CanonicalProtocol}' does not support '{capability}'.";

    private bool MatchesProtocol(string protocolId)
        => FeatureProtocolIds.Matches(Descriptor.Protocol, protocolId) ||
           FeatureProtocolIds.Matches(_queryClient.ProviderName, protocolId);

    private static string? ResolveFeatureId(FeatureRecord feature, string? idFieldName)
    {
        if (!string.IsNullOrWhiteSpace(feature.Id))
        {
            return feature.Id;
        }

        if (!string.IsNullOrWhiteSpace(idFieldName) &&
            feature.Attributes.TryGetValue(idFieldName, out var value))
        {
            return JsonElementToString(value);
        }

        return null;
    }

    private static string? JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };
    }
}
