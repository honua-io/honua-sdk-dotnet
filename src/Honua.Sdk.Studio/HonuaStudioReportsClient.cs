// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Models;

namespace Honua.Sdk.Studio;

/// <summary>
/// HTTP client implementation for Console Studio analysis reports.
/// </summary>
public sealed class HonuaStudioReportsClient : IHonuaStudioReportsClient
{
    private const string BasePath = "/api/v1/analysis/reports";
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioReportsClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaStudioReportsClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<HonuaAnalysisReport> GetReportAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureId(jobId, nameof(jobId));

        using var response = await _http.GetAsync(
            CreateRequestUri($"{BasePath}/{Uri.EscapeDataString(jobId)}"),
            cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        HonuaAnalysisReport? report;
        try
        {
            report = JsonSerializer.Deserialize(body, StudioJsonContext.Default.HonuaAnalysisReport);
        }
        catch (JsonException ex)
        {
            throw new HonuaStudioContractException(
                "Failed to deserialize the analysis report envelope.",
                "GetReport",
                body,
                ex);
        }

        return report
            ?? throw new HonuaStudioContractException(
                "Server returned an empty analysis report envelope.",
                "GetReport",
                body);
    }

    /// <inheritdoc />
    public async Task<HonuaRenderedReport> RenderReportAsync(
        string jobId,
        HonuaReportRenderFormat format,
        CancellationToken cancellationToken = default)
    {
        EnsureId(jobId, nameof(jobId));

        var (formatValue, acceptMediaType) = format switch
        {
            HonuaReportRenderFormat.Markdown => ("md", "text/markdown"),
            HonuaReportRenderFormat.Html => ("html", "text/html"),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported render format.")
        };

        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            CreateRequestUri($"{BasePath}/{Uri.EscapeDataString(jobId)}/render?format={formatValue}"));
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptMediaType));

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? acceptMediaType;
        return new HonuaRenderedReport
        {
            JobId = jobId,
            Format = format,
            MediaType = mediaType,
            Content = body
        };
    }

    private static HonuaStudioApiException CreateApiException(HttpStatusCode statusCode, string body)
    {
        if (TryParseProblem(body, out var problem) && problem is not null)
        {
            var message = problem.Detail ?? problem.Title ?? "Studio API request failed.";
            return new HonuaStudioApiException(statusCode, message, body, problem.Title, problem.Detail);
        }

        return new HonuaStudioApiException(statusCode, "Studio API request failed.", body);
    }

    private static bool TryParseProblem(string body, out StudioProblem? problem)
    {
        try
        {
            problem = JsonSerializer.Deserialize(body, StudioJsonContext.Default.StudioProblem);
            return problem is not null;
        }
        catch (JsonException)
        {
            // Body is not a parseable problem-details document; the caller falls back to a generic message.
            problem = null;
            return false;
        }
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private static void EnsureId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier must be supplied.", parameterName);
        }
    }
}
