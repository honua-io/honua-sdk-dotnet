// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Abstractions.Console.Share;
using Honua.Sdk.ConsoleShare.Exceptions;
using Honua.Sdk.ConsoleShare.Models;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// HTTP client implementation for the Console Share export-definition,
/// export-run, and Share-traffic admin surface.
/// </summary>
public sealed class HonuaConsoleShareExportClient : IHonuaConsoleShareExportClient
{
    private const string ExportsPath = "/api/v1/admin/share/exports";
    private const string TrafficPath = "/api/v1/admin/share/traffic";
    private const string ServicesPath = "/api/v1/admin/services";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaConsoleShareExportClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaConsoleShareExportClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportDefinitionPage> ListExportDefinitionsAsync(HonuaShareExportDefinitionQuery? query = null, CancellationToken cancellationToken = default)
    {
        var builder = new QueryBuilder();
        if (query is not null)
        {
            builder.Add("serviceName", query.ServiceName);
            builder.Add("resourceId", query.ResourceId);
            builder.Add("layerId", query.LayerId);
            builder.Add("destinationType", query.DestinationType?.ToString());
            builder.Add("scheduleState", query.ScheduleState?.ToString());
            builder.Add("cursor", query.Cursor);
            builder.Add("limit", query.Limit);
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, RelativeUri(ExportsPath, builder));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportDefinitionPage,
            "ListExportDefinitions",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportDefinition> CreateExportDefinitionAsync(HonuaShareExportDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, RelativeUri(ExportsPath))
        {
            Content = JsonContent(request, ConsoleShareJsonContext.Default.HonuaShareExportDefinitionRequest)
        };
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportDefinition,
            "CreateExportDefinition",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportDefinition> GetExportDefinitionAsync(string exportId, CancellationToken cancellationToken = default)
    {
        EnsureId(exportId, nameof(exportId));

        using var message = new HttpRequestMessage(HttpMethod.Get, ExportPath(exportId));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportDefinition,
            "GetExportDefinition",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportDefinition> UpdateExportDefinitionAsync(string exportId, HonuaShareExportDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(exportId, nameof(exportId));
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Put, ExportPath(exportId))
        {
            Content = JsonContent(request, ConsoleShareJsonContext.Default.HonuaShareExportDefinitionRequest)
        };
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportDefinition,
            "UpdateExportDefinition",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteExportDefinitionAsync(string exportId, CancellationToken cancellationToken = default)
    {
        EnsureId(exportId, nameof(exportId));

        using var message = new HttpRequestMessage(HttpMethod.Delete, ExportPath(exportId));
        await SendForNoContentAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportRun> TriggerExportAsync(string exportId, CancellationToken cancellationToken = default)
    {
        EnsureId(exportId, nameof(exportId));

        using var message = new HttpRequestMessage(HttpMethod.Post, ExportPath(exportId, "trigger"));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportRun,
            "TriggerExport",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<HonuaShareExportDefinition> PauseExportAsync(string exportId, CancellationToken cancellationToken = default)
        => SetScheduleStateAsync(exportId, "pause", "PauseExport", cancellationToken);

    /// <inheritdoc />
    public Task<HonuaShareExportDefinition> ResumeExportAsync(string exportId, CancellationToken cancellationToken = default)
        => SetScheduleStateAsync(exportId, "resume", "ResumeExport", cancellationToken);

    private async Task<HonuaShareExportDefinition> SetScheduleStateAsync(string exportId, string action, string operation, CancellationToken cancellationToken)
    {
        EnsureId(exportId, nameof(exportId));

        using var message = new HttpRequestMessage(HttpMethod.Post, ExportPath(exportId, action));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportDefinition,
            operation,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportRunPage> ListExportRunsAsync(string exportId, string? cursor = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        EnsureId(exportId, nameof(exportId));

        var builder = new QueryBuilder();
        builder.Add("cursor", cursor);
        builder.Add("limit", limit);

        using var message = new HttpRequestMessage(HttpMethod.Get, ExportPath(exportId, "runs", builder));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportRunPage,
            "ListExportRuns",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareExportRun> GetExportRunAsync(string exportId, string runId, CancellationToken cancellationToken = default)
    {
        EnsureId(exportId, nameof(exportId));
        EnsureId(runId, nameof(runId));

        var path = $"{ExportsPath}/{Uri.EscapeDataString(exportId)}/runs/{Uri.EscapeDataString(runId)}";
        using var message = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.RelativeOrAbsolute));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareExportRun,
            "GetExportRun",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareTrafficSummary> GetTrafficSummaryAsync(HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default)
    {
        var builder = TrafficSummaryQuery(query);
        using var message = new HttpRequestMessage(HttpMethod.Get, RelativeUri(TrafficPath, builder));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareTrafficSummary,
            "GetTrafficSummary",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareTrafficSeries> GetTrafficSeriesAsync(HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default)
    {
        var builder = TrafficSeriesQuery(query);
        using var message = new HttpRequestMessage(HttpMethod.Get, RelativeUri($"{TrafficPath}/series", builder));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareTrafficSeries,
            "GetTrafficSeries",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareTrafficSummary> GetItemTrafficSummaryAsync(string serviceName, int layerId, string? resourceId = null, HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default)
    {
        EnsureId(serviceName, nameof(serviceName));

        var builder = TrafficSummaryQuery(query);
        builder.Add("resourceId", resourceId);
        using var message = new HttpRequestMessage(HttpMethod.Get, RelativeUri(ItemTrafficPath(serviceName, layerId), builder));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareTrafficSummary,
            "GetItemTrafficSummary",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareTrafficSeries> GetItemTrafficSeriesAsync(string serviceName, int layerId, string? resourceId = null, HonuaShareTrafficQuery? query = null, CancellationToken cancellationToken = default)
    {
        EnsureId(serviceName, nameof(serviceName));

        var builder = TrafficSeriesQuery(query);
        builder.Add("resourceId", resourceId);
        using var message = new HttpRequestMessage(HttpMethod.Get, RelativeUri($"{ItemTrafficPath(serviceName, layerId)}/series", builder));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareTrafficSeries,
            "GetItemTrafficSeries",
            cancellationToken).ConfigureAwait(false);
    }

    private static QueryBuilder TrafficSummaryQuery(HonuaShareTrafficQuery? query)
    {
        var builder = new QueryBuilder();
        if (query is not null)
        {
            builder.Add("periodStart", query.PeriodStart);
            builder.Add("periodEnd", query.PeriodEnd);
        }

        return builder;
    }

    private static QueryBuilder TrafficSeriesQuery(HonuaShareTrafficQuery? query)
    {
        var builder = TrafficSummaryQuery(query);
        builder.Add("bucketMinutes", query?.BucketMinutes);
        return builder;
    }

    private async Task<T> SendForJsonAsync<T>(
        HttpRequestMessage message,
        JsonTypeInfo<T> typeInfo,
        string operation,
        CancellationToken cancellationToken)
        where T : class
    {
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (JsonException ex)
        {
            throw new HonuaConsoleShareContractException(
                $"Failed to deserialize the {operation} response envelope.",
                operation,
                body,
                ex);
        }

        return value
            ?? throw new HonuaConsoleShareContractException(
                $"Server returned an empty {operation} response envelope.",
                operation,
                body);
    }

    private async Task SendForNoContentAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }
    }

    private static StringContent JsonContent<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static HonuaConsoleShareApiException CreateApiException(HttpStatusCode statusCode, string body)
    {
        if (TryParseProblem(body, out var problem) && problem is not null)
        {
            var message = problem.Detail ?? problem.Title ?? "Console Share export API request failed.";
            return new HonuaConsoleShareApiException(statusCode, message, body, problem.Title, problem.Detail);
        }

        return new HonuaConsoleShareApiException(statusCode, "Console Share export API request failed.", body);
    }

    private static bool TryParseProblem(string body, out ConsoleShareProblem? problem)
    {
        try
        {
            problem = JsonSerializer.Deserialize(body, ConsoleShareJsonContext.Default.ConsoleShareProblem);
            return problem is not null;
        }
        catch (JsonException)
        {
            // Body is not a parseable problem-details document; the caller falls back to a generic message.
            problem = null;
            return false;
        }
    }

    private static Uri ExportPath(string exportId, string? suffix = null, QueryBuilder? query = null)
    {
        var path = $"{ExportsPath}/{Uri.EscapeDataString(exportId)}";
        if (suffix is not null)
        {
            path = $"{path}/{suffix}";
        }

        return RelativeUri(path, query);
    }

    private static string ItemTrafficPath(string serviceName, int layerId)
        => $"{ServicesPath}/{Uri.EscapeDataString(serviceName)}/layers/{layerId.ToString(CultureInfo.InvariantCulture)}/share/traffic";

    private static Uri RelativeUri(string path, QueryBuilder? query = null)
    {
        var suffix = query?.ToQueryString() ?? string.Empty;
        return new Uri(path + suffix, UriKind.RelativeOrAbsolute);
    }

    private static void EnsureId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier must be supplied.", parameterName);
        }
    }

    private sealed class QueryBuilder
    {
        private readonly List<string> _pairs = [];

        public void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _pairs.Add($"{name}={Uri.EscapeDataString(value)}");
            }
        }

        public void Add(string name, int? value)
        {
            if (value is { } v)
            {
                _pairs.Add($"{name}={v.ToString(CultureInfo.InvariantCulture)}");
            }
        }

        public void Add(string name, DateTimeOffset? value)
        {
            if (value is { } v)
            {
                _pairs.Add($"{name}={Uri.EscapeDataString(v.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture))}");
            }
        }

        public string ToQueryString()
            => _pairs.Count == 0 ? string.Empty : "?" + string.Join("&", _pairs);
    }
}
