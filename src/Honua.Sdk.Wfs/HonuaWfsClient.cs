// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using Honua.Sdk.Wfs.Exceptions;
using Honua.Sdk.Wfs.Formats;
using Honua.Sdk.Wfs.Models;
using Honua.Sdk.Wfs.Parsing;

namespace Honua.Sdk.Wfs;

/// <summary>
/// WFS 2.0 read/query client for Honua Server.
/// </summary>
public sealed class HonuaWfsClient : IHonuaWfsClient
{
    private static readonly ActivitySource ActivitySource = new("Honua.Sdk.Wfs");
    private static readonly GeoJsonFeatureCollectionHandler DefaultGeoJsonHandler = new();
    private const int MaxAutoPages = 100;

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth.</param>
    public HonuaWfsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<WfsCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("WFS GetCapabilities");
        activity?.SetTag("wfs.operation", "GetCapabilities");

        var url = BuildWfsUrl("GetCapabilities");
        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body, ct).ConfigureAwait(false);

        return WfsCapabilitiesParser.Parse(body);
    }

    /// <inheritdoc />
    public async Task<WfsFeatureTypeSchema> DescribeFeatureTypeAsync(string typeName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        using var activity = ActivitySource.StartActivity("WFS DescribeFeatureType");
        activity?.SetTag("wfs.operation", "DescribeFeatureType");
        activity?.SetTag("wfs.type_name", typeName);

        var url = BuildWfsUrl("DescribeFeatureType", ("TYPENAMES", typeName));
        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body, ct).ConfigureAwait(false);

        return WfsDescribeFeatureTypeParser.Parse(body);
    }

    /// <inheritdoc />
    public async Task<WfsFeatureCollection> GetFeaturesAsync(GetFeaturesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);
        activity?.SetTag("wfs.output_format", "application/geo+json");

        var url = BuildGetFeatureUrl(request, DefaultGeoJsonHandler.MediaType);
        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

        await EnsureGetFeatureSuccessAsync(response, ct).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var result = await DefaultGeoJsonHandler.ReadAsync(stream, ct).ConfigureAwait(false);

        activity?.SetTag("wfs.number_returned", result.NumberReturned);
        return result;
    }

    /// <inheritdoc />
    public async Task<TResult> GetFeaturesAsync<TResult>(
        GetFeaturesRequest request,
        IWfsOutputFormatHandler<TResult> handler,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        using var activity = ActivitySource.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);
        activity?.SetTag("wfs.output_format", handler.MediaType);

        var url = BuildGetFeatureUrl(request, handler.MediaType);
        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

        await EnsureGetFeatureSuccessAsync(response, ct).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await handler.ReadAsync(stream, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<long> GetFeatureCountAsync(string typeName, string? filter = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        using var activity = ActivitySource.StartActivity("WFS GetFeature (hits)");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", typeName);

        // Server returns XML wfs:FeatureCollection for RESULTTYPE=hits regardless of OUTPUTFORMAT.
        var parameters = new List<(string, string)>
        {
            ("TYPENAMES", typeName),
            ("RESULTTYPE", "hits"),
        };

        if (!string.IsNullOrEmpty(filter))
        {
            parameters.Add(("FILTER", filter));
        }

        var url = BuildWfsUrl("GetFeature", parameters.ToArray());
        var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body, ct).ConfigureAwait(false);

        return ParseHitsNumberMatched(body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WfsFeature> GetFeaturesAsyncEnumerable(
        GetFeaturesRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startIndex = request.StartIndex ?? 0;
        var pageCount = 0;

        while (pageCount < MaxAutoPages)
        {
            ct.ThrowIfCancellationRequested();

            var pageRequest = new GetFeaturesRequest
            {
                TypeNames = request.TypeNames,
                Count = request.Count,
                StartIndex = startIndex,
                SortBy = request.SortBy,
                Filter = request.Filter,
                Bbox = request.Bbox,
                ResourceId = request.ResourceId,
                PropertyName = request.PropertyName,
                SrsName = request.SrsName,
            };

            var page = await GetFeaturesAsync(pageRequest, ct).ConfigureAwait(false);

            foreach (var feature in page.Features)
            {
                yield return feature;
            }

            if (page.NumberReturned == 0)
            {
                yield break;
            }

            startIndex += page.NumberReturned;

            if (page.NumberMatched.HasValue && startIndex >= page.NumberMatched.Value)
            {
                yield break;
            }

            pageCount++;
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use manual paging with GetFeaturesAsync for larger result sets.");
    }

    // ── URL building ─────────────────────────────────────────────────────

    private string BuildWfsUrl(string requestType, params (string Key, string Value)[] extra)
    {
        var sb = new StringBuilder("/wfs?SERVICE=WFS&VERSION=2.0.0&REQUEST=");
        sb.Append(Uri.EscapeDataString(requestType));

        foreach (var (key, value) in extra)
        {
            sb.Append('&');
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        return sb.ToString();
    }

    private string BuildGetFeatureUrl(GetFeaturesRequest request, string mediaType)
    {
        var parameters = new List<(string, string)>
        {
            ("TYPENAMES", request.TypeNames),
            ("OUTPUTFORMAT", mediaType),
        };

        if (request.Count.HasValue)
            parameters.Add(("COUNT", request.Count.Value.ToString(CultureInfo.InvariantCulture)));

        if (request.StartIndex.HasValue)
            parameters.Add(("STARTINDEX", request.StartIndex.Value.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrEmpty(request.SortBy))
            parameters.Add(("SORTBY", request.SortBy));

        if (!string.IsNullOrEmpty(request.Filter))
            parameters.Add(("FILTER", request.Filter));

        if (request.Bbox is not null)
            parameters.Add(("BBOX", request.Bbox.ToQueryValue()));

        if (!string.IsNullOrEmpty(request.ResourceId))
            parameters.Add(("RESOURCEID", request.ResourceId));

        if (!string.IsNullOrEmpty(request.PropertyName))
            parameters.Add(("PROPERTYNAME", request.PropertyName));

        if (!string.IsNullOrEmpty(request.SrsName))
            parameters.Add(("SRSNAME", request.SrsName));

        return BuildWfsUrl("GetFeature", parameters.ToArray());
    }

    // ── Response validation ──────────────────────────────────────────────

    private static Task EnsureSuccessAsync(HttpResponseMessage response, string body, CancellationToken ct)
    {
        // Check for OGC ExceptionReport even on 200 responses
        var exceptionReport = WfsExceptionParser.TryParse(body);
        if (exceptionReport is not null)
        {
            throw new HonuaWfsException(
                response.StatusCode,
                exceptionReport.ExceptionText ?? $"WFS exception: {exceptionReport.ExceptionCode}",
                body,
                exceptionReport.ExceptionCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HonuaWfsException(
                response.StatusCode,
                $"WFS request failed with status {(int)response.StatusCode}.",
                body);
        }

        return Task.CompletedTask;
    }

    private async Task EnsureGetFeatureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        // WFS servers may return XML ExceptionReport even when GeoJSON was requested.
        // Check Content-Type to detect this.
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        if (!response.IsSuccessStatusCode ||
            contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var exceptionReport = WfsExceptionParser.TryParse(body);
            if (exceptionReport is not null)
            {
                throw new HonuaWfsException(
                    response.StatusCode,
                    exceptionReport.ExceptionText ?? $"WFS exception: {exceptionReport.ExceptionCode}",
                    body,
                    exceptionReport.ExceptionCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HonuaWfsException(
                    response.StatusCode,
                    $"WFS request failed with status {(int)response.StatusCode}.",
                    body);
            }
        }
    }

    // ── Hits response parsing ────────────────────────────────────────────

    private static long ParseHitsNumberMatched(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null) return 0;

        var attr = root.Attribute("numberMatched")?.Value;
        return attr is not null &&
               long.TryParse(attr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : 0;
    }
}
