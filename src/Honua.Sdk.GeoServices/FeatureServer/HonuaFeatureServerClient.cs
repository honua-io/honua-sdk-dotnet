// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// HTTP client implementation for the Honua FeatureServer (GeoServices) read/query API.
/// </summary>
public sealed class HonuaFeatureServerClient : IHonuaFeatureServerClient, IHonuaFeatureServerEditClient, IHonuaFeatureQueryClient, IHonuaFeatureEditClient
{
    private const int PostFallbackThreshold = 2000;
    private static readonly FeatureEditCapabilities ProviderEditCapabilities = new()
    {
        SupportsAdds = true,
        SupportsUpdates = true,
        SupportsDeletes = true,
        SupportsRollbackOnFailure = true,
        NativeSurface = "GeoServices FeatureServer applyEdits"
    };

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureServerClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaFeatureServerClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public string ProviderName => "geoservices-featureserver";

    /// <inheritdoc />
    public FeatureEditCapabilities EditCapabilities => ProviderEditCapabilities;

    private static string ServicePath(string serviceId) =>
        $"/rest/services/{Uri.EscapeDataString(serviceId)}/FeatureServer";

    /// <inheritdoc />
    public async Task<FeatureServerServiceInfo> GetServiceInfoAsync(string serviceId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        var url = $"{ServicePath(serviceId)}?f=json";

        var body = await GetStringAsync(url, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerServiceInfo)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize service info.", body);
    }

    /// <inheritdoc />
    public async Task<FeatureServerLayerInfo> GetLayerInfoAsync(string serviceId, int layerId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        var url = $"{ServicePath(serviceId)}/{layerId}?f=json";

        var body = await GetStringAsync(url, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerLayerInfo)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize layer info.", body);
    }

    /// <inheritdoc />
    public async Task<FeatureEditCapabilities> GetEditCapabilitiesAsync(
        string serviceId, int layerId, CancellationToken ct = default)
    {
        var layer = await GetLayerInfoAsync(serviceId, layerId, ct).ConfigureAwait(false);
        return BuildEditCapabilities(layer.Capabilities);
    }

    /// <inheritdoc />
    public async Task<FeatureServerQueryResponse> QueryAsync(
        string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var body = await ExecuteQueryAsync(serviceId, layerId, BuildQueryParams(query), ct).ConfigureAwait(false);
        return DeserializeQueryResponse(body);
    }

    /// <inheritdoc />
    public async Task<FeatureServerFeature?> GetFeatureAsync(
        string serviceId,
        int layerId,
        long objectId,
        FeatureServerQueryParams? query = null,
        CancellationToken ct = default)
    {
        var response = await QueryAsync(
            serviceId,
            layerId,
            (query ?? new FeatureServerQueryParams()) with
            {
                ObjectIds = [objectId],
                ResultRecordCount = 1
            },
            ct).ConfigureAwait(false);

        return response.Features is { Count: > 0 } features ? features[0] : null;
    }

