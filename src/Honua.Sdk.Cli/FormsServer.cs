// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Honua.Sdk.Cli;

/// <summary>The layer an imported workbook's questions bind to.</summary>
internal sealed record FormsImportTarget(string ServiceId, int LayerId);

/// <summary>Calls the server's admin form package XLSForm routes.</summary>
internal sealed class FormsServer
{
    internal const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    internal const string ApiKeyVariable = "HONUA_API_KEY";
    private const string PackagesPath = "api/v1/admin/forms/packages/";
    private const int MaxResponseBytes = 16 * 1024 * 1024;
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    private readonly Uri _baseUri;
    private readonly string? _apiKey;
    private readonly HttpClient _client;
    private readonly TimeSpan _timeout;

    private FormsServer(Uri baseUri, string? apiKey, HttpClient client, TimeSpan timeout)
    {
        _baseUri = baseUri;
        _apiKey = apiKey;
        _client = client;
        _timeout = timeout;
    }

    internal static FormsServer Create(FormsInvocation invocation, HttpClient client)
    {
        string baseValue = invocation.Get("base-url")
            ?? Environment.GetEnvironmentVariable("HONUA_BASE_URL")
            ?? throw new CliArgumentException("honua forms requires --base-url or HONUA_BASE_URL (or --local).");
        Uri baseUri = DiagnosticReplay.ValidateBaseUri(baseValue);
        if (!baseUri.AbsolutePath.EndsWith('/'))
            baseUri = new Uri(baseUri.AbsoluteUri + "/");

        int timeoutMs = 60_000;
        string? timeoutValue = invocation.Get("timeout-ms");
        if (timeoutValue is not null
            && (!int.TryParse(timeoutValue, NumberStyles.None, CultureInfo.InvariantCulture, out timeoutMs)
                || timeoutMs is < 1 or > 300_000))
        {
            throw new CliArgumentException("--timeout-ms must be between 1 and 300000.");
        }

        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        return new FormsServer(baseUri, string.IsNullOrWhiteSpace(apiKey) ? null : apiKey, client, TimeSpan.FromMilliseconds(timeoutMs));
    }

    internal async Task<FormsResult> ImportAsync(string workbookPath, FormsImportTarget target, CancellationToken cancellationToken)
    {
        byte[] workbook = await CliFiles.ReadBoundedAsync(workbookPath, CliFiles.MaxWorkbookBytes, cancellationToken).ConfigureAwait(false);
        ByteArrayContent file = new(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(WorkbookContentType);
        using MultipartFormDataContent content = new() { { file, "file", Path.GetFileName(workbookPath) } };

        string query = string.Create(
            CultureInfo.InvariantCulture,
            $"import?serviceId={Uri.EscapeDataString(target.ServiceId)}&layerId={target.LayerId}");
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, query);
        request.Content = content;
        (HttpStatusCode status, byte[] body) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (status != HttpStatusCode.Created)
            return Problem(status, body, "import");

        FormsImportResponse created = JsonSerializer.Deserialize(body, FormsJsonContext.Default.FormsImportResponse)
            ?? throw new JsonException("Empty import response.");
        return FormsResult.Succeeded("imported", new Dictionary<string, string>
        {
            ["formId"] = created.FormId,
            ["version"] = created.Version.ToString(CultureInfo.InvariantCulture),
            ["status"] = created.Status
        });
    }

    internal async Task<FormsResult> ExportAsync(string formId, string? outputPath, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, Uri.EscapeDataString(formId) + "/export");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(WorkbookContentType));
        (HttpStatusCode status, byte[] body) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (status != HttpStatusCode.OK)
            return Problem(status, body, "export");
        if (!body.AsSpan().StartsWith(ZipSignature))
            return FormsResult.Failed("honua forms export failed: the server response is not an XLSX workbook.");

        string destination = CliFiles.WriteAtomically(outputPath ?? CliFiles.DefaultWorkbookName(formId), body);
        return FormsResult.Succeeded("exported", new Dictionary<string, string>
        {
            ["formId"] = formId,
            ["output"] = destination
        });
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relative)
    {
        HttpRequestMessage request = new(method, new Uri(_baseUri, PackagesPath + relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/problem+json"));
        if (_apiKey is not null)
            request.Headers.TryAddWithoutValidation("X-API-Key", _apiKey);
        return request;
    }

    private async Task<(HttpStatusCode Status, byte[] Body)> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        using HttpResponseMessage response = await _client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new IOException("The server response exceeds 16 MiB.");
        Stream stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            byte[] body = await CliFiles.ReadBoundedAsync(stream, MaxResponseBytes, timeout.Token).ConfigureAwait(false);
            return (response.StatusCode, body);
        }
    }

    private static FormsResult Problem(HttpStatusCode status, byte[] body, string verb)
    {
        FormsProblemBody? problem = TryReadProblem(body);
        string reason = problem?.Detail ?? problem?.Title ?? status.ToString();
        string message = string.Create(CultureInfo.InvariantCulture, $"honua forms {verb} failed: HTTP {(int)status}: {reason}");
        return problem?.Diagnostics is { Length: > 0 } diagnostics
            ? FormsResult.Rejected(message, diagnostics)
            : FormsResult.Failed(message);
    }

    private static FormsProblemBody? TryReadProblem(byte[] body)
    {
        try
        {
            return body.Length == 0 ? null : JsonSerializer.Deserialize(body, FormsJsonContext.Default.FormsProblemBody);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
