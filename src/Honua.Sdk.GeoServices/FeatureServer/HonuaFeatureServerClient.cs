// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// HTTP client implementation for the Honua FeatureServer (GeoServices) read/query API.
/// </summary>
public sealed class HonuaFeatureServerClient :
    IHonuaFeatureServerClient,
    IHonuaFeatureServerEditClient,
    IHonuaFeatureQueryClient,
    IHonuaFeatureEditClient,
    IHonuaFeatureDescriptorClient,
    IHonuaFeatureAttachmentClient
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
    private static readonly FeatureAttachmentCapabilities ProviderAttachmentCapabilities = new()
    {
        SupportsList = true,
        SupportsDownload = true,
        SupportsAdd = true,
        SupportsUpdate = true,
        SupportsDelete = true,
        NativeSurface = "GeoServices FeatureServer attachments"
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

    /// <inheritdoc />
    public FeatureAttachmentCapabilities AttachmentCapabilities => ProviderAttachmentCapabilities;

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
    public async Task<SourceDescriptor> GetDescriptorAsync(SourceDescriptor descriptor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var locator = descriptor.Locator;
        if (string.IsNullOrWhiteSpace(locator.ServiceId) || !locator.LayerId.HasValue)
        {
            throw new ArgumentException(
                "GeoServices descriptor discovery requires SourceDescriptor.Locator.ServiceId and LayerId.",
                nameof(descriptor));
        }

        var layer = await GetLayerInfoAsync(locator.ServiceId, locator.LayerId.Value, ct).ConfigureAwait(false);
        return descriptor with
        {
            Capabilities = BuildDiscoveredCapabilities(layer),
            Schema = BuildSourceSchema(layer),
        };
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
    public async Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
        FeatureAttachmentListRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (serviceId, layerId) = GetAttachmentSource(request.Source);
        var path = $"{ServicePath(serviceId)}/{layerId}/{request.ObjectId}/attachments?f=json";
        var body = await GetStringAsync(path, ct).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerAttachmentQueryResponse)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize attachment response.", body);

        return response.AttachmentInfos
            .Select(info => ToFeatureAttachmentInfo(info, request.Source, request.ObjectId))
            .ToList();
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Response ownership is transferred to FeatureAttachmentContent.Content.")]
    public async Task<FeatureAttachmentContent> DownloadAttachmentAsync(
        FeatureAttachmentDownloadRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (serviceId, layerId) = GetAttachmentSource(request.Source);
        var path = $"{ServicePath(serviceId)}/{layerId}/{request.ObjectId}/attachments/{request.AttachmentId}";
        var response = await _http.GetAsync(CreateRequestUri(path), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            response.Dispose();
            EnsureSuccess(response, body);
        }

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var contentLength = response.Content.Headers.ContentLength;
        var contentDispositionName = response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName;

        return new FeatureAttachmentContent
        {
            Info = new FeatureAttachmentInfo
            {
                Source = request.Source,
                ParentObjectId = request.ObjectId,
                AttachmentId = request.AttachmentId,
                Name = TrimHeaderFileName(contentDispositionName),
                ContentType = contentType,
                Size = contentLength,
            },
            Content = new ResponseOwningStream(stream, response),
        };
    }

    /// <inheritdoc />
    public async Task<FeatureAttachmentResult> AddAttachmentAsync(
        FeatureAttachmentAddRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (serviceId, layerId) = GetAttachmentSource(request.Source);
        var path = $"{ServicePath(serviceId)}/{layerId}/{request.ObjectId}/addAttachment";
        var response = await PostAttachmentAsync(
            path,
            request.Content,
            request.Name,
            request.ContentType,
            request.Keywords,
            ct).ConfigureAwait(false);

        return ToFeatureAttachmentResult(response.AddAttachmentResult)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "FeatureServer did not return addAttachmentResult.");
    }

    /// <inheritdoc />
    public async Task<FeatureAttachmentResult> UpdateAttachmentAsync(
        FeatureAttachmentUpdateRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (serviceId, layerId) = GetAttachmentSource(request.Source);
        var path = $"{ServicePath(serviceId)}/{layerId}/{request.ObjectId}/updateAttachment";
        var response = await PostAttachmentAsync(
            path,
            request.Content,
            request.Name,
            request.ContentType,
            request.Keywords,
            ct,
            ("attachmentId", request.AttachmentId.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);

        return ToFeatureAttachmentResult(response.UpdateAttachmentResult)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "FeatureServer did not return updateAttachmentResult.");
    }

    /// <inheritdoc />
    public async Task<FeatureAttachmentResult> DeleteAttachmentAsync(
        FeatureAttachmentDeleteRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (serviceId, layerId) = GetAttachmentSource(request.Source);
        var path = $"{ServicePath(serviceId)}/{layerId}/{request.ObjectId}/deleteAttachments";
        var body = await PostFormAsync(
            path,
            [
                ("f", "json"),
                ("attachmentIds", request.AttachmentId.ToString(CultureInfo.InvariantCulture))
            ],
            ct).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerAttachmentEditResponse)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize delete attachment response.", body);

        var result = response.DeleteAttachmentResults is { Count: > 0 } results
            ? results[0]
            : null;

        return ToFeatureAttachmentResult(result)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "FeatureServer did not return deleteAttachmentResults.");
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

            var count = currentQuery.ReturnIdsOnly is true
                ? page.ObjectIds?.Count ?? 0
                : page.Features?.Count ?? 0;
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

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "MultipartFormDataContent owns and disposes child HttpContent instances.")]
    private async Task<FeatureServerAttachmentEditResponse> PostAttachmentAsync(
        string path,
        Stream content,
        string name,
        string contentType,
        string? keywords,
        CancellationToken ct,
        params (string Key, string Value)[] extraParameters)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("json"), "f");

        if (!string.IsNullOrWhiteSpace(keywords))
        {
            form.Add(new StringContent(keywords), "keywords");
        }

        foreach (var parameter in extraParameters)
        {
            form.Add(new StringContent(parameter.Value), parameter.Key);
        }

        using var uploadStream = new NonDisposingStream(content);
        using var attachment = new StreamContent(uploadStream);
        attachment.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(attachment, "attachment", name);

        using var response = await _http.PostAsync(CreateRequestUri(path), form, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return JsonSerializer.Deserialize(body, FeatureServerJsonContext.Default.FeatureServerAttachmentEditResponse)
            ?? throw new HonuaFeatureServerException(HttpStatusCode.OK, "Failed to deserialize attachment edit response.", body);
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

        if (query.OutStatistics is not null)
            parameters.Add(("outStatistics", query.OutStatistics));

        if (query.GroupByFieldsForStatistics is not null)
            parameters.Add(("groupByFieldsForStatistics", query.GroupByFieldsForStatistics));

        if (query.Having is not null)
            parameters.Add(("having", query.Having));

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
            Time = BuildFeatureServerTime(request.TimeFilter),
            TimeRelation = ToFeatureServerTimeRelation(request.TimeFilter),
            OutStatistics = BuildFeatureServerOutStatistics(request.OutStatistics),
            GroupByFieldsForStatistics = request.GroupBy is { Count: > 0 } ? string.Join(",", request.GroupBy) : null,
            Having = request.Having,
            ReturnDistinctValues = request.ReturnDistinct,
            ReturnCountOnly = request.ReturnCountOnly,
            ReturnIdsOnly = request.ReturnIdsOnly,
            ReturnExtentOnly = request.ReturnExtentOnly,
            SpatialFilter = BuildSpatialFilter(request.SpatialFilter, request.Bbox),
            OutSR = ParseWkid(request.OutputCrs),
            InSR = ParseWkid(request.SpatialFilter?.Crs ?? request.Bbox?.Crs),
        };

        return (request.Source.ServiceId, request.Source.LayerId.Value, query);
    }

    private static List<string> BuildDiscoveredCapabilities(FeatureServerLayerInfo layer)
    {
        var capabilities = new HashSet<string>(
            FeatureProtocolCapabilities.DefaultsFor(FeatureProtocolIds.GeoServicesFeatureService),
            StringComparer.Ordinal);
        var advertised = SplitCapabilities(layer.Capabilities);
        var hasAdvertisedCapabilities = advertised.Count > 0;
        var supportsQuery = !hasAdvertisedCapabilities || advertised.Contains("Query");

        if (!supportsQuery)
        {
            capabilities.Remove(FeatureCapabilities.Query);
            capabilities.Remove(FeatureCapabilities.QueryAggregate);
            capabilities.Remove(FeatureCapabilities.QueryExtent);
            capabilities.Remove(FeatureCapabilities.QueryObjectIds);
            capabilities.Remove(FeatureCapabilities.TimeFilter);
            capabilities.Remove(FeatureCapabilities.SpatialRelationships);
            capabilities.Remove(FeatureCapabilities.Stream);
        }

        if (!layer.SupportsStatistics)
        {
            capabilities.Remove(FeatureCapabilities.QueryAggregate);
        }

        if (!layer.SupportsAdvancedQueries && hasAdvertisedCapabilities && !advertised.Contains("Query"))
        {
            capabilities.Remove(FeatureCapabilities.QueryExtent);
        }

        if (layer.TimeInfo is null)
        {
            capabilities.Remove(FeatureCapabilities.TimeFilter);
        }

        if (!LayerSupportsEdits(advertised, hasAdvertisedCapabilities))
        {
            capabilities.Remove(FeatureCapabilities.ApplyEdits);
        }

        if (layer.HasAttachments)
        {
            capabilities.Add(FeatureCapabilities.Attachments);
        }

        if (advertised.Contains("Sync"))
        {
            capabilities.Add(FeatureCapabilities.Offline);
        }

        return FeatureCapabilities.All.Where(capabilities.Contains).ToList();
    }

    private static SourceSchema BuildSourceSchema(FeatureServerLayerInfo layer)
    {
        var spatialReference = FormatSpatialReference(layer.SpatialReference);
        return new SourceSchema
        {
            Fields = layer.Fields?.Select(ToSourceField).ToList() ?? [],
            PrimaryKey = layer.ObjectIdField,
            ObjectIdField = layer.ObjectIdField,
            GlobalIdField = layer.GlobalIdField,
            GeometryType = ToSourceGeometryType(layer.GeometryType),
            Extent = ToFeatureBoundingBox(layer.Extent, spatialReference),
            SpatialReference = spatialReference,
            TimeField = layer.TimeInfo?.StartTimeField,
            TimeInfo = layer.TimeInfo is not null
                ? new SourceTimeInfo
                {
                    StartTimeField = layer.TimeInfo.StartTimeField,
                    EndTimeField = layer.TimeInfo.EndTimeField,
                    TrackIdField = layer.TimeInfo.TrackIdField,
                    TimeReference = JsonElementToRawText(layer.TimeInfo.TimeReference),
                }
                : null,
            EditCapabilities = BuildLayerEditCapabilities(layer),
            AttachmentCapabilities = BuildLayerAttachmentCapabilities(layer),
        };
    }

    private static SourceField ToSourceField(FeatureServerField field)
        => new()
        {
            Name = field.Name,
            Alias = field.Alias,
            Type = field.Type,
            Nullable = field.Nullable,
            Length = field.Length,
            Editable = field.Editable,
            Required = !field.Nullable,
            DefaultValue = CloneJsonElement(field.DefaultValue),
            Domain = CloneJsonElement(field.Domain),
        };

    private static FeatureEditCapabilities BuildLayerEditCapabilities(FeatureServerLayerInfo layer)
    {
        var advertised = SplitCapabilities(layer.Capabilities);
        var hasAdvertisedCapabilities = advertised.Count > 0;
        var supportsAnyEdit = LayerSupportsEdits(advertised, hasAdvertisedCapabilities);

        return new FeatureEditCapabilities
        {
            SupportsAdds = supportsAnyEdit && (!hasAdvertisedCapabilities || advertised.Contains("Create") || advertised.Contains("Editing")),
            SupportsUpdates = supportsAnyEdit && (!hasAdvertisedCapabilities || advertised.Contains("Update") || advertised.Contains("Editing")),
            SupportsDeletes = supportsAnyEdit && (!hasAdvertisedCapabilities || advertised.Contains("Delete") || advertised.Contains("Editing")),
            SupportsRollbackOnFailure = supportsAnyEdit,
            NativeSurface = ProviderEditCapabilities.NativeSurface,
            UnsupportedReason = supportsAnyEdit ? null : "GeoServices FeatureServer layer does not advertise edit capabilities.",
        };
    }

    private static FeatureAttachmentCapabilities BuildLayerAttachmentCapabilities(FeatureServerLayerInfo layer)
    {
        return new FeatureAttachmentCapabilities
        {
            SupportsList = layer.HasAttachments,
            SupportsDownload = layer.HasAttachments,
            SupportsAdd = layer.HasAttachments,
            SupportsUpdate = layer.HasAttachments,
            SupportsDelete = layer.HasAttachments,
            NativeSurface = ProviderAttachmentCapabilities.NativeSurface,
            UnsupportedReason = layer.HasAttachments
                ? null
                : "GeoServices FeatureServer layer does not advertise attachment support.",
        };
    }

    private static bool LayerSupportsEdits(HashSet<string> advertised, bool hasAdvertisedCapabilities)
        => !hasAdvertisedCapabilities ||
           advertised.Contains("Create") ||
           advertised.Contains("Update") ||
           advertised.Contains("Delete") ||
           advertised.Contains("Editing");

    private static HashSet<string> SplitCapabilities(string? capabilities)
        => string.IsNullOrWhiteSpace(capabilities)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : capabilities
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static FeatureSpatialGeometryType ToSourceGeometryType(string? geometryType)
        => geometryType switch
        {
            "esriGeometryPoint" => FeatureSpatialGeometryType.Point,
            "esriGeometryEnvelope" => FeatureSpatialGeometryType.Envelope,
            "esriGeometryMultipoint" => FeatureSpatialGeometryType.MultiPoint,
            "esriGeometryPolyline" => FeatureSpatialGeometryType.Polyline,
            "esriGeometryPolygon" => FeatureSpatialGeometryType.Polygon,
            _ => FeatureSpatialGeometryType.Unspecified,
        };

    private static FeatureBoundingBox? ToFeatureBoundingBox(FeatureServerExtent? extent, string? fallbackCrs)
    {
        if (extent is null)
        {
            return null;
        }

        return new FeatureBoundingBox
        {
            MinX = extent.Xmin,
            MinY = extent.Ymin,
            MaxX = extent.Xmax,
            MaxY = extent.Ymax,
            Crs = FormatSpatialReference(extent.SpatialReference) ?? fallbackCrs,
        };
    }

    private static string? FormatSpatialReference(FeatureServerSpatialReference? spatialReference)
        => spatialReference?.Wkid > 0
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"EPSG:{spatialReference.Wkid}")
            : null;

    private static JsonElement? CloneJsonElement(JsonElement? element)
        => element.HasValue ? element.Value.Clone() : null;

    private static string? JsonElementToRawText(JsonElement? element)
        => element.HasValue && element.Value.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? element.Value.GetRawText()
            : null;

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

    private static (string ServiceId, int LayerId) GetAttachmentSource(FeatureSource source)
    {
        if (string.IsNullOrWhiteSpace(source.ServiceId))
        {
            throw new ArgumentException("A service ID is required for FeatureServer attachments.", nameof(source));
        }

        if (!source.LayerId.HasValue)
        {
            throw new ArgumentException("A layer ID is required for FeatureServer attachments.", nameof(source));
        }

        return (source.ServiceId, source.LayerId.Value);
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

    private static FeatureServerSpatialFilter? BuildSpatialFilter(
        FeatureSpatialFilter? spatialFilter,
        FeatureBoundingBox? bbox)
    {
        EnsureSingleSpatialFilter(spatialFilter, bbox);

        if (spatialFilter is not null)
        {
            EnsureSpatialGeometryObject(spatialFilter.Geometry);
            return new FeatureServerSpatialFilter
            {
                Geometry = spatialFilter.Geometry.GetRawText(),
                GeometryType = ToFeatureServerGeometryType(spatialFilter),
                SpatialRel = ToFeatureServerSpatialRelationship(spatialFilter.Relationship)
            };
        }

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

    private static void EnsureSingleSpatialFilter(FeatureSpatialFilter? spatialFilter, FeatureBoundingBox? bbox)
    {
        if (spatialFilter is not null && bbox is not null)
        {
            throw new ArgumentException("Feature query cannot specify both Bbox and SpatialFilter.");
        }
    }

    private static void EnsureSpatialGeometryObject(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Feature query spatial filter geometry must be a JSON object.", nameof(geometry));
        }
    }

    private static string ToFeatureServerGeometryType(FeatureSpatialFilter spatialFilter)
    {
        var inferred = InferSpatialGeometryType(spatialFilter.Geometry);
        var geometryType = spatialFilter.GeometryType == FeatureSpatialGeometryType.Unspecified
            ? inferred
            : spatialFilter.GeometryType;

        if (geometryType != inferred)
        {
            throw new ArgumentException(
                "Feature query spatial filter geometry type does not match the geometry object.",
                nameof(spatialFilter));
        }

        return geometryType switch
        {
            FeatureSpatialGeometryType.Point => "esriGeometryPoint",
            FeatureSpatialGeometryType.Envelope => "esriGeometryEnvelope",
            FeatureSpatialGeometryType.MultiPoint => "esriGeometryMultipoint",
            FeatureSpatialGeometryType.Polyline => "esriGeometryPolyline",
            FeatureSpatialGeometryType.Polygon => "esriGeometryPolygon",
            _ => throw new ArgumentOutOfRangeException(
                nameof(spatialFilter),
                spatialFilter.GeometryType,
                "Feature spatial filter geometry type is not supported by FeatureServer shared queries."),
        };
    }

    private static FeatureSpatialGeometryType InferSpatialGeometryType(JsonElement geometry)
    {
        EnsureSpatialGeometryObject(geometry);

        if (geometry.TryGetProperty("x", out _) && geometry.TryGetProperty("y", out _))
        {
            return FeatureSpatialGeometryType.Point;
        }

        if (geometry.TryGetProperty("xmin", out _) &&
            geometry.TryGetProperty("ymin", out _) &&
            geometry.TryGetProperty("xmax", out _) &&
            geometry.TryGetProperty("ymax", out _))
        {
            return FeatureSpatialGeometryType.Envelope;
        }

        if (geometry.TryGetProperty("points", out _))
        {
            return FeatureSpatialGeometryType.MultiPoint;
        }

        if (geometry.TryGetProperty("paths", out _))
        {
            return FeatureSpatialGeometryType.Polyline;
        }

        if (geometry.TryGetProperty("rings", out _))
        {
            return FeatureSpatialGeometryType.Polygon;
        }

        throw new ArgumentException(
            "Feature query spatial filter geometry type could not be inferred from the geometry object.",
            nameof(geometry));
    }

    private static SpatialRelationship ToFeatureServerSpatialRelationship(FeatureSpatialRelationship relationship)
        => relationship switch
        {
            FeatureSpatialRelationship.Unspecified => SpatialRelationship.Intersects,
            FeatureSpatialRelationship.Intersects => SpatialRelationship.Intersects,
            FeatureSpatialRelationship.Within => SpatialRelationship.Within,
            FeatureSpatialRelationship.Contains => SpatialRelationship.Contains,
            FeatureSpatialRelationship.EnvelopeIntersects => SpatialRelationship.EnvelopeIntersects,
            FeatureSpatialRelationship.Crosses => SpatialRelationship.Crosses,
            FeatureSpatialRelationship.Touches => SpatialRelationship.Touches,
            FeatureSpatialRelationship.Overlaps => SpatialRelationship.Overlaps,
            FeatureSpatialRelationship.IndexIntersects => SpatialRelationship.IndexIntersects,
            _ => throw new ArgumentOutOfRangeException(
                nameof(relationship),
                relationship,
                "Feature spatial relationship is not supported by FeatureServer shared queries."),
        };

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
        if (separatorIndex < 0)
        {
            return null;
        }

        return int.TryParse(trimmed[(separatorIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var wkid)
            ? wkid
            : null;
    }

    private static string? BuildFeatureServerTime(FeatureTimeFilter? timeFilter)
    {
        if (timeFilter is null)
        {
            return null;
        }

        if (timeFilter.End is not { } end)
        {
            return timeFilter.Start.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        }

        if (end < timeFilter.Start)
        {
            throw new ArgumentException("Feature query time filter end must be greater than or equal to start.", nameof(timeFilter));
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{timeFilter.Start.ToUnixTimeMilliseconds()},{end.ToUnixTimeMilliseconds()}");
    }

    private static TimeRelation? ToFeatureServerTimeRelation(FeatureTimeFilter? timeFilter)
    {
        return timeFilter?.Relation switch
        {
            FeatureTimeRelation.Overlaps => TimeRelation.Overlaps,
            FeatureTimeRelation.AfterStartWithinEnd => TimeRelation.AfterStartWithinEnd,
            FeatureTimeRelation.Within => TimeRelation.Within,
            _ => null,
        };
    }

    private static string? BuildFeatureServerOutStatistics(IReadOnlyList<FeatureQueryStatistic>? statistics)
    {
        if (statistics is not { Count: > 0 })
        {
            return null;
        }

        var definitions = statistics.Select(statistic => new FeatureServerStatisticDefinition
        {
            StatisticType = ToFeatureServerStatisticType(statistic.StatisticType),
            OnStatisticField = statistic.OnField,
            OutStatisticFieldName = statistic.OutField,
        }).ToArray();

        return JsonSerializer.Serialize(
            definitions,
            FeatureServerJsonContext.Default.FeatureServerStatisticDefinitionArray);
    }

    private static string ToFeatureServerStatisticType(FeatureStatisticType statisticType) => statisticType switch
    {
        FeatureStatisticType.Count => "count",
        FeatureStatisticType.Sum => "sum",
        FeatureStatisticType.Min => "min",
        FeatureStatisticType.Max => "max",
        FeatureStatisticType.Average => "avg",
        FeatureStatisticType.StandardDeviation => "stddev",
        FeatureStatisticType.Variance => "var",
        _ => throw new ArgumentOutOfRangeException(
            nameof(statisticType),
            statisticType,
            "Feature statistic type must be specified."),
    };

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
            NumberMatched = response.Count,
            NumberReturned = features.Count,
            ObjectIds = response.ObjectIds ?? [],
            Extent = response.Extent is not null ? ToFeatureBoundingBox(response.Extent) : null,
            HasMoreResults = response.ExceededTransferLimit,
            ObjectIdFieldName = response.ObjectIdFieldName,
        };
    }

    private static FeatureBoundingBox ToFeatureBoundingBox(FeatureServerExtent extent)
    {
        return new FeatureBoundingBox
        {
            MinX = extent.Xmin,
            MinY = extent.Ymin,
            MaxX = extent.Xmax,
            MaxY = extent.Ymax,
            Crs = extent.SpatialReference?.Wkid > 0
                ? string.Create(CultureInfo.InvariantCulture, $"EPSG:{extent.SpatialReference.Wkid}")
                : null,
        };
    }

    private static FeatureAttachmentInfo ToFeatureAttachmentInfo(
        FeatureServerAttachmentInfo info,
        FeatureSource source,
        long fallbackParentObjectId)
    {
        return new FeatureAttachmentInfo
        {
            Source = source,
            ParentObjectId = info.ParentObjectId ?? fallbackParentObjectId,
            AttachmentId = info.Id,
            GlobalId = info.GlobalId,
            Name = info.Name,
            ContentType = info.ContentType,
            Size = info.Size,
            Keywords = info.Keywords,
            Url = info.Url,
        };
    }

    private static FeatureAttachmentResult? ToFeatureAttachmentResult(FeatureServerAttachmentEditResult? result)
    {
        return result is null
            ? null
            : new FeatureAttachmentResult
            {
                AttachmentId = result.ObjectId,
                GlobalId = result.GlobalId,
                Succeeded = result.Success,
                Error = result.Error is not null ? ToFeatureEditError(result.Error) : null,
            };
    }

    private static string? TrimHeaderFileName(string? fileName)
        => string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim('"');

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
