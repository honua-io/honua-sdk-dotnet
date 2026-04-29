// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Spec.Models;

namespace Honua.Sdk.Spec;

/// <summary>
/// HTTP client implementation for the Honua spec workspace REST API.
/// </summary>
public sealed class HonuaSpecClient : IHonuaSpecClient
{
    private const string SpecPrefix = "/v1/spec";
    private const string ApplyTokenHeader = "X-Spec-Apply-Token";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaSpecClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<SpecValidateResponse> ValidateAsync(SpecValidateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await PostJsonAsync(
            $"{SpecPrefix}/validate",
            request,
            SpecJsonContext.Default.SpecValidateRequest,
            SpecJsonContext.Default.SpecValidateResponse,
            ct).ConfigureAwait(false);

        return response ?? throw new HonuaSpecException(System.Net.HttpStatusCode.OK, "Server returned null validation response.");
    }

    /// <inheritdoc />
    public async Task<SpecPlanResponse> PlanAsync(SpecDocumentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var response = await PostJsonAsync(
            $"{SpecPrefix}/plan",
            request,
            SpecJsonContext.Default.SpecDocumentRequest,
            SpecJsonContext.Default.SpecPlanResponse,
            ct).ConfigureAwait(false);

        return response ?? throw new HonuaSpecException(System.Net.HttpStatusCode.OK, "Server returned null plan response.");
    }

    /// <inheritdoc />
    public async Task<SpecApplyStream> ApplyAsync(SpecDocumentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var content = JsonContent.Create(request, SpecJsonContext.Default.SpecDocumentRequest);
        using var message = new HttpRequestMessage(HttpMethod.Post, CreateRequestUri($"{SpecPrefix}/apply"))
        {
            Content = content
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        var disposeResponse = true;
        try
        {
            var applyToken = TryGetHeader(response, ApplyTokenHeader);
            await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
            var events = ReadApplyEventsAsync(response, ct);
            disposeResponse = false;
            return new SpecApplyStream(applyToken, events, response);
        }
        finally
        {
            if (disposeResponse)
            {
                response.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public async Task<SpecCancelResponse> CancelAsync(string applyToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(applyToken))
        {
            throw new ArgumentException("Apply token is required.", nameof(applyToken));
        }

        var response = await PostJsonAsync(
            $"{SpecPrefix}/cancel",
            new SpecCancelRequest { ApplyToken = applyToken },
            SpecJsonContext.Default.SpecCancelRequest,
            SpecJsonContext.Default.SpecCancelResponse,
            ct).ConfigureAwait(false);

        return response ?? throw new HonuaSpecException(System.Net.HttpStatusCode.OK, "Server returned null cancel response.");
    }

    private async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
        string url,
        TRequest requestBody,
        JsonTypeInfo<TRequest> requestTypeInfo,
        JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken ct)
    {
        using var content = JsonContent.Create(requestBody, requestTypeInfo);
        using var response = await _http.PostAsync(CreateRequestUri(url), content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return JsonSerializer.Deserialize(body, responseTypeInfo);
    }

    private static async IAsyncEnumerable<SpecApplyEvent> ReadApplyEventsAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var data = new StringBuilder();

            while (true)
            {
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                if (line.Length == 0)
                {
                    if (data.Length > 0)
                    {
                        var json = data.ToString();
                        data.Clear();
                        var parsed = JsonSerializer.Deserialize(json, SpecJsonContext.Default.SpecApplyEvent);
                        if (parsed is not null)
                        {
                            yield return parsed;
                        }
                    }

                    continue;
                }

                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length > 0)
                    {
                        data.Append('\n');
                    }

                    data.Append(line.AsSpan("data:".Length).TrimStart());
                }
            }

            if (data.Length > 0)
            {
                var parsed = JsonSerializer.Deserialize(data.ToString(), SpecJsonContext.Default.SpecApplyEvent);
                if (parsed is not null)
                {
                    yield return parsed;
                }
            }
        }
        finally
        {
            response.Dispose();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, body);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        SpecProblem? problem = null;
        try
        {
            problem = JsonSerializer.Deserialize(body, SpecJsonContext.Default.SpecProblem);
        }
        catch (JsonException)
        {
            // Non-JSON error body.
        }

        var message = problem?.Detail
            ?? problem?.Title
            ?? response.ReasonPhrase
            ?? "Spec request failed.";
        throw new HonuaSpecException(response.StatusCode, message, body, problem);
    }

    private static string? TryGetHeader(HttpResponseMessage response, string name)
    {
        return response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);
}
