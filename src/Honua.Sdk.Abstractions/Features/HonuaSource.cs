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
    private readonly IHonuaFeatureDescriptorClient? _descriptorClient;
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
        : this(descriptor, queryClient, editClient, nativeClient, descriptorClient: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSource"/> class.
    /// </summary>
    /// <param name="descriptor">Serializable source descriptor.</param>
    /// <param name="queryClient">Provider-neutral query client.</param>
    /// <param name="editClient">Optional provider-neutral edit client.</param>
    /// <param name="nativeClient">Optional native protocol client for <see cref="Protocol(string)"/>.</param>
    /// <param name="descriptorClient">Optional provider-neutral descriptor discovery client.</param>
    public HonuaSource(
        SourceDescriptor descriptor,
        IHonuaFeatureQueryClient queryClient,
        IHonuaFeatureEditClient? editClient,
        object? nativeClient,
        IHonuaFeatureDescriptorClient? descriptorClient)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(queryClient);

        Descriptor = descriptor;
        _queryClient = queryClient;
        _editClient = editClient;
        _nativeClient = nativeClient ?? queryClient;
        _descriptorClient =
            descriptorClient ??
            queryClient as IHonuaFeatureDescriptorClient ??
            nativeClient as IHonuaFeatureDescriptorClient;
        Capabilities = BuildCapabilities(descriptor, queryClient, editClient);
    }

    /// <inheritdoc />
    public SourceDescriptor Descriptor { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities { get; }

    /// <inheritdoc />
    public Task<SourceDescriptor> GetDescriptorAsync(CancellationToken ct = default)
    {
        if (_descriptorClient is null || !SupportsDescriptorProtocol(Descriptor, _descriptorClient))
        {
            return Task.FromResult(Descriptor);
        }

        return _descriptorClient.GetDescriptorAsync(Descriptor, ct);
    }

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

        var useIdsOnlyMode =
            FeatureProtocolIds.Matches(Descriptor.Protocol, FeatureProtocolIds.Grpc) ||
            FeatureProtocolIds.Matches(Descriptor.Protocol, FeatureProtocolIds.GeoServicesFeatureService);
        var objectIdQuery = query is null
            ? new SourceQuery { ReturnGeometry = false, ReturnIdsOnly = useIdsOnlyMode ? true : null }
            : query with { ReturnGeometry = false, ReturnIdsOnly = useIdsOnlyMode ? true : query.ReturnIdsOnly };
        var request = BuildQueryRequest(objectIdQuery);
        var limit = query?.Limit;
        if (limit is <= 0)
        {
            return [];
        }

        var idFieldName = Descriptor.Schema?.PrimaryKey;
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var maxIds = limit.GetValueOrDefault(int.MaxValue);

        if (useIdsOnlyMode)
        {
            var result = await _queryClient.QueryAsync(request, ct).ConfigureAwait(false);
            idFieldName ??= result.ObjectIdFieldName;
            if (AddPageObjectIds(result, ids, seen, maxIds) ||
                AddPageFeatureIds(result, idFieldName, ids, seen, maxIds) ||
                !result.HasMoreResults)
            {
                return ids;
            }

            request = BuildQueryRequest(objectIdQuery with { ReturnIdsOnly = false });
        }

        await foreach (var page in _queryClient.QueryPagesAsync(request, ct).ConfigureAwait(false))
        {
            idFieldName ??= page.ObjectIdFieldName;
            if (AddPageObjectIds(page, ids, seen, maxIds) ||
                AddPageFeatureIds(page, idFieldName, ids, seen, maxIds))
            {
                return ids;
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

    private static List<string> BuildCapabilities(
        SourceDescriptor descriptor,
        IHonuaFeatureQueryClient queryClient,
        IHonuaFeatureEditClient? editClient)
    {
        var declared = descriptor.Capabilities.Count > 0
            ? descriptor.Capabilities
            : FeatureProtocolCapabilities.DefaultsFor(descriptor.Protocol);
        var normalizedDeclared = declared.Select(FeatureCapabilities.Normalize).ToList();
        var capabilities = new HashSet<string>(normalizedDeclared, StringComparer.Ordinal);

        if (!SupportsQueryProtocol(descriptor, queryClient))
        {
            capabilities.Remove(FeatureCapabilities.Query);
            capabilities.Remove(FeatureCapabilities.QueryObjectIds);
            capabilities.Remove(FeatureCapabilities.Stream);
        }

        if (editClient?.EditCapabilities is { } editCapabilities &&
            SupportsEditProtocol(descriptor, editClient) &&
            (editCapabilities.SupportsAdds ||
             editCapabilities.SupportsUpdates ||
             editCapabilities.SupportsPatches ||
             editCapabilities.SupportsDeletes))
        {
            if (FeatureCapabilities.Contains(normalizedDeclared, FeatureCapabilities.ApplyEdits) || descriptor.Capabilities.Count == 0)
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

    private static bool SupportsDescriptorProtocol(SourceDescriptor descriptor, IHonuaFeatureDescriptorClient descriptorClient)
        => FeatureProtocolIds.Matches(descriptor.Protocol, descriptorClient.ProviderName) ||
           FeatureProtocolIds.Matches(descriptor.CanonicalProtocol, descriptorClient.ProviderName);

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

    private static bool AddPageObjectIds(
        FeatureQueryResult page,
        List<string> ids,
        HashSet<string> seen,
        int maxIds)
    {
        foreach (var objectId in page.ObjectIds)
        {
            var id = objectId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (seen.Add(id))
            {
                ids.Add(id);
                if (ids.Count >= maxIds)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AddPageFeatureIds(
        FeatureQueryResult page,
        string? idFieldName,
        List<string> ids,
        HashSet<string> seen,
        int maxIds)
    {
        foreach (var feature in page.Features)
        {
            if (ResolveFeatureId(feature, idFieldName) is { } id && seen.Add(id))
            {
                ids.Add(id);
                if (ids.Count >= maxIds)
                {
                    return true;
                }
            }
        }

        return false;
    }

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
