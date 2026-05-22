// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin.Catalog;

/// <summary>
/// HTTP client implementation for Honua portal and catalog discovery.
/// </summary>
public sealed class HonuaCatalogClient : IHonuaCatalogClient
{
    private const string ApiPrefix = "/api/v1/admin";
    private const string FeatureServerProtocol = "FeatureServer";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaCatalogClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaCatalogClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<CatalogSearchResult> SearchAsync(CatalogQueryOptions? options = null, CancellationToken cancellationToken = default)
    {
        var normalizedOptions = NormalizeOptions(options);
        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var services = await LoadServicesAsync(metadata, cancellationToken).ConfigureAwait(false);

        var items = new List<CatalogItem>();
        if (IncludesKind(normalizedOptions, CatalogItemKind.Service))
        {
            items.AddRange(services.Select(ToItem));
        }

        if (IncludesKind(normalizedOptions, CatalogItemKind.Layer))
        {
            var layers = await LoadLayersAsync(services, metadata, cancellationToken).ConfigureAwait(false);
            items.AddRange(layers.Select(ToItem));
        }

        if (IncludesKind(normalizedOptions, CatalogItemKind.Group))
        {
            items.AddRange(BuildGroups(metadata.Groups).Select(ToItem));
        }

        if (IncludesKind(normalizedOptions, CatalogItemKind.SourceDescriptor))
        {
            items.AddRange(BuildSourceDescriptors(metadata.SourceDescriptors).Select(ToItem));
        }

        return ApplySearchQuery(items, normalizedOptions);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogService>> ListServicesAsync(
        CatalogQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = NormalizeOptions(options);
        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var services = await LoadServicesAsync(metadata, cancellationToken).ConfigureAwait(false);
        return ApplyDetailQuery(services, WithoutKindFilter(normalizedOptions), ToItem, static item => item.Service!);
    }

    /// <inheritdoc />
    public async Task<CatalogService?> GetServiceAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var services = await LoadServicesAsync(metadata, cancellationToken).ConfigureAwait(false);
        return services.FirstOrDefault(service => string.Equals(service.Name, serviceName, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogLayer>> ListLayersAsync(
        CatalogQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = NormalizeOptions(options);
        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var services = await LoadServicesAsync(metadata, cancellationToken).ConfigureAwait(false);
        var layers = await LoadLayersAsync(services, metadata, cancellationToken).ConfigureAwait(false);
        return ApplyDetailQuery(layers, WithoutKindFilter(normalizedOptions), ToItem, static item => item.Layer!);
    }

    /// <inheritdoc />
    public async Task<CatalogLayer?> GetLayerAsync(string serviceName, int layerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var service = (await LoadServicesAsync(metadata, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(candidate => string.Equals(candidate.Name, serviceName, StringComparison.OrdinalIgnoreCase));
        if (service is null)
        {
            return null;
        }

        var layer = await GetFeatureServerLayerInfoOrNullAsync(serviceName, layerId, cancellationToken).ConfigureAwait(false);
        return layer is null
            ? null
            : BuildCatalogLayer(service, layer, metadata);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogGroup>> ListGroupsAsync(
        CatalogQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = NormalizeOptions(options);
        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var groups = BuildGroups(metadata.Groups);
        return ApplyDetailQuery(groups, WithoutKindFilter(normalizedOptions), ToItem, static item => item.Group!);
    }

    /// <inheritdoc />
    public async Task<CatalogGroup?> GetGroupAsync(string ns, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ns);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = await GetMetadataResourceOrNullAsync(CatalogMetadataKinds.Group, ns, name, cancellationToken).ConfigureAwait(false);
        return resource is null ? null : BuildGroup(resource);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogSourceDescriptor>> ListSourceDescriptorsAsync(
        CatalogQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOptions = NormalizeOptions(options);
        var metadata = await LoadMetadataIndexAsync(cancellationToken).ConfigureAwait(false);
        var descriptors = BuildSourceDescriptors(metadata.SourceDescriptors);
        return ApplyDetailQuery(
            descriptors,
            WithoutKindFilter(normalizedOptions),
            ToItem,
            static item => item.SourceDescriptor!);
    }

    /// <inheritdoc />
    public async Task<CatalogSourceDescriptor?> GetSourceDescriptorAsync(string ns, string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ns);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resource = await GetMetadataResourceOrNullAsync(CatalogMetadataKinds.SourceDescriptor, ns, name, cancellationToken)
            .ConfigureAwait(false);
        return resource is null ? null : TryBuildSourceDescriptor(resource);
    }

    private async Task<IReadOnlyList<CatalogService>> LoadServicesAsync(
        CatalogMetadataIndex metadata,
        CancellationToken cancellationToken)
    {
        var summaries = await GetAdminEnvelopeAsync<ServiceSummary[]>(
            $"{ApiPrefix}/services/",
            HonuaAdminJsonContext.Default.ApiResponseServiceSummaryArray,
            cancellationToken).ConfigureAwait(false) ?? [];

        var services = new List<CatalogService>(summaries.Length);
        foreach (var summary in summaries.OrderBy(static service => service.ServiceName, StringComparer.OrdinalIgnoreCase))
        {
            var serviceMetadata = metadata.FindService(summary.ServiceName);
            var serviceInfo = HasServiceType(summary.EnabledProtocols, FeatureServerProtocol)
                ? await GetFeatureServerServiceInfoOrNullAsync(summary.ServiceName, cancellationToken).ConfigureAwait(false)
                : null;

            services.Add(BuildCatalogService(summary, serviceInfo, serviceMetadata));
        }

        return services.AsReadOnly();
    }

    private async Task<IReadOnlyList<CatalogLayer>> LoadLayersAsync(
        IReadOnlyList<CatalogService> services,
        CatalogMetadataIndex metadata,
        CancellationToken cancellationToken)
    {
        var layers = new List<CatalogLayer>();
        foreach (var service in services)
        {
            if (!HasServiceType(service.ServiceTypes, FeatureServerProtocol))
            {
                continue;
            }

            var serviceInfo = await GetFeatureServerServiceInfoOrNullAsync(service.Name, cancellationToken).ConfigureAwait(false);
            if (serviceInfo?.Layers is not { Count: > 0 })
            {
                continue;
            }

            foreach (var layerSummary in serviceInfo.Layers)
            {
                var layer = await GetFeatureServerLayerInfoOrNullAsync(service.Name, layerSummary.Id, cancellationToken).ConfigureAwait(false);
                if (layer is not null)
                {
                    layers.Add(BuildCatalogLayer(service, layer, metadata));
                }
            }
        }

        return layers.AsReadOnly();
    }

    private async Task<CatalogMetadataIndex> LoadMetadataIndexAsync(CancellationToken cancellationToken)
    {
        var services = await ListMetadataResourcesByKindAsync(CatalogMetadataKinds.Service, cancellationToken).ConfigureAwait(false);
        var layers = await ListMetadataResourcesByKindAsync(CatalogMetadataKinds.Layer, cancellationToken).ConfigureAwait(false);
        var groups = await ListMetadataResourcesByKindAsync(CatalogMetadataKinds.Group, cancellationToken).ConfigureAwait(false);
        var sourceDescriptors = await ListMetadataResourcesByKindAsync(CatalogMetadataKinds.SourceDescriptor, cancellationToken).ConfigureAwait(false);
        return new CatalogMetadataIndex(services, layers, groups, sourceDescriptors);
    }

    private async Task<IReadOnlyList<MetadataResource>> ListMetadataResourcesByKindAsync(
        string kind,
        CancellationToken cancellationToken)
    {
        var query = BuildQuery(("kind", kind));
        return await GetAdminEnvelopeAsync<MetadataResource[]>(
            $"{ApiPrefix}/metadata/resources{query}",
            HonuaAdminJsonContext.Default.ApiResponseMetadataResourceArray,
            cancellationToken).ConfigureAwait(false) ?? [];
    }

    private async Task<MetadataResource?> GetMetadataResourceOrNullAsync(
        string kind,
        string ns,
        string name,
        CancellationToken cancellationToken)
    {
        var url = $"{ApiPrefix}/metadata/resources/{Uri.EscapeDataString(kind)}/{Uri.EscapeDataString(ns)}/{Uri.EscapeDataString(name)}";
        try
        {
            return await GetAdminEnvelopeAsync<MetadataResource>(
                url,
                HonuaAdminJsonContext.Default.ApiResponseMetadataResource,
                cancellationToken).ConfigureAwait(false);
        }
        catch (HonuaAdminApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<CatalogFeatureServerServiceInfo?> GetFeatureServerServiceInfoOrNullAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var url = $"/rest/services/{Uri.EscapeDataString(serviceName)}/FeatureServer?f=json";
        try
        {
            return await GetRawAsync(url, HonuaAdminJsonContext.Default.CatalogFeatureServerServiceInfo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HonuaAdminApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<CatalogFeatureServerLayerInfo?> GetFeatureServerLayerInfoOrNullAsync(
        string serviceName,
        int layerId,
        CancellationToken cancellationToken)
    {
        var url = $"/rest/services/{Uri.EscapeDataString(serviceName)}/FeatureServer/{layerId}?f=json";
        try
        {
            return await GetRawAsync(url, HonuaAdminJsonContext.Default.CatalogFeatureServerLayerInfo, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HonuaAdminApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static CatalogService BuildCatalogService(
        ServiceSummary summary,
        CatalogFeatureServerServiceInfo? serviceInfo,
        MetadataResource? metadata)
    {
        var serviceTypes = NormalizeValues(summary.EnabledProtocols);
        var capabilities = MergeValues(serviceTypes, SplitCsv(serviceInfo?.Capabilities));
        var tags = GetTags(metadata);
        var owner = GetOwner(metadata);

        return new CatalogService
        {
            Id = summary.ServiceName,
            Name = summary.ServiceName,
            Description = FirstNonWhiteSpace(
                summary.Description,
                serviceInfo?.ServiceDescription,
                GetSpecString(metadata?.Spec, "description", "title")),
            LayerCount = summary.LayerCount,
            ServiceTypes = serviceTypes,
            Capabilities = capabilities,
            GeometryTypes = [],
            Extent = ToBoundingBox(serviceInfo?.FullExtent ?? serviceInfo?.InitialExtent),
            Namespace = metadata?.Metadata?.Namespace,
            Owner = owner,
            Tags = tags,
            MetadataResource = metadata
        };
    }

    private static CatalogLayer BuildCatalogLayer(
        CatalogService service,
        CatalogFeatureServerLayerInfo layer,
        CatalogMetadataIndex metadata)
    {
        var layerName = FirstNonWhiteSpace(layer.Name, layer.Id.ToString(CultureInfo.InvariantCulture))!;
        var layerMetadata = metadata.FindLayer(service.Name, layer.Id, layerName);
        var geometryType = NormalizeGeometryType(layer.GeometryType ?? GetSpecString(layerMetadata?.Spec, "geometryType"));
        var capabilities = MergeValues(
            service.ServiceTypes,
            SplitCsv(layer.Capabilities),
            layer.SupportsStatistics ? ["Statistics"] : [],
            layer.HasAttachments ? ["Attachments"] : []);
        var source = new SourceDescriptor
        {
            Id = $"{service.Name}/{layer.Id}",
            Protocol = FeatureProtocolIds.GeoServicesFeatureService,
            Locator = new SourceLocator
            {
                Url = $"/rest/services/{Uri.EscapeDataString(service.Name)}/FeatureServer/{layer.Id}",
                ServiceId = service.Name,
                LayerId = layer.Id
            },
            Capabilities = capabilities,
            Schema = new SourceSchema
            {
                GeometryType = ToFeatureGeometryType(geometryType),
                Extent = ToBoundingBox(layer.Extent),
                SpatialReference = ToSpatialReference(layer.SpatialReference ?? layer.Extent?.SpatialReference),
                ObjectIdField = layer.ObjectIdField,
                GlobalIdField = layer.GlobalIdField
            }
        };

        return new CatalogLayer
        {
            Id = source.Id,
            ServiceName = service.Name,
            LayerId = layer.Id,
            Name = layerName,
            Description = FirstNonWhiteSpace(layer.Description, GetSpecString(layerMetadata?.Spec, "description", "title")),
            ServiceTypes = service.ServiceTypes,
            Capabilities = capabilities,
            GeometryType = geometryType,
            Extent = ToBoundingBox(layer.Extent),
            SourceDescriptor = source,
            Namespace = layerMetadata?.Metadata?.Namespace,
            Owner = GetOwner(layerMetadata),
            Tags = GetTags(layerMetadata),
            MetadataResource = layerMetadata
        };
    }

    private static ReadOnlyCollection<CatalogGroup> BuildGroups(IReadOnlyList<MetadataResource> resources)
        => resources.Select(BuildGroup).ToList().AsReadOnly();

    private static CatalogGroup BuildGroup(MetadataResource resource)
    {
        var name = GetResourceName(resource);
        return new CatalogGroup
        {
            Id = GetResourceId(resource),
            Name = name,
            Namespace = resource.Metadata?.Namespace,
            Description = GetSpecString(resource.Spec, "description", "title"),
            Owner = GetOwner(resource),
            Tags = GetTags(resource),
            Resource = resource
        };
    }

    private static ReadOnlyCollection<CatalogSourceDescriptor> BuildSourceDescriptors(IReadOnlyList<MetadataResource> resources)
        => resources
            .Select(TryBuildSourceDescriptor)
            .Where(static descriptor => descriptor is not null)
            .Select(static descriptor => descriptor!)
            .ToList()
            .AsReadOnly();

    private static CatalogSourceDescriptor? TryBuildSourceDescriptor(MetadataResource resource)
    {
        if (!TryReadSourceDescriptor(resource.Spec, out var descriptor))
        {
            return null;
        }

        return new CatalogSourceDescriptor
        {
            Id = GetResourceId(resource),
            Name = GetResourceName(resource),
            Namespace = resource.Metadata?.Namespace,
            Owner = GetOwner(resource),
            Tags = GetTags(resource),
            Descriptor = descriptor,
            Resource = resource
        };
    }

    private static CatalogItem ToItem(CatalogService service)
        => new()
        {
            Id = service.Id,
            Kind = CatalogItemKind.Service,
            Name = service.Name,
            Description = service.Description,
            ServiceName = service.Name,
            Namespace = service.Namespace,
            Owner = service.Owner,
            Tags = service.Tags,
            ServiceTypes = service.ServiceTypes,
            Capabilities = service.Capabilities,
            Extent = service.Extent,
            CreatedAt = service.MetadataResource?.Metadata?.CreatedAt,
            UpdatedAt = service.MetadataResource?.Metadata?.UpdatedAt,
            Service = service
        };

    private static CatalogItem ToItem(CatalogLayer layer)
        => new()
        {
            Id = layer.Id,
            Kind = CatalogItemKind.Layer,
            Name = layer.Name,
            Description = layer.Description,
            ServiceName = layer.ServiceName,
            LayerId = layer.LayerId,
            Namespace = layer.Namespace,
            Owner = layer.Owner,
            Tags = layer.Tags,
            ServiceTypes = layer.ServiceTypes,
            Capabilities = layer.Capabilities,
            GeometryType = layer.GeometryType,
            Extent = layer.Extent,
            CreatedAt = layer.MetadataResource?.Metadata?.CreatedAt,
            UpdatedAt = layer.MetadataResource?.Metadata?.UpdatedAt,
            Layer = layer
        };

    private static CatalogItem ToItem(CatalogGroup group)
        => new()
        {
            Id = group.Id,
            Kind = CatalogItemKind.Group,
            Name = group.Name,
            Description = group.Description,
            Namespace = group.Namespace,
            Owner = group.Owner,
            Tags = group.Tags,
            CreatedAt = group.Resource.Metadata?.CreatedAt,
            UpdatedAt = group.Resource.Metadata?.UpdatedAt,
            Group = group
        };

    private static CatalogItem ToItem(CatalogSourceDescriptor descriptor)
        => new()
        {
            Id = descriptor.Id,
            Kind = CatalogItemKind.SourceDescriptor,
            Name = descriptor.Name,
            Namespace = descriptor.Namespace,
            Owner = descriptor.Owner,
            Tags = descriptor.Tags,
            ServiceTypes = [descriptor.Descriptor.Protocol],
            Capabilities = descriptor.Descriptor.Capabilities,
            GeometryType = descriptor.Descriptor.Schema?.GeometryType.ToString(),
            Extent = descriptor.Descriptor.Schema?.Extent,
            CreatedAt = descriptor.Resource.Metadata?.CreatedAt,
            UpdatedAt = descriptor.Resource.Metadata?.UpdatedAt,
            SourceDescriptor = descriptor
        };

    private static CatalogSearchResult ApplySearchQuery(
        IReadOnlyList<CatalogItem> items,
        CatalogQueryOptions options)
    {
        var filtered = FilterAndSortItems(items, options).ToList();
        var totalCount = filtered.Count;
        var paged = PageItems(filtered, options).ToList().AsReadOnly();
        var nextOffset = options.Limit.HasValue && options.Offset.GetValueOrDefault() + options.Limit.Value < totalCount
            ? options.Offset.GetValueOrDefault() + options.Limit.Value
            : (int?)null;

        return new CatalogSearchResult
        {
            Items = paged,
            TotalCount = totalCount,
            Offset = options.Offset.GetValueOrDefault(),
            NextOffset = nextOffset
        };
    }

    private static ReadOnlyCollection<T> ApplyDetailQuery<T>(
        IReadOnlyList<T> values,
        CatalogQueryOptions options,
        Func<T, CatalogItem> toItem,
        Func<CatalogItem, T> fromItem)
    {
        var selected = FilterAndSortItems(values.Select(toItem), options)
            .Select(fromItem);

        return PageItems(selected, options).ToList().AsReadOnly();
    }

    private static IEnumerable<CatalogItem> FilterAndSortItems(
        IEnumerable<CatalogItem> items,
        CatalogQueryOptions options)
    {
        var filtered = items.Where(item => MatchesFilters(item, options));
        return options.SortBy switch
        {
            CatalogSortBy.Kind => Sort(filtered, item => item.Kind.ToString(), options.SortDirection),
            CatalogSortBy.ServiceName => Sort(filtered, item => item.ServiceName ?? string.Empty, options.SortDirection),
            CatalogSortBy.CreatedAt => Sort(filtered, item => item.CreatedAt, options.SortDirection),
            CatalogSortBy.UpdatedAt => Sort(filtered, item => item.UpdatedAt, options.SortDirection),
            _ => Sort(filtered, item => item.Name, options.SortDirection)
        };
    }

    private static IEnumerable<T> Sort<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector,
        CatalogSortDirection direction)
        => direction == CatalogSortDirection.Descending
            ? values.OrderByDescending(keySelector)
            : values.OrderBy(keySelector);

    private static IEnumerable<T> PageItems<T>(IEnumerable<T> items, CatalogQueryOptions options)
    {
        var paged = items.Skip(options.Offset.GetValueOrDefault());
        return options.Limit.HasValue ? paged.Take(options.Limit.Value) : paged;
    }

    private static bool MatchesFilters(CatalogItem item, CatalogQueryOptions options)
    {
        if (options.Kinds is { Count: > 0 } && !options.Kinds.Contains(item.Kind))
        {
            return false;
        }

        if (!MatchesQuery(item, options.Query))
        {
            return false;
        }

        if (!MatchesAny(item.ServiceTypes, options.ServiceTypes))
        {
            return false;
        }

        if (!MatchesAll(item.Tags, options.Tags))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Owner) &&
            !string.Equals(item.Owner, options.Owner, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(options.Namespace) &&
            !string.Equals(item.Namespace, options.Namespace, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (options.GeometryTypes is { Count: > 0 } &&
            !ContainsValue(options.GeometryTypes.Select(NormalizeGeometryType), item.GeometryType))
        {
            return false;
        }

        return MatchesAll(item.Capabilities, options.Capabilities);
    }

    private static bool MatchesQuery(CatalogItem item, string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var haystack = new[]
        {
            item.Id,
            item.Name,
            item.Description,
            item.ServiceName,
            item.Namespace,
            item.Owner,
            item.GeometryType
        }.Concat(item.Tags).Concat(item.ServiceTypes).Concat(item.Capabilities);

        return haystack.Any(value => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool MatchesAny(IReadOnlyList<string> actual, IReadOnlyList<string>? filters)
        => filters is null or { Count: 0 } ||
           filters.Any(filter => actual.Any(value => string.Equals(value, filter, StringComparison.OrdinalIgnoreCase)));

    private static bool MatchesAll(IReadOnlyList<string> actual, IReadOnlyList<string>? filters)
        => filters is null or { Count: 0 } ||
           filters.All(filter => actual.Any(value => string.Equals(value, filter, StringComparison.OrdinalIgnoreCase)));

    private static bool ContainsValue(IEnumerable<string?> values, string? candidate)
        => !string.IsNullOrWhiteSpace(candidate) &&
           values.Any(value => string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase));

    private static bool IncludesKind(CatalogQueryOptions options, CatalogItemKind kind)
        => options.Kinds is null or { Count: 0 } || options.Kinds.Contains(kind);

    private static CatalogQueryOptions NormalizeOptions(CatalogQueryOptions? options)
    {
        var normalized = options ?? new CatalogQueryOptions();
        if (normalized.Offset is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Catalog query offset cannot be negative.");
        }

        if (normalized.Limit is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Catalog query limit cannot be negative.");
        }

        return normalized;
    }

    private static CatalogQueryOptions WithoutKindFilter(CatalogQueryOptions options)
        => options.Kinds is null ? options : options with { Kinds = null };

    private async Task<T?> GetAdminEnvelopeAsync<T>(
        string url,
        JsonTypeInfo<ApiResponse<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);
        EnsureEnvelopeSucceeded(response, body);

        var envelope = JsonSerializer.Deserialize(body, typeInfo);
        return envelope is not null ? envelope.Data : default;
    }

    private async Task<T?> GetRawAsync<T>(
        string url,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return JsonSerializer.Deserialize(body, typeInfo);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Catalog request failed";
        throw new HonuaAdminApiException(response.StatusCode, message, body);
    }

    private static void EnsureEnvelopeSucceeded(HttpResponseMessage response, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("success", out var success) &&
                success.ValueKind == JsonValueKind.False)
            {
                var message = TryExtractErrorMessage(body) ?? "API response indicated failure.";
                throw new HonuaAdminApiException(response.StatusCode, message, body);
            }
        }
        catch (JsonException)
        {
            // Not JSON or invalid JSON envelope, ignore.
        }
    }

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }

            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var errorMessage) &&
                errorMessage.ValueKind == JsonValueKind.String)
            {
                return errorMessage.GetString();
            }

            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString();
            }

            if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON, that's fine.
        }

        return null;
    }

    private static bool TryReadSourceDescriptor(JsonElement spec, out SourceDescriptor descriptor)
    {
        foreach (var candidate in EnumerateDescriptorCandidates(spec))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize(candidate.GetRawText(), HonuaAdminJsonContext.Default.SourceDescriptor);
                if (parsed is not null)
                {
                    descriptor = parsed;
                    return true;
                }
            }
            catch (JsonException)
            {
                // Try the next known descriptor shape.
            }
        }

        descriptor = default!;
        return false;
    }

    private static IEnumerable<JsonElement> EnumerateDescriptorCandidates(JsonElement spec)
    {
        if (spec.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        if (spec.TryGetProperty("sourceDescriptor", out var sourceDescriptor) &&
            sourceDescriptor.ValueKind == JsonValueKind.Object)
        {
            yield return sourceDescriptor;
        }

        if (spec.TryGetProperty("descriptor", out var descriptor) &&
            descriptor.ValueKind == JsonValueKind.Object)
        {
            yield return descriptor;
        }

        yield return spec;
    }

    private static string? GetSpecString(JsonElement? spec, params string[] names)
    {
        if (spec is null || spec.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (spec.Value.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int? GetSpecInt(JsonElement? spec, params string[] names)
    {
        if (spec is null || spec.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (spec.Value.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static string GetResourceId(MetadataResource resource)
        => FirstNonWhiteSpace(resource.Metadata?.Id, resource.Metadata?.Name, GetSpecString(resource.Spec, "id", "name")) ??
           string.Empty;

    private static string GetResourceName(MetadataResource resource)
        => FirstNonWhiteSpace(resource.Metadata?.Name, GetSpecString(resource.Spec, "name", "title"), resource.Metadata?.Id) ??
           string.Empty;

    private static string? GetOwner(MetadataResource? resource)
        => FirstNonWhiteSpace(
            GetDictionaryValue(resource?.Metadata?.Labels, "owner"),
            GetDictionaryValue(resource?.Metadata?.Labels, "honua.io/owner"),
            GetDictionaryValue(resource?.Metadata?.Annotations, "owner"),
            GetDictionaryValue(resource?.Metadata?.Annotations, "honua.io/owner"));

    private static ReadOnlyCollection<string> GetTags(MetadataResource? resource)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddSplitValues(tags, GetDictionaryValue(resource?.Metadata?.Labels, "tags"));
        AddSplitValues(tags, GetDictionaryValue(resource?.Metadata?.Labels, "honua.io/tags"));
        AddSplitValues(tags, GetDictionaryValue(resource?.Metadata?.Annotations, "tags"));
        AddSplitValues(tags, GetDictionaryValue(resource?.Metadata?.Annotations, "honua.io/tags"));

        if (resource?.Metadata?.Labels is not null)
        {
            foreach (var (key, value) in resource.Metadata.Labels)
            {
                if (key.StartsWith("tag/", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("tags/", StringComparison.OrdinalIgnoreCase))
                {
                    AddSplitValues(tags, value);
                }
            }
        }

        return tags.OrderBy(static tag => tag, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly();
    }

    private static void AddSplitValues(HashSet<string> values, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var value in raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            values.Add(value);
        }
    }

    private static string? GetDictionaryValue(Dictionary<string, string>? values, string key)
        => values is not null && values.TryGetValue(key, out var value) ? value : null;

    private static ReadOnlyCollection<string> NormalizeValues(IEnumerable<string>? values)
        => values is null
            ? []
            : values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .ToList()
                .AsReadOnly();

    private static ReadOnlyCollection<string> MergeValues(params IEnumerable<string>[] valueSets)
        => NormalizeValues(valueSets.SelectMany(static values => values));

    private static ReadOnlyCollection<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : NormalizeValues(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? FirstNonWhiteSpace(params string?[] values)
        => values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

    private static bool HasServiceType(IReadOnlyList<string>? serviceTypes, string serviceType)
        => serviceTypes?.Any(type => string.Equals(type, serviceType, StringComparison.OrdinalIgnoreCase)) == true;

    private static FeatureBoundingBox? ToBoundingBox(CatalogFeatureServerExtent? extent)
    {
        if (extent is null)
        {
            return null;
        }

        return new FeatureBoundingBox
        {
            MinX = extent.XMin,
            MinY = extent.YMin,
            MaxX = extent.XMax,
            MaxY = extent.YMax,
            Crs = ToSpatialReference(extent.SpatialReference)
        };
    }

    private static string? ToSpatialReference(CatalogFeatureServerSpatialReference? spatialReference)
    {
        if (spatialReference is null)
        {
            return null;
        }

        return spatialReference.LatestWkid > 0
            ? $"EPSG:{spatialReference.LatestWkid.ToString(CultureInfo.InvariantCulture)}"
            : spatialReference.Wkid > 0
                ? $"EPSG:{spatialReference.Wkid.ToString(CultureInfo.InvariantCulture)}"
                : spatialReference.Wkt;
    }

    private static string? NormalizeGeometryType(string? geometryType)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
        {
            return null;
        }

        return geometryType.Trim() switch
        {
            "esriGeometryPoint" => "Point",
            "esriGeometryMultipoint" => "MultiPoint",
            "esriGeometryPolyline" => "Polyline",
            "esriGeometryPolygon" => "Polygon",
            "esriGeometryEnvelope" => "Envelope",
            var value => value
        };
    }

    private static FeatureSpatialGeometryType ToFeatureGeometryType(string? geometryType)
        => geometryType switch
        {
            "Point" => FeatureSpatialGeometryType.Point,
            "MultiPoint" => FeatureSpatialGeometryType.MultiPoint,
            "Polyline" => FeatureSpatialGeometryType.Polyline,
            "Polygon" => FeatureSpatialGeometryType.Polygon,
            "Envelope" => FeatureSpatialGeometryType.Envelope,
            _ => FeatureSpatialGeometryType.Unspecified
        };

    private static string BuildQuery(params ReadOnlySpan<(string Key, string? Value)> parameters)
    {
        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private sealed class CatalogMetadataIndex
    {
        private readonly Dictionary<string, MetadataResource> _services;
        private readonly Dictionary<string, MetadataResource> _layers;

        public CatalogMetadataIndex(
            IReadOnlyList<MetadataResource> services,
            IReadOnlyList<MetadataResource> layers,
            IReadOnlyList<MetadataResource> groups,
            IReadOnlyList<MetadataResource> sourceDescriptors)
        {
            _services = BuildServiceIndex(services);
            _layers = BuildLayerIndex(layers);
            Groups = groups;
            SourceDescriptors = sourceDescriptors;
        }

        public IReadOnlyList<MetadataResource> Groups { get; }

        public IReadOnlyList<MetadataResource> SourceDescriptors { get; }

        public MetadataResource? FindService(string serviceName)
            => _services.TryGetValue(NormalizeKey(serviceName), out var resource) ? resource : null;

        public MetadataResource? FindLayer(string serviceName, int layerId, string layerName)
        {
            var keys = new[]
            {
                $"{serviceName}/{layerId.ToString(CultureInfo.InvariantCulture)}",
                $"{serviceName}/{layerName}",
                layerId.ToString(CultureInfo.InvariantCulture),
                layerName
            };

            foreach (var key in keys)
            {
                if (_layers.TryGetValue(NormalizeKey(key), out var resource))
                {
                    return resource;
                }
            }

            return null;
        }

        private static Dictionary<string, MetadataResource> BuildServiceIndex(IReadOnlyList<MetadataResource> resources)
        {
            var index = new Dictionary<string, MetadataResource>(StringComparer.OrdinalIgnoreCase);
            foreach (var resource in resources)
            {
                AddIndexValue(index, resource, resource.Metadata?.Name);
                AddIndexValue(index, resource, resource.Metadata?.Id);
                AddIndexValue(index, resource, GetSpecString(resource.Spec, "serviceName", "name"));
            }

            return index;
        }

        private static Dictionary<string, MetadataResource> BuildLayerIndex(IReadOnlyList<MetadataResource> resources)
        {
            var index = new Dictionary<string, MetadataResource>(StringComparer.OrdinalIgnoreCase);
            foreach (var resource in resources)
            {
                var serviceName = GetSpecString(resource.Spec, "serviceName", "serviceId");
                var layerId = GetSpecInt(resource.Spec, "layerId", "id");
                var name = FirstNonWhiteSpace(resource.Metadata?.Name, GetSpecString(resource.Spec, "layerName", "name"));

                AddIndexValue(index, resource, resource.Metadata?.Name);
                AddIndexValue(index, resource, resource.Metadata?.Id);
                AddIndexValue(index, resource, name);

                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    AddIndexValue(index, resource, $"{serviceName}/{name}");
                    if (layerId.HasValue)
                    {
                        AddIndexValue(index, resource, $"{serviceName}/{layerId.Value.ToString(CultureInfo.InvariantCulture)}");
                    }
                }

                if (layerId.HasValue)
                {
                    AddIndexValue(index, resource, layerId.Value.ToString(CultureInfo.InvariantCulture));
                }
            }

            return index;
        }

        private static void AddIndexValue(
            Dictionary<string, MetadataResource> index,
            MetadataResource resource,
            string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                index.TryAdd(NormalizeKey(value), resource);
            }
        }

        private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
    }
}

internal sealed class CatalogFeatureServerServiceInfo
{
    [JsonPropertyName("serviceDescription")]
    public string? ServiceDescription { get; set; }

    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; set; }

    [JsonPropertyName("initialExtent")]
    public CatalogFeatureServerExtent? InitialExtent { get; set; }

    [JsonPropertyName("fullExtent")]
    public CatalogFeatureServerExtent? FullExtent { get; set; }

    [JsonPropertyName("layers")]
    public IReadOnlyList<CatalogFeatureServerLayerSummary>? Layers { get; set; }
}

internal sealed class CatalogFeatureServerLayerSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class CatalogFeatureServerLayerInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; set; }

    [JsonPropertyName("spatialReference")]
    public CatalogFeatureServerSpatialReference? SpatialReference { get; set; }

    [JsonPropertyName("extent")]
    public CatalogFeatureServerExtent? Extent { get; set; }

    [JsonPropertyName("objectIdField")]
    public string? ObjectIdField { get; set; }

    [JsonPropertyName("globalIdField")]
    public string? GlobalIdField { get; set; }

    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; set; }

    [JsonPropertyName("hasAttachments")]
    public bool HasAttachments { get; set; }

    [JsonPropertyName("supportsStatistics")]
    public bool SupportsStatistics { get; set; }
}

internal sealed class CatalogFeatureServerExtent
{
    [JsonPropertyName("xmin")]
    public double XMin { get; set; }

    [JsonPropertyName("ymin")]
    public double YMin { get; set; }

    [JsonPropertyName("xmax")]
    public double XMax { get; set; }

    [JsonPropertyName("ymax")]
    public double YMax { get; set; }

    [JsonPropertyName("spatialReference")]
    public CatalogFeatureServerSpatialReference? SpatialReference { get; set; }
}

internal sealed class CatalogFeatureServerSpatialReference
{
    [JsonPropertyName("wkid")]
    public int Wkid { get; set; }

    [JsonPropertyName("latestWkid")]
    public int LatestWkid { get; set; }

    [JsonPropertyName("wkt")]
    public string? Wkt { get; set; }
}