    /// <inheritdoc />
    public async Task<FeatureQueryResult> QueryAsync(
        FeatureQueryRequest request, CancellationToken ct = default)
    {
        var (serviceId, layerId, query) = BuildFeatureServerQuery(request);
        var response = await QueryAsync(serviceId, layerId, query, ct).ConfigureAwait(false);
        return ToFeatureQueryResult(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        FeatureQueryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (serviceId, layerId, query) = BuildFeatureServerQuery(request);
        await foreach (var page in QueryPagesAsync(serviceId, layerId, query, ct).ConfigureAwait(false))
        {
            yield return ToFeatureQueryResult(page);
        }
    }

    /// <inheritdoc />
    public async Task<FeatureServerEditResponse> ApplyEditsAsync(
        string serviceId,
        int layerId,
        FeatureServerEditRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(request);
        EnsureHasEdits(request);

        var parameters = BuildEditParams(request);
        var basePath = $"{ServicePath(serviceId)}/{layerId}/applyEdits";
        var body = await PostFormAsync(basePath, parameters, ct).ConfigureAwait(false);
        return DeserializeEditResponse(body);
    }

    /// <inheritdoc />
    public Task<FeatureServerEditResponse> AddFeaturesAsync(
        string serviceId,
        int layerId,
        IReadOnlyList<FeatureServerFeature> features,
        bool rollbackOnFailure = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(features);
        return ApplyEditsAsync(
            serviceId,
            layerId,
            new FeatureServerEditRequest
            {
                Adds = features,
                RollbackOnFailure = rollbackOnFailure
            },
            ct);
    }

    /// <inheritdoc />
    public Task<FeatureServerEditResponse> UpdateFeaturesAsync(
        string serviceId,
        int layerId,
        IReadOnlyList<FeatureServerFeature> features,
        bool rollbackOnFailure = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(features);
        return ApplyEditsAsync(
            serviceId,
            layerId,
            new FeatureServerEditRequest
            {
                Updates = features,
                RollbackOnFailure = rollbackOnFailure
            },
            ct);
    }

    /// <inheritdoc />
    public Task<FeatureServerEditResponse> DeleteFeaturesAsync(
        string serviceId,
        int layerId,
        IReadOnlyList<long> objectIds,
        bool rollbackOnFailure = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(objectIds);
        return ApplyEditsAsync(
            serviceId,
            layerId,
            new FeatureServerEditRequest
            {
                Deletes = objectIds,
                RollbackOnFailure = rollbackOnFailure
            },
            ct);
    }

    /// <inheritdoc />
    public async Task<FeatureEditResponse> ApplyEditsAsync(
        FeatureEditRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (serviceId, layerId) = GetEditSource(request);
        var objectIdField = await ResolveObjectIdFieldAsync(serviceId, layerId, request, ct).ConfigureAwait(false);
        var response = await ApplyEditsAsync(
            serviceId,
            layerId,
            BuildFeatureServerEditRequest(request, objectIdField),
            ct).ConfigureAwait(false);

        return ToFeatureEditResponse(response);
    }

    /// <inheritdoc />
    public async Task<long> QueryCountAsync(
        string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var parameters = BuildQueryParams(query);
        parameters.Add(("returnCountOnly", "true"));

        var body = await ExecuteQueryAsync(serviceId, layerId, parameters, ct).ConfigureAwait(false);
        var response = DeserializeQueryResponse(body);
        return response.Count ?? 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> QueryIdsAsync(
        string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var parameters = BuildQueryParams(query);
        parameters.Add(("returnIdsOnly", "true"));

        var body = await ExecuteQueryAsync(serviceId, layerId, parameters, ct).ConfigureAwait(false);
        var response = DeserializeQueryResponse(body);
        return response.ObjectIds ?? [];
    }

    /// <inheritdoc />
    public async Task<FeatureServerExtent> QueryExtentAsync(
        string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var parameters = BuildQueryParams(query);
        parameters.Add(("returnExtentOnly", "true"));

        var body = await ExecuteQueryAsync(serviceId, layerId, parameters, ct).ConfigureAwait(false);
        var response = DeserializeQueryResponse(body);
        return response.Extent
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Server did not return extent.", body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureServerQueryResponse> QueryPagesAsync(
        string serviceId, int layerId, FeatureServerQueryParams query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var currentQuery = query;
        while (true)
        {
            var page = await QueryAsync(serviceId, layerId, currentQuery, ct).ConfigureAwait(false);
            yield return page;

            var count = page.Features?.Count ?? 0;
            if (count == 0 || !page.ExceededTransferLimit)
            {
                yield break;
            }

            var currentOffset = currentQuery.ResultOffset ?? 0;
            currentQuery = currentQuery with { ResultOffset = currentOffset + count };
        }
    }

    /// <inheritdoc />
    public async Task<FeatureServerQueryResponse> QueryStatisticsAsync(
        string serviceId, int layerId, FeatureServerStatisticsParams query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var parameters = BuildStatisticsParams(query);
        var body = await ExecuteQueryAsync(serviceId, layerId, parameters, ct).ConfigureAwait(false);
        return DeserializeQueryResponse(body);
    }

    /// <inheritdoc />
    public async Task<FeatureServerValidateSqlResponse> ValidateSqlAsync(
        string serviceId, int layerId, string where, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(where);

        var parameters = new List<(string Key, string? Value)>
        {
            ("where", where),
            ("f", "json"),
        };

        var basePath = $"{ServicePath(serviceId)}/{layerId}/validateSQL";
        var body = await PostFormAsync(basePath, parameters, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerValidateSqlResponse)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize validateSQL response.", body);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> QueryRawAsync(
        string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);

        var parameters = BuildQueryParams(query);
        var basePath = $"{ServicePath(serviceId)}/{layerId}/query";
        var queryString = BuildQueryString(parameters);
        var url = basePath + queryString;

        if (url.Length > PostFallbackThreshold)
        {
            using var content = new FormUrlEncodedContent(
                parameters.Where(p => p.Value is not null).Select(p => new KeyValuePair<string, string>(p.Key, p.Value!)));
            return await _http.PostAsync(CreateRequestUri(basePath), content, ct).ConfigureAwait(false);
        }

        return await _http.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private async Task<string> ExecuteQueryAsync(
        string serviceId, int layerId, List<(string Key, string? Value)> parameters, CancellationToken ct)
    {
        var basePath = $"{ServicePath(serviceId)}/{layerId}/query";
        var queryString = BuildQueryString(parameters);
        var url = basePath + queryString;

        if (url.Length > PostFallbackThreshold)
        {
            return await PostFormAsync(basePath, parameters, ct).ConfigureAwait(false);
        }

        return await GetStringAsync(url, ct).ConfigureAwait(false);
    }

    private async Task<string> PostFormAsync(
        string path, List<(string Key, string? Value)> parameters, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(
            parameters.Where(p => p.Value is not null).Select(p => new KeyValuePair<string, string>(p.Key, p.Value!)));

        using var response = await _http.PostAsync(CreateRequestUri(path), content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            // GeoServices may return 200 with an error payload
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                        errorElement.ValueKind == JsonValueKind.Object)
                    {
                        var message = "FeatureServer returned an error.";
                        int? geoServicesCode = null;
                        IReadOnlyList<string>? details = null;

                        if (errorElement.TryGetProperty("message", out var msgProp) &&
                            msgProp.ValueKind == JsonValueKind.String)
                        {
                            message = msgProp.GetString() ?? message;
                        }

                        var httpCode = response.StatusCode;
                        if (errorElement.TryGetProperty("code", out var codeProp) &&
                            codeProp.TryGetInt32(out var errorCode))
                        {
                            geoServicesCode = errorCode;
                            httpCode = (HttpStatusCode)errorCode;
                        }

                        if (errorElement.TryGetProperty("details", out var detailsProp) &&
                            detailsProp.ValueKind == JsonValueKind.Array)
                        {
                            var detailList = new List<string>();
                            foreach (var item in detailsProp.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.String)
                                {
                                    detailList.Add(item.GetString()!);
                                }
                            }
                            details = detailList;
                        }

                        throw new HonuaFeatureServerException(httpCode, message, body, geoServicesCode, details);
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, ignore
                }
            }

            return;
        }

