using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Honua.Sdk.Cli;

internal static partial class DiagnosticSanitizer
{
    internal const int MaxBodyBytes = 25 * 1024 * 1024;
    internal const int MaxEnvelopes = 50;
    internal const int DefaultPreviewBytes = 4096;
    internal const int MaxPreviewBytes = 8192;

    private static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
    {
        "unknown", "public", "internal", "customer-data", "secret-suspected"
    };

    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept", "content-length", "content-type", "traceparent", "x-correlation-id", "x-request-id"
    };

    private static readonly HashSet<string> SafePathSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "api", "capabilities", "collections", "conformance", "features", "featureserver", "health",
        "healthz", "honua", "items", "layers", "mapserver", "maps", "ogc", "query", "readiness",
        "ready", "records", "search", "services", "stac", "tiles", "v1", "v2"
    };

    private static readonly HashSet<string> SafeQueryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bbox", "collections", "datetime", "f", "fields", "filter", "format", "limit", "offset",
        "outfields", "q", "resultoffset", "resultrecordcount", "where"
    };

    internal static DiagnosticBundle CreateBundle(
        string classification,
        DiagnosticConsent consent,
        IReadOnlyList<DiagnosticExchangeInput> exchanges,
        string? bundleId = null,
        int previewBytes = DefaultPreviewBytes)
    {
        if (!Classifications.Contains(classification))
            throw new DiagnosticSafetyException("invalid-classification", "Diagnostic classification is not supported.");
        if (previewBytes is < 1 or > MaxPreviewBytes)
            throw new DiagnosticSafetyException("invalid-preview-budget", "Diagnostic preview bytes must be between 1 and 8192.");
        if (exchanges.Count is < 1 or > MaxEnvelopes)
            throw new DiagnosticSafetyException("invalid-envelope-count", "Diagnostic bundle requires between 1 and 50 exchanges.");

        AssertSafeMetadata(bundleId, 64, "unsafe-bundle-id", "Diagnostic bundle id");
        AssertSafeMetadata(consent.GrantedBy, 256, "unsafe-granted-by", "Diagnostic consent identity");

        DiagnosticBundle bundle = new(
            "1.0",
            classification,
            consent,
            exchanges.Select(exchange => SanitizeExchange(exchange, previewBytes)).ToArray(),
            bundleId);
        using JsonDocument document = JsonSerializer.SerializeToDocument(bundle, DiagnosticJson.Options);
        DiagnosticSchema.Instance.AssertValid(document.RootElement);
        return bundle;
    }

    internal static DiagnosticEnvelope SanitizeExchange(DiagnosticExchangeInput input, int previewBytes)
    {
        string method = input.Method.Trim().ToUpperInvariant();
        if (method.Length is < 1 or > 16 || !method.All(character => character is >= 'A' and <= 'Z'))
            throw new DiagnosticSafetyException("invalid-method", "Diagnostic HTTP method is invalid.");
        if (input.StatusCode is < 100 or > 599)
            throw new DiagnosticSafetyException("invalid-status", "Diagnostic status code must be between 100 and 599.");

        string? mediaType = SanitizeMediaType(input.MediaType);
        IReadOnlyList<DiagnosticHeader>? requestHeaders = SanitizeHeaders(input.RequestHeaders);
        IReadOnlyList<DiagnosticHeader>? responseHeaders = SanitizeHeaders(input.ResponseHeaders);
        string? requestMediaType = requestHeaders?.FirstOrDefault(header =>
            header.Name.Equals("content-type", StringComparison.OrdinalIgnoreCase))?.Value;

        return new DiagnosticEnvelope(
            method,
            NormalizePath(input.Url),
            input.StatusCode,
            mediaType,
            SanitizeBoundedText(input.CorrelationId, 200),
            SanitizeBoundedText(input.TraceId, 200),
            NormalizeTimestamp(input.CapturedAt),
            requestHeaders,
            responseHeaders,
            SanitizeBody(input.RequestBody, requestMediaType, previewBytes),
            SanitizeBody(input.ResponseBody, mediaType, previewBytes));
    }

    internal static IReadOnlyList<DiagnosticHeader>? SanitizeHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
            return null;

        DiagnosticHeader[] sanitized = headers
            .Where(header => SafeHeaders.Contains(header.Key))
            .Select(header => new DiagnosticHeader(
#pragma warning disable CA1308 // HTTP field names are conventionally normalized to lowercase on the wire.
                header.Key.ToLowerInvariant(),
#pragma warning restore CA1308
                header.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase)
                    ? SanitizeMediaType(header.Value) ?? "application/octet-stream"
                    : TruncateText(RedactText(header.Value), 2048)))
            .OrderBy(header => header.Name, StringComparer.Ordinal)
            .Take(32)
            .ToArray();
        return sanitized.Length == 0 ? null : sanitized;
    }

    internal static DiagnosticBodyPreview? SanitizeBody(byte[]? body, string? mediaType, int previewBytes)
    {
        if (body is null)
            return null;
        if (body.Length > MaxBodyBytes)
            throw new DiagnosticSafetyException("body-over-budget", "Diagnostic body exceeds the 25 MiB intake ceiling.");

        string digest = Convert.ToHexStringLower(SHA256.HashData(body));
        int scanLength = Math.Min(body.Length, previewBytes * 8);
        string decoded;
        try
        {
            decoded = new UTF8Encoding(false, true).GetString(body, 0, scanLength);
        }
        catch (DecoderFallbackException)
        {
            return new DiagnosticBodyPreview(body.Length, true, true, "[BINARY_BODY_OMITTED]", digest);
        }

        string sanitized = LooksLikeJson(decoded, mediaType) ? RedactJsonOrText(decoded) : RedactText(decoded);
        string preview = TruncateUtf8(sanitized, previewBytes);
        bool truncated = body.Length > scanLength || Encoding.UTF8.GetByteCount(sanitized) > previewBytes;
        return new DiagnosticBodyPreview(
            body.Length,
            !string.Equals(sanitized, decoded, StringComparison.Ordinal),
            truncated,
            preview,
            digest);
    }

    internal static string NormalizePath(string input)
    {
        string rawPath = input.Split('?', '#')[0];
        if (input.Length > 8192
            || input.Contains('\\', StringComparison.Ordinal)
            || HasControl(input)
            || TraversalSegment().IsMatch(rawPath))
            throw new DiagnosticSafetyException("unsafe-url", "Diagnostic URL is malformed or over budget.");
        if (!Uri.TryCreate(input, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DiagnosticSafetyException("unsafe-url", "Diagnostic URL must be an absolute HTTP or HTTPS URL.");
        }

        string[] segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        List<string> normalizedSegments = [];
        foreach (string encodedSegment in segments)
        {
            string segment;
            try
            {
                segment = Uri.UnescapeDataString(encodedSegment);
            }
            catch (UriFormatException)
            {
                throw new DiagnosticSafetyException("unsafe-path", "Diagnostic URL contains invalid path encoding.");
            }
            if (segment is "." or ".." || segment.Contains('\\', StringComparison.Ordinal) || HasControl(segment))
                throw new DiagnosticSafetyException("unsafe-path", "Diagnostic URL contains an unsafe path segment.");
            normalizedSegments.Add(SafePathSegments.Contains(segment) ? Uri.EscapeDataString(segment) : "{value}");
        }

        List<string> query = [];
        string rawQuery = uri.Query.TrimStart('?');
        foreach (string pair in rawQuery.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string rawName = pair.Split('=', 2)[0];
            string name = Uri.UnescapeDataString(rawName.Replace("+", " ", StringComparison.Ordinal));
            if (SensitiveName().IsMatch(name))
                continue;
            query.Add($"{(SafeQueryNames.Contains(name) ? Uri.EscapeDataString(name) : "{parameter}")}={{value}}");
        }
        query.Sort(StringComparer.Ordinal);

        string path = "/" + string.Join('/', normalizedSegments);
        string result = query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
        if (result.Length > 2048)
            throw new DiagnosticSafetyException("path-over-budget", "Normalized diagnostic path exceeds the schema limit.");
        return result;
    }

    internal static string RedactText(string input)
    {
        string value = DecodeForRedaction(input);
        value = Authorization().Replace(value, "[REDACTED_AUTH]");
        value = AwsAccessKey().Replace(value, "[REDACTED_AWS_KEY]");
        value = ProviderToken().Replace(value, "[REDACTED_PROVIDER_TOKEN]");
        value = Jwt().Replace(value, "[REDACTED_JWT]");
        value = NamedSecret().Replace(value, "$1=[REDACTED]");
        value = Email().Replace(value, "[REDACTED_EMAIL]");
        return HighEntropy().Replace(value, "[REDACTED_TOKEN]");
    }

    internal static void AssertSafeMetadata(string? value, int maxLength, string code, string label)
    {
        if (value is null)
            return;
        if (value.Length > maxLength || HasControl(value) || !string.Equals(RedactText(value), value, StringComparison.Ordinal))
            throw new DiagnosticSafetyException(code, $"{label} must not contain credentials or personal data.");
    }

    private static string? SanitizeMediaType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        string mediaType = value.Split(';', 2)[0].Trim();
        return SanitizeBoundedText(mediaType, 256);
    }

    private static string? NormalizeTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !DateTimeOffset.TryParse(value, out DateTimeOffset timestamp))
            return null;
        return timestamp.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string? SanitizeBoundedText(string? value, int length)
        => string.IsNullOrWhiteSpace(value) ? null : TruncateText(RedactText(value), length);

    private static string RedactJsonOrText(string decoded)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(decoded);
            using MemoryStream output = new();
            using (Utf8JsonWriter writer = new(output, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
                WriteRedactedJson(writer, document.RootElement, 0, new Counter());
            return Encoding.UTF8.GetString(output.ToArray());
        }
        catch (JsonException)
        {
            return RedactText(decoded);
        }
    }

    private static void WriteRedactedJson(Utf8JsonWriter writer, JsonElement element, int depth, Counter counter)
    {
        counter.Value++;
        if (depth > 16 || counter.Value > 4096)
        {
            writer.WriteStringValue("[REDACTED_COMPLEX_VALUE]");
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                int propertyIndex = 0;
                foreach (JsonProperty property in element.EnumerateObject().Take(256))
                {
                    propertyIndex++;
                    bool sensitive = SensitiveName().IsMatch(property.Name)
                        || !string.Equals(RedactText(property.Name), property.Name, StringComparison.Ordinal);
                    writer.WritePropertyName(sensitive ? $"[REDACTED_KEY_{propertyIndex}]" : property.Name);
                    if (sensitive)
                        writer.WriteStringValue("[REDACTED]");
                    else
                        WriteRedactedJson(writer, property.Value, depth + 1, counter);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray().Take(256))
                    WriteRedactedJson(writer, item, depth + 1, counter);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(RedactText(element.GetString() ?? string.Empty));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool LooksLikeJson(string decoded, string? mediaType)
    {
        string trimmed = decoded.TrimStart();
        return mediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true
            || trimmed.StartsWith('{')
            || trimmed.StartsWith('[');
    }

    private static string DecodeForRedaction(string input)
    {
        string value = input;
        for (int pass = 0; pass < 2 && PercentEncoding().IsMatch(value); pass++)
        {
            try
            {
                value = Uri.UnescapeDataString(value);
            }
            catch (UriFormatException)
            {
                break;
            }
        }
        return value;
    }

    private static bool HasControl(string value) => value.Any(character => character is <= '\u001f' or '\u007f');

    private static string TruncateText(string value, int maximumCharacters)
        => value.Length <= maximumCharacters ? value : value[..maximumCharacters];

    private static string TruncateUtf8(string value, int maximumBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maximumBytes)
            return value;
        int length = Math.Min(value.Length, maximumBytes);
        while (length > 0 && Encoding.UTF8.GetByteCount(value.AsSpan(0, length)) > maximumBytes)
            length--;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]))
            length--;
        return value[..length];
    }

    private sealed class Counter
    {
        internal int Value { get; set; }
    }

    [GeneratedRegex("(?:api[-_]?key|authorization|cookie|credential|jwt|pass(?:word)?|proxy[-_]?authorization|secret|session|set[-_]?cookie|sig(?:nature)?|token)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex SensitiveName();

    [GeneratedRegex("\\b(?:Bearer|Basic)\\s+[A-Za-z0-9._~+/=-]+", RegexOptions.IgnoreCase, 100)]
    private static partial Regex Authorization();

    [GeneratedRegex("\\bAKIA[0-9A-Z]{16}\\b", RegexOptions.None, 100)]
    private static partial Regex AwsAccessKey();

    [GeneratedRegex("\\b(?:sk_[A-Za-z0-9_-]{8,}|(?:sk|pk|rk)_(?:live|test)_[A-Za-z0-9]{8,}|gh[pousr]_[A-Za-z0-9_-]{8,}|xox[baprs]-[A-Za-z0-9-]{8,}|AIza[0-9A-Za-z_-]{20,}|glpat-[A-Za-z0-9_-]{10,})\\b", RegexOptions.IgnoreCase, 100)]
    private static partial Regex ProviderToken();

    [GeneratedRegex("\\beyJ[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\b", RegexOptions.None, 100)]
    private static partial Regex Jwt();

    [GeneratedRegex("\\b(access[-_]?key|api[-_]?key|authorization|aws[-_]?secret[-_]?access[-_]?key|bearer|client[-_]?secret|cookie|credential|pass(?:word)?|proxy[-_]?authorization|pwd|secret|session|set[-_]?cookie|signature|token)\\s*[:=]\\s*(?:\"[^\"]*\"|'[^']*'|[^\\s&,;}]*)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex NamedSecret();

    [GeneratedRegex("\\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}\\b", RegexOptions.IgnoreCase, 100)]
    private static partial Regex Email();

    [GeneratedRegex("\\b[A-Za-z0-9_+/=]{32,}\\b", RegexOptions.None, 100)]
    private static partial Regex HighEntropy();

    [GeneratedRegex("%[0-9a-f]{2}", RegexOptions.IgnoreCase, 100)]
    private static partial Regex PercentEncoding();

    [GeneratedRegex("(?:^|/)(?:(?:%2e|\\.){1,2})(?:/|$)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex TraversalSegment();
}
