using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Honua.Sdk.Cli;

internal static class DiagnosticDoctor
{
    internal const int MaxInputBytes = 30 * 1024 * 1024;
    internal const int MaxProbeBytes = 256 * 1024;

    internal static DiagnosticExchangeInput ReadCapturedExchange(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length > MaxInputBytes)
            throw new CliArgumentException("Diagnostic input could not be read or exceeds 30 MiB.");

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file.FullName);
        }
        catch (IOException)
        {
            throw new CliArgumentException("Diagnostic input could not be read.");
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("request", out JsonElement request)
                || !request.TryGetProperty("method", out JsonElement methodElement)
                || methodElement.ValueKind != JsonValueKind.String
                || !request.TryGetProperty("url", out JsonElement urlElement)
                || urlElement.ValueKind != JsonValueKind.String)
            {
                throw new CliArgumentException("Captured exchange requires request.method and request.url strings.");
            }

            root.TryGetProperty("response", out JsonElement response);
            return new DiagnosticExchangeInput(
                methodElement.GetString()!,
                urlElement.GetString()!,
                ReadStatus(response),
                ReadString(response, "mediaType"),
                ReadString(root, "correlationId"),
                ReadString(root, "traceId"),
                ReadString(root, "capturedAt"),
                ReadHeaders(request, "headers"),
                ReadHeaders(response, "headers"),
                ReadBody(request),
                ReadBody(response));
        }
        catch (JsonException)
        {
            throw new CliArgumentException("Diagnostic input is not valid JSON.");
        }
    }

    internal static async Task<DiagnosticExchangeInput> ProbeCapabilitiesAsync(
        Uri baseUri,
        HttpClient client,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Uri target = AppendPath(baseUri, "/api/v1/services?limit=1");
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, target);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/problem+json"));
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token).ConfigureAwait(false);
            byte[] body = await ReadBoundedBodyAsync(response, MaxProbeBytes, timeoutSource.Token).ConfigureAwait(false);
            return new DiagnosticExchangeInput(
                "GET",
                target.AbsoluteUri,
                (int)response.StatusCode,
                response.Content.Headers.ContentType?.MediaType,
                FirstHeader(response, "x-correlation-id", "x-request-id"),
                FirstHeader(response, "traceparent"),
                DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ResponseHeaders: ReadHeaders(response),
                ResponseBody: body);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException or DiagnosticSafetyException)
        {
            return new DiagnosticExchangeInput(
                "GET",
                target.AbsoluteUri,
                CapturedAt: DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ResponseHeaders: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["content-type"] = "application/problem+json"
                },
                ResponseBody: Encoding.UTF8.GetBytes("{\"error\":\"capability-probe-failed\"}"));
        }
    }

    internal static async Task<byte[]> ReadBoundedBodyAsync(
        HttpResponseMessage response,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > maximumBytes)
            throw new DiagnosticSafetyException("response-over-budget", "Diagnostic response exceeds the byte budget.");

        Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (responseStream.ConfigureAwait(false))
        {
            using MemoryStream output = new();
            byte[] buffer = new byte[8192];
            while (true)
            {
                int read = await responseStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;
                if (output.Length + read > maximumBytes)
                    throw new DiagnosticSafetyException("response-over-budget", "Diagnostic response exceeds the byte budget.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            return output.ToArray();
        }
    }

    internal static IReadOnlyDictionary<string, string> ReadHeaders(HttpResponseMessage response)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        AddHeaders(result, response.Headers);
        AddHeaders(result, response.Content.Headers);
        return result;
    }

    internal static string? FirstHeader(HttpResponseMessage response, params string[] names)
    {
        foreach (string name in names)
        {
            if (response.Headers.TryGetValues(name, out IEnumerable<string>? values))
                return values.FirstOrDefault();
        }
        return null;
    }

    private static Uri AppendPath(Uri baseUri, string suffix)
    {
        string basePath = baseUri.AbsolutePath == "/" ? string.Empty : baseUri.AbsolutePath.TrimEnd('/');
        return new Uri(baseUri.GetLeftPart(UriPartial.Authority) + basePath + suffix);
    }

    private static void AddHeaders(Dictionary<string, string> destination, HttpHeaders headers)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
            destination[header.Key] = string.Join(",", header.Value);
    }

    private static int? ReadStatus(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object)
            return null;
        foreach (string name in new[] { "statusCode", "status" })
        {
            if (response.TryGetProperty(name, out JsonElement status) && status.TryGetInt32(out int value))
                return value;
        }
        return null;
    }

    private static string? ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

    private static Dictionary<string, string>? ReadHeaders(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out JsonElement headers)
            || headers.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty header in headers.EnumerateObject())
        {
            if (header.Value.ValueKind == JsonValueKind.String)
                result[header.Name] = header.Value.GetString() ?? string.Empty;
            else if (header.Value.ValueKind == JsonValueKind.Array)
                result[header.Name] = string.Join(",", header.Value.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString()));
        }
        return result;
    }

    private static byte[]? ReadBody(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("body", out JsonElement body))
            return null;
        return body.ValueKind == JsonValueKind.String
            ? Encoding.UTF8.GetBytes(body.GetString() ?? string.Empty)
            : Encoding.UTF8.GetBytes(body.GetRawText());
    }
}
