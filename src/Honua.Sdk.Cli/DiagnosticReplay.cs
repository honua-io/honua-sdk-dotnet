using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Honua.Sdk.Cli;

internal static partial class DiagnosticReplay
{
    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept", "content-length", "content-type", "traceparent", "x-correlation-id", "x-request-id"
    };

    internal static DiagnosticBundle ReadAndValidateBundle(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length > 30L * 1024 * 1024)
            throw new CliArgumentException("Replay bundle could not be read or exceeds 30 MiB.");

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(file.FullName);
        }
        catch (IOException)
        {
            throw new CliArgumentException("Replay bundle could not be read.");
        }

        using JsonDocument document = ParseJson(bytes, "Replay bundle is not valid JSON.");
        DiagnosticSchema.Instance.AssertValid(document.RootElement);
        DiagnosticBundle bundle = JsonSerializer.Deserialize<DiagnosticBundle>(document.RootElement, DiagnosticJson.Options)
            ?? throw new DiagnosticSafetyException("invalid-bundle", "Replay bundle did not contain an object.");
        AssertArtifactSafe(bundle, bytes);
        return bundle;
    }

    internal static async Task<DiagnosticBundle> ReplayAsync(
        DiagnosticBundle bundle,
        Uri baseUri,
        HttpClient client,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        DiagnosticEnvelope source = bundle.Envelopes[^1];
        string method = source.Method.ToUpperInvariant();
        if (method is not ("GET" or "HEAD"))
            throw new DiagnosticSafetyException("unsafe-method", "Replay permits only GET and HEAD exchanges.");

        string path = GetReplayPath(source.NormalizedPath);
        Uri target = BuildTarget(baseUri, path);
        using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        using HttpRequestMessage request = new(new HttpMethod(method), target);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/problem+json"));

        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutSource.Token).ConfigureAwait(false);
        byte[]? responseBody = method == "HEAD"
            ? null
            : await DiagnosticDoctor.ReadBoundedBodyAsync(response, DiagnosticSanitizer.MaxBodyBytes, timeoutSource.Token)
                .ConfigureAwait(false);

        DiagnosticExchangeInput replay = new(
            method,
            target.AbsoluteUri,
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            DiagnosticDoctor.FirstHeader(response, "x-correlation-id", "x-request-id"),
            DiagnosticDoctor.FirstHeader(response, "traceparent"),
            DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ResponseHeaders: DiagnosticDoctor.ReadHeaders(response),
            ResponseBody: responseBody);

        return DiagnosticSanitizer.CreateBundle(
            bundle.ContentClassification,
            bundle.Consent,
            [replay]);
    }

    internal static Uri ValidateBaseUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || uri.UserInfo.Length > 0
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new CliArgumentException("Base URL must be credential-free and contain no query or fragment.");
        }

        bool localHttp = uri.Scheme == Uri.UriSchemeHttp
            && (uri.IsLoopback || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase));
        if (uri.Scheme != Uri.UriSchemeHttps && !localHttp)
            throw new CliArgumentException("Base URL must use HTTPS (localhost HTTP is allowed).");
        return uri;
    }

    private static void AssertArtifactSafe(DiagnosticBundle bundle, byte[] bytes)
    {
        DiagnosticSanitizer.AssertSafeMetadata(bundle.BundleId, 64, "unsafe-bundle-id", "Diagnostic bundle id");
        DiagnosticSanitizer.AssertSafeMetadata(bundle.Consent.GrantedBy, 256, "unsafe-granted-by", "Diagnostic consent identity");
        string artifact = Encoding.UTF8.GetString(bytes);
        if (CredentialMaterial().IsMatch(artifact))
            throw new DiagnosticSafetyException("credential-bearing-artifact", "Replay artifact contains credential material.");

        foreach (DiagnosticEnvelope envelope in bundle.Envelopes)
        {
            foreach (DiagnosticHeader header in (envelope.RequestHeaders ?? []).Concat(envelope.ResponseHeaders ?? []))
            {
                if (!SafeHeaders.Contains(header.Name))
                    throw new DiagnosticSafetyException("unsafe-header", "Replay artifact contains a non-allowlisted header.");
            }
            VerifyBody(envelope.RequestBody);
            VerifyBody(envelope.ResponseBody);
        }
    }

    private static void VerifyBody(DiagnosticBodyPreview? body)
    {
        if (body is null)
            return;
        if (body.ContentSha256 is null || !LowercaseSha256().IsMatch(body.ContentSha256))
            throw new DiagnosticSafetyException("hash-drift", "Replay requires a lowercase SHA-256 for every captured body.");
        if (body.RedactionApplied || body.Truncated)
            return;

        byte[] preview = Encoding.UTF8.GetBytes(body.Preview ?? string.Empty);
        string digest = Convert.ToHexStringLower(SHA256.HashData(preview));
        if (preview.LongLength != body.OriginalByteSize || !digest.Equals(body.ContentSha256, StringComparison.Ordinal))
            throw new DiagnosticSafetyException("hash-drift", "Captured body preview no longer matches its integrity metadata.");
    }

    private static string GetReplayPath(string normalizedPath)
    {
        if (normalizedPath.Length > 2048
            || !normalizedPath.StartsWith('/')
            || normalizedPath.StartsWith("//", StringComparison.Ordinal)
            || normalizedPath.Contains('\\', StringComparison.Ordinal)
            || normalizedPath.Any(character => character is <= '\u001f' or '\u007f'))
        {
            throw new DiagnosticSafetyException("unsafe-path", "Replay path is malformed.");
        }

        string path = normalizedPath.Split('?', 2)[0];
        if (path.Contains('{', StringComparison.Ordinal) || path.Contains('}', StringComparison.Ordinal))
            throw new DiagnosticSafetyException("unsafe-path", "Replay refuses placeholder path segments.");

        string decoded = Uri.UnescapeDataString(path);
        if (decoded.Split('/').Any(segment => segment is "." or "..") || ForbiddenPath().IsMatch(decoded))
            throw new DiagnosticSafetyException("unsafe-path", "Replay path is mutation-, subscription-, or traversal-capable.");
        return path;
    }

    private static Uri BuildTarget(Uri baseUri, string path)
    {
        string basePath = baseUri.AbsolutePath == "/" ? string.Empty : baseUri.AbsolutePath.TrimEnd('/');
        bool alreadyPrefixed = basePath.Length > 0
            && (path.Equals(basePath, StringComparison.Ordinal) || path.StartsWith(basePath + "/", StringComparison.Ordinal));
        Uri target = new(baseUri.GetLeftPart(UriPartial.Authority) + (alreadyPrefixed ? path : basePath + path));
        if (!target.GetLeftPart(UriPartial.Authority).Equals(baseUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            throw new DiagnosticSafetyException("unsafe-origin", "Replay target escaped the configured server origin.");
        return target;
    }

    private static JsonDocument ParseJson(byte[] bytes, string error)
    {
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException)
        {
            throw new CliArgumentException(error);
        }
    }

    [GeneratedRegex("(?:\\bBearer\\s+|\\bBasic\\s+|AKIA[0-9A-Z]{16}|[?&](?:api[-_]?key|signature|token)=)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex CredentialMaterial();

    [GeneratedRegex("^[a-f0-9]{64}$", RegexOptions.None, 100)]
    private static partial Regex LowercaseSha256();

    [GeneratedRegex("(?:^|/)(?:applyedits|attachments|delete|edit|jobs|mutate|publish|stream|subscribe|update|upload)(?:/|$)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex ForbiddenPath();
}
