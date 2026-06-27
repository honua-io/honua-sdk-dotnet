// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Processes.Exceptions;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes;

/// <summary>
/// HTTP client implementation for OGC API Processes.
/// </summary>
public sealed class HonuaProcessesClient : IHonuaProcessesClient
{
    private const string BasePath = "/ogc/processes";
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesClient"/> class.
    /// </summary>
    public HonuaProcessesClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public Task<HonuaProcessesLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default)
        => GetAsync($"{BasePath}?f=json", ProcessesJsonContext.Default.HonuaProcessesLandingPage, cancellationToken);

    /// <inheritdoc />
    public Task<HonuaProcessesConformance> GetConformanceAsync(CancellationToken cancellationToken = default)
        => GetAsync($"{BasePath}/conformance?f=json", ProcessesJsonContext.Default.HonuaProcessesConformance, cancellationToken);

    /// <inheritdoc />
    public Task<HonuaProcessList> ListProcessesAsync(CancellationToken cancellationToken = default)
        => GetAsync($"{BasePath}/processes", ProcessesJsonContext.Default.HonuaProcessList, cancellationToken);

    /// <inheritdoc />
    public Task<HonuaProcessDescription> GetProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        EnsureId(processId, nameof(processId));
        return GetAsync(
            $"{BasePath}/processes/{Uri.EscapeDataString(processId)}",
            ProcessesJsonContext.Default.HonuaProcessDescription,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<HonuaProcessJobStatus> SubmitJobAsync(
        string processId,
        IReadOnlyDictionary<string, JsonElement> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        return SubmitJobAsync(
            processId,
            new HonuaProcessExecuteRequest
            {
                Inputs = HonuaProcessExecuteInputs.FromDirectInputs(inputs)
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HonuaProcessJobStatus> SubmitJobAsync(
        string processId,
        HonuaProcessExecuteRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureId(processId, nameof(processId));
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            CreateRequestUri($"{BasePath}/processes/{Uri.EscapeDataString(processId)}/execution"))
        {
            Content = JsonContent.Create(request, ProcessesJsonContext.Default.HonuaProcessExecuteRequest)
        };
        message.Headers.TryAddWithoutValidation("Prefer", "respond-async");

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(
            response,
            ProcessesJsonContext.Default.HonuaProcessJobStatus,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<HonuaProcessJobList> ListJobsAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var query = limit.HasValue
            ? $"?limit={Uri.EscapeDataString(limit.Value.ToString(CultureInfo.InvariantCulture))}"
            : string.Empty;
        return GetAsync($"{BasePath}/jobs{query}", ProcessesJsonContext.Default.HonuaProcessJobList, cancellationToken);
    }

    /// <inheritdoc />
    public Task<HonuaProcessJobStatus> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureId(jobId, nameof(jobId));
        return GetAsync(
            $"{BasePath}/jobs/{Uri.EscapeDataString(jobId)}",
            ProcessesJsonContext.Default.HonuaProcessJobStatus,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HonuaProcessJobStatus> DismissJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureId(jobId, nameof(jobId));
        using var response = await _http.DeleteAsync(
            CreateRequestUri($"{BasePath}/jobs/{Uri.EscapeDataString(jobId)}"), cancellationToken).ConfigureAwait(false);
        return await ReadAsync(
            response,
            ProcessesJsonContext.Default.HonuaProcessJobStatus,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<HonuaProcessResults> GetJobResultsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureId(jobId, nameof(jobId));
        return GetAsync(
            $"{BasePath}/jobs/{Uri.EscapeDataString(jobId)}/results",
            ProcessesJsonContext.Default.HonuaProcessResults,
            cancellationToken);
    }

    private async Task<T> GetAsync<T>(
        string url,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        return await ReadAsync(response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException(response.StatusCode, body);
        }

        try
        {
            return JsonSerializer.Deserialize(body, typeInfo)
                ?? throw new HonuaProcessesException(response.StatusCode, "Server returned an empty OGC API Processes response.", body);
        }
        catch (JsonException ex)
        {
            throw new HonuaProcessesException(response.StatusCode, "Failed to deserialize OGC API Processes response.", body, ex);
        }
    }

    private static HonuaProcessesException CreateException(HttpStatusCode statusCode, string body)
    {
        // RFC 7807 problem details via the shared Abstractions parser.
        var message = Honua.Sdk.Abstractions.HonuaProblemDetailsParser.ResolveMessage(
            body, "OGC API Processes request failed.", out var problem);
        return new HonuaProcessesException(
            statusCode,
            message,
            body,
            problem?.Type,
            problem?.Title,
            problem?.Detail);
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