        var errorMessage = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "FeatureServer request failed";
        throw new HonuaFeatureServerException(response.StatusCode, errorMessage, body);
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

            if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }

            if (doc.RootElement.TryGetProperty("message", out var topMsg) &&
                topMsg.ValueKind == JsonValueKind.String)
            {
                return topMsg.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON
        }

        return null;
    }

    private static FeatureServerQueryResponse DeserializeQueryResponse(string body)
    {
        return JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerQueryResponse)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize query response.", body);
    }

    private static FeatureServerEditResponse DeserializeEditResponse(string body)
    {
        return JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerEditResponse)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize edit response.", body);
    }

    private static List<(string Key, string? Value)> BuildQueryParams(FeatureServerQueryParams query)
    {
        var parameters = new List<(string Key, string? Value)>
        {
            ("where", query.Where ?? "1=1"),
            ("f", FormatToString(query.Format)),
        };

        if (query.OutFields is not null)
            parameters.Add(("outFields", query.OutFields));

        if (query.ObjectIds is { Count: > 0 })
            parameters.Add(("objectIds", string.Join(",", query.ObjectIds)));

        if (query.OrderByFields is not null)
            parameters.Add(("orderByFields", query.OrderByFields));

        if (query.ReturnGeometry.HasValue)
            parameters.Add(("returnGeometry", query.ReturnGeometry.Value ? "true" : "false"));

        if (query.ResultOffset.HasValue)
            parameters.Add(("resultOffset", query.ResultOffset.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.ResultRecordCount.HasValue)
            parameters.Add(("resultRecordCount", query.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.SpatialFilter is not null)
        {
            if (query.SpatialFilter.Geometry is not null)
                parameters.Add(("geometry", query.SpatialFilter.Geometry));

            if (query.SpatialFilter.GeometryType is not null)
                parameters.Add(("geometryType", query.SpatialFilter.GeometryType));

            parameters.Add(("spatialRel", SpatialRelToString(query.SpatialFilter.SpatialRel)));
        }

        if (query.OutSR.HasValue)
            parameters.Add(("outSR", query.OutSR.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.InSR.HasValue)
            parameters.Add(("inSR", query.InSR.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.Time is not null)
            parameters.Add(("time", query.Time));

        if (query.TimeRelation.HasValue)
            parameters.Add(("timeRelation", TimeRelationToString(query.TimeRelation.Value)));

        if (query.ReturnDistinctValues is true)
            parameters.Add(("returnDistinctValues", "true"));

        if (query.ReturnCountOnly is true)
            parameters.Add(("returnCountOnly", "true"));

        if (query.ReturnIdsOnly is true)
            parameters.Add(("returnIdsOnly", "true"));

        if (query.ReturnExtentOnly is true)
            parameters.Add(("returnExtentOnly", "true"));

        return parameters;
    }

    private static List<(string Key, string? Value)> BuildEditParams(FeatureServerEditRequest request)
    {
        var parameters = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("rollbackOnFailure", request.RollbackOnFailure ? "true" : "false"),
        };

        if (request.ForceWrite)
        {
            parameters.Add(("forceWrite", "true"));
        }

        if (request.Adds is { Count: > 0 })
        {
            parameters.Add(("adds", SerializeFeatures(request.Adds)));
        }

        if (request.Updates is { Count: > 0 })
        {
            parameters.Add(("updates", SerializeFeatures(request.Updates)));
        }

        if (request.Deletes is { Count: > 0 })
        {
            parameters.Add(("deletes", string.Join(",", request.Deletes)));
        }

        return parameters;
    }

    private static string SerializeFeatures(IReadOnlyList<FeatureServerFeature> features)
    {
        return JsonSerializer.Serialize(
            features.ToArray(),
            FeatureServerJsonContext.Default.FeatureServerFeatureArray);
    }

    private static void EnsureHasEdits(FeatureServerEditRequest request)
    {
        if (request.Adds is { Count: > 0 } ||
            request.Updates is { Count: > 0 } ||
            request.Deletes is { Count: > 0 })
        {
            return;
        }

        throw new ArgumentException("At least one add, update, or delete edit is required.", nameof(request));
    }

    private static List<(string Key, string? Value)> BuildStatisticsParams(FeatureServerStatisticsParams query)
    {
        var parameters = new List<(string Key, string? Value)>
        {
            ("where", query.Where ?? "1=1"),
            ("f", "json"),
        };

        if (query.OutStatistics is not null)
            parameters.Add(("outStatistics", query.OutStatistics));

        if (query.GroupByFieldsForStatistics is not null)
            parameters.Add(("groupByFieldsForStatistics", query.GroupByFieldsForStatistics));

        if (query.Having is not null)
            parameters.Add(("having", query.Having));

        if (query.OrderByFields is not null)
            parameters.Add(("orderByFields", query.OrderByFields));

        if (query.ResultOffset.HasValue)
            parameters.Add(("resultOffset", query.ResultOffset.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.ResultRecordCount.HasValue)
            parameters.Add(("resultRecordCount", query.ResultRecordCount.Value.ToString(CultureInfo.InvariantCulture)));

        return parameters;
    }

    private static string FormatToString(FeatureServerFormat? format) => format switch
    {
        FeatureServerFormat.GeoJson => "geojson",
        FeatureServerFormat.Pbf => "pbf",
        FeatureServerFormat.FlatGeobuf => "flatgeobuf",
        FeatureServerFormat.Parquet => "parquet",
        _ => "json",
    };

    private static (string ServiceId, int LayerId, FeatureServerQueryParams Query) BuildFeatureServerQuery(
        FeatureQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupportedFilterLanguage(request.FilterLanguage);

        if (string.IsNullOrWhiteSpace(request.Source.ServiceId))
        {
            throw new ArgumentException("A service ID is required for FeatureServer queries.", nameof(request));
        }

        if (!request.Source.LayerId.HasValue)
        {
            throw new ArgumentException("A layer ID is required for FeatureServer queries.", nameof(request));
        }

        var query = new FeatureServerQueryParams
        {
            Where = request.Filter,
            OutFields = request.OutFields is { Count: > 0 } ? string.Join(",", request.OutFields) : null,
            ObjectIds = ResolveObjectIds(request),
            OrderByFields = request.OrderBy,
            ReturnGeometry = request.ReturnGeometry,
            ResultOffset = request.Offset,
            ResultRecordCount = request.Limit,
            SpatialFilter = BuildSpatialFilter(request.Bbox),
            OutSR = ParseWkid(request.OutputCrs),
            InSR = ParseWkid(request.Bbox?.Crs),
        };

        return (request.Source.ServiceId, request.Source.LayerId.Value, query);
    }

    private static (string ServiceId, int LayerId) GetEditSource(FeatureEditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Source.ServiceId))
        {
            throw new ArgumentException("A service ID is required for FeatureServer edits.", nameof(request));
        }

        if (!request.Source.LayerId.HasValue)
        {
            throw new ArgumentException("A layer ID is required for FeatureServer edits.", nameof(request));
        }

        return (request.Source.ServiceId, request.Source.LayerId.Value);
    }

    private async Task<string?> ResolveObjectIdFieldAsync(
        string serviceId,
        int layerId,
        FeatureEditRequest request,
        CancellationToken ct)
    {
        if (!request.Updates.Any(HasFeatureObjectId))
        {
            return null;
        }

        var layer = await GetLayerInfoAsync(serviceId, layerId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(layer.ObjectIdField))
        {
            throw new InvalidOperationException(
                "FeatureServer layer metadata does not expose an object ID field required for shared updates.");
        }

        return layer.ObjectIdField;
    }

    private static FeatureServerEditRequest BuildFeatureServerEditRequest(
        FeatureEditRequest request,
        string? objectIdField)
    {
        return new FeatureServerEditRequest
        {
            Adds = request.Adds.Select(feature => ToFeatureServerFeature(feature, objectIdField: null)).ToList(),
            Updates = request.Updates.Select(feature => ToFeatureServerFeature(feature, objectIdField)).ToList(),
            Deletes = ResolveDeleteObjectIds(request),
            RollbackOnFailure = request.RollbackOnFailure,
            ForceWrite = request.ForceWrite,
        };
    }

    private static FeatureServerFeature ToFeatureServerFeature(FeatureEditFeature feature, string? objectIdField)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var attributes = feature.Attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone());
        if (!string.IsNullOrWhiteSpace(objectIdField) && !ContainsAttribute(attributes, objectIdField))
        {
            var objectId = ResolveFeatureObjectId(feature);
            if (objectId.HasValue)
            {
                attributes[objectIdField] = JsonSerializer.SerializeToElement(objectId.Value);
            }
        }

        return new FeatureServerFeature
        {
            Attributes = attributes,
            Geometry = feature.Geometry.HasValue ? feature.Geometry.Value.Clone() : null,
        };
    }

    private static List<long> ResolveDeleteObjectIds(FeatureEditRequest request)
    {
        var objectIds = new List<long>(request.DeleteObjectIds);
        foreach (var id in request.DeleteIds)
        {
            if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
            {
                throw new ArgumentException("FeatureServer feature deletes require numeric feature IDs.", nameof(request));
            }

            objectIds.Add(objectId);
        }

        return objectIds;
    }

    private static bool HasFeatureObjectId(FeatureEditFeature feature)
        => feature.ObjectId.HasValue || !string.IsNullOrWhiteSpace(feature.Id);

    private static long? ResolveFeatureObjectId(FeatureEditFeature feature)
    {
        if (feature.ObjectId.HasValue)
        {
            return feature.ObjectId.Value;
        }

        if (string.IsNullOrWhiteSpace(feature.Id))
        {
            return null;
        }

        if (long.TryParse(feature.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
        {
            return objectId;
        }

        throw new ArgumentException("FeatureServer feature updates require numeric feature IDs.");
    }

    private static bool ContainsAttribute(Dictionary<string, JsonElement> attributes, string name)
        => attributes.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));

    private static FeatureEditCapabilities BuildEditCapabilities(string? capabilities)
    {
        var tokens = ParseCapabilities(capabilities);
        var supportsAdds = tokens.Contains("CREATE") || tokens.Contains("EDITING");
        var supportsUpdates = tokens.Contains("UPDATE") || tokens.Contains("EDITING");
        var supportsDeletes = tokens.Contains("DELETE") || tokens.Contains("EDITING");

        return new FeatureEditCapabilities
        {
            SupportsAdds = supportsAdds,
            SupportsUpdates = supportsUpdates,
            SupportsDeletes = supportsDeletes,
            SupportsRollbackOnFailure = supportsAdds || supportsUpdates || supportsDeletes,
            NativeSurface = "GeoServices FeatureServer applyEdits"
        };
    }

    private static HashSet<string> ParseCapabilities(string? capabilities)
    {
        return string.IsNullOrWhiteSpace(capabilities)
            ? []
            : capabilities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(capability => capability.ToUpperInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private static void EnsureSupportedFilterLanguage(FeatureFilterLanguage language)
    {
        if (language is not FeatureFilterLanguage.ProviderDefault and not FeatureFilterLanguage.SqlWhere)
        {
            throw new NotSupportedException("FeatureServer queries support provider-default or SQL WHERE filters.");
        }
    }

    private static IReadOnlyList<long>? ResolveObjectIds(FeatureQueryRequest request)
    {
        if (request.ObjectIds is { Count: > 0 })
        {
            return request.ObjectIds;
        }

        if (request.FeatureIds is not { Count: > 0 })
        {
            return null;
        }

        var objectIds = new List<long>(request.FeatureIds.Count);
        foreach (var featureId in request.FeatureIds)
        {
            if (!long.TryParse(featureId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
            {
                throw new ArgumentException("FeatureServer feature IDs must be numeric object IDs.", nameof(request));
            }

            objectIds.Add(objectId);
        }

        return objectIds;
    }

    private static FeatureServerSpatialFilter? BuildSpatialFilter(FeatureBoundingBox? bbox)
    {
        if (bbox is null)
        {
            return null;
        }

        var geometry = JsonSerializer.Serialize(new
        {
            xmin = bbox.MinX,
            ymin = bbox.MinY,
            xmax = bbox.MaxX,
            ymax = bbox.MaxY
        });

        return new FeatureServerSpatialFilter
        {
            Geometry = geometry,
            GeometryType = "esriGeometryEnvelope",
            SpatialRel = SpatialRelationship.Intersects
        };
    }

    private static int? ParseWkid(string? crs)
    {
        if (string.IsNullOrWhiteSpace(crs))
        {
            return null;
        }

        var trimmed = crs.Trim();
        if (IsCrs84(trimmed))
        {
            return 4326;
        }

        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bareWkid))
        {
            return bareWkid;
        }

        var separatorIndex = Math.Max(trimmed.LastIndexOf(':'), trimmed.LastIndexOf('/'));
        return separatorIndex >= 0 &&
            int.TryParse(trimmed[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var wkid)
                ? wkid
                : null;
    }

    private static bool IsCrs84(string crs) =>
        string.Equals(crs, "CRS84", StringComparison.OrdinalIgnoreCase) ||
        crs.EndsWith("/CRS84", StringComparison.OrdinalIgnoreCase) ||
        crs.EndsWith(":CRS84", StringComparison.OrdinalIgnoreCase);

    private FeatureQueryResult ToFeatureQueryResult(FeatureServerQueryResponse response)
    {
        var features = response.Features?.Select(feature => ToFeatureRecord(feature, response.ObjectIdFieldName)).ToList()
            ?? [];

        return new FeatureQueryResult
        {
            ProviderName = ProviderName,
            Features = features,
            NumberReturned = features.Count,
            HasMoreResults = response.ExceededTransferLimit,
            ObjectIdFieldName = response.ObjectIdFieldName,
        };
    }

    private FeatureEditResponse ToFeatureEditResponse(FeatureServerEditResponse response)
    {
        return new FeatureEditResponse
        {
            ProviderName = ProviderName,
            AddResults = response.AddResults.Select(ToFeatureEditResult).ToList(),
            UpdateResults = response.UpdateResults.Select(ToFeatureEditResult).ToList(),
            DeleteResults = response.DeleteResults.Select(ToFeatureEditResult).ToList(),
        };
    }

    private static FeatureEditResult ToFeatureEditResult(FeatureServerEditResult result)
    {
        var id = result.ObjectId?.ToString(CultureInfo.InvariantCulture) ?? result.GlobalId;
        return new FeatureEditResult
        {
            Id = id,
            ObjectId = result.ObjectId,
            Succeeded = result.Success,
            Error = result.Error is not null ? ToFeatureEditError(result.Error) : null,
        };
    }

    private static FeatureEditError ToFeatureEditError(FeatureServerEditError error)
    {
        return new FeatureEditError
        {
            Code = error.Code,
            Message = error.Description ?? error.Message ?? "FeatureServer edit failed.",
        };
    }

    private static FeatureRecord ToFeatureRecord(FeatureServerFeature feature, string? objectIdFieldName)
    {
        return new FeatureRecord
        {
            Id = TryGetFeatureId(feature, objectIdFieldName),
            Attributes = feature.Attributes?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Clone()) ?? new Dictionary<string, JsonElement>(),
            Geometry = feature.Geometry.HasValue ? feature.Geometry.Value.Clone() : null,
        };
    }

    private static string? TryGetFeatureId(FeatureServerFeature feature, string? objectIdFieldName)
    {
        if (feature.Attributes is null || string.IsNullOrWhiteSpace(objectIdFieldName))
        {
            return null;
        }

        return feature.Attributes.TryGetValue(objectIdFieldName, out var idElement)
            ? JsonElementToString(idElement)
            : null;
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

    private static string SpatialRelToString(SpatialRelationship rel) => rel switch
    {
        SpatialRelationship.Contains => "esriSpatialRelContains",
        SpatialRelationship.Crosses => "esriSpatialRelCrosses",
        SpatialRelationship.EnvelopeIntersects => "esriSpatialRelEnvelopeIntersects",
        SpatialRelationship.IndexIntersects => "esriSpatialRelIndexIntersects",
        SpatialRelationship.Overlaps => "esriSpatialRelOverlaps",
        SpatialRelationship.Touches => "esriSpatialRelTouches",
        SpatialRelationship.Within => "esriSpatialRelWithin",
        _ => "esriSpatialRelIntersects",
    };

    private static string TimeRelationToString(TimeRelation rel) => rel switch
    {
        TimeRelation.AfterStartWithinEnd => "esriTimeRelationAfterStartWithinEnd",
        TimeRelation.Within => "esriTimeRelationWithin",
        _ => "esriTimeRelationOverlaps",
    };

    private static string BuildQueryString(List<(string Key, string? Value)> parameters)
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
}
