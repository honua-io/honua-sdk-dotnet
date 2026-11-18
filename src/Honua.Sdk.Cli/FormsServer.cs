// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Honua.Sdk.Cli;

internal sealed record FormsImportTarget(string ServiceId, int LayerId);

internal readonly record struct FormsEndpoint(Uri BaseUri, string ApiKey, TimeSpan Timeout);

internal sealed class FormsServer
{
    internal const string WorkbookContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    internal const string ApiKeyVariable = "HONUA_API_KEY";
    private const string PackagesPath = "api/v1/admin/forms/packages/";
    private const int MaxResponseBytes = 16 * 1024 * 1024;
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    private readonly FormsEndpoint _endpoint;
    private readonly HttpClient _client;

    private FormsServer(FormsEndpoint endpoint, HttpClient client)
    {
        _endpoint = endpoint;
        _client = client;
    }

    internal static FormsServer Create(FormsInvocation invocation, HttpClient client)
    {
        string baseValue = CliOptions.RequireBaseUrl(
            invocation.Get("base-url"),
            "honua forms requires --base-url or HONUA_BASE_URL (or --local).");
        Uri baseUri = DiagnosticReplay.ValidateBaseUri(baseValue);
        if (!baseUri.AbsolutePath.EndsWith('/', StringComparison.Ordinal))
            baseUri = new Uri(baseUri.AbsoluteUri + "/");

        int timeoutMs = CliOptions.ReadInteger(
            invocation.Get("timeout-ms"),
            new IntegerBounds
            {
                Name = "timeout-ms",
                DefaultValue = 60_000,
                Minimum = 1,
                Maximum = 300_000,
                Message = "--timeout-ms must be between 1 and 300000."
            });

        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new CliArgumentException("honua forms requires HONUA_API_KEY for a server import or export.");
        if (apiKey.Any(char.IsControl))
            throw new CliArgumentException("HONUA_API_KEY contains a control character.");
        return new FormsServer(new FormsEndpoint(baseUri, apiKey, TimeSpan.FromMilliseconds(timeoutMs)), client);
    }

    internal async Task<FormsResult> ImportAsync(string workbookPath, FormsImportTarget target, CancellationToken cancellationToken)
    {
        byte[] workbook = await CliFiles.ReadBoundedAsync(workbookPath, CliFiles.MaxWorkbookBytes, cancellationToken).ConfigureAwait(false);
        using ByteArrayContent file = new(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(WorkbookContentType);
        using MultipartFormDataContent content = new() { { file, "file", Path.GetFileName(workbookPath) } };

        string query = string.Create(
            CultureInfo.InvariantCulture,
            $"import?serviceId={Uri.EscapeDataString(target.ServiceId)}&layerId={target.LayerId}");
        using HttpRequestMessage request = CreateRequest(HttpMethod.Post, query, "application/json");
        request.Content = content;
        (HttpStatusCode status, byte[] body) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (status != HttpStatusCode.Created)
            return Problem(status, body, "import");

        FormsImportResponse created = JsonSerializer.Deserialize(body, FormsJsonContext.Default.FormsImportResponse)
            ?? throw new JsonException("Empty import response.");
        if (string.IsNullOrWhiteSpace(created.FormId))
            return FormsResult.Failed("honua forms import failed: the server response has no form id.");
        return FormsResult.Succeeded("imported", new Dictionary<string, string>
        {
            ["formId"] = created.FormId,
            ["version"] = created.Version.ToString(CultureInfo.InvariantCulture),
            ["status"] = created.Status
        });
    }

    internal async Task<FormsResult> ExportAsync(string formId, string? outputPath, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(HttpMethod.Get, Uri.EscapeDataString(formId) + "/export", WorkbookContentType);
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

    private HttpRequestMessage CreateRequest(HttpMethod method, string relative, string primaryAccept)
    {
        HttpRequestMessage request = new(method, new Uri(_endpoint.BaseUri, PackagesPath + relative));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(primaryAccept));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/problem+json"));
        request.Headers.TryAddWithoutValidation("X-API-Key", _endpoint.ApiKey);
        return request;
    }

    private async Task<(HttpStatusCode Status, byte[] Body)> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_endpoint.Timeout);
        using HttpResponseMessage response = await _client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
            .ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new ContentTooLargeException("The server response exceeds 16 MiB.");
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
