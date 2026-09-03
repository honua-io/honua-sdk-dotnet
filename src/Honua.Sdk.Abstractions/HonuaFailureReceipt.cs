// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Honua.Sdk.Abstractions;

/// <summary>Normalized machine failure classes shared by every official transport.</summary>
public enum HonuaFailureKind
{
    /// <summary>The failure could not be classified safely.</summary>
    Unknown,
    /// <summary>Credentials are absent, expired, or otherwise unusable.</summary>
    Authentication,
    /// <summary>The authenticated principal lacks permission.</summary>
    Authorization,
    /// <summary>The requested resource does not exist.</summary>
    NotFound,
    /// <summary>One or more request fields are invalid.</summary>
    Validation,
    /// <summary>The request conflicts with current server state.</summary>
    Conflict,
    /// <summary>Capacity is temporarily exhausted.</summary>
    Throttled,
    /// <summary>A remote dependency or service is temporarily unavailable.</summary>
    Unavailable
}

/// <summary>One field- or item-addressable server failure.</summary>
public sealed class HonuaFieldFailure
{
    /// <summary>Stable item-level machine code.</summary>
    public string? Code { get; init; }
    /// <summary>Optional severity supplied by the protocol.</summary>
    public string? Severity { get; init; }
    /// <summary>JSON Pointer or other protocol-native field path.</summary>
    public string? Path { get; init; }
    /// <summary>Stable field identifier, when supplied.</summary>
    public string? FieldId { get; init; }
    /// <summary>Batch item index, when supplied.</summary>
    public int? ItemIndex { get; init; }
    /// <summary>Human-readable item detail.</summary>
    public string? Message { get; init; }
}

/// <summary>Safe protocol metadata retained separately for response headers and trailers.</summary>
public sealed class HonuaProtocolMetadata
{
    /// <summary>HTTP response headers or gRPC initial metadata.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Initial { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>gRPC trailing metadata.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Trailing { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A protocol-preserving, machine-actionable terminal failure receipt.</summary>
public sealed class HonuaFailureReceipt
{
    /// <summary>HTTP transport status, independent from any body-level protocol code.</summary>
    public int? TransportStatus { get; init; }
    /// <summary>GeoServices or gRPC protocol-native status/code.</summary>
    public string? ProtocolCode { get; init; }
    /// <summary>Normalized failure class.</summary>
    public HonuaFailureKind Kind { get; init; }
    /// <summary>Stable server machine code.</summary>
    public string? Code { get; init; }
    /// <summary>Whether the terminal receipt permits a later retry.</summary>
    public bool Retryable { get; init; }
    /// <summary>Server-declared retry delay, when known.</summary>
    public TimeSpan? RetryAfter { get; init; }
    /// <summary>Server correlation/request identity.</summary>
    public string? CorrelationId { get; init; }
    /// <summary>Field- or item-addressable failures.</summary>
    public IReadOnlyList<HonuaFieldFailure> FieldErrors { get; init; } = [];
    /// <summary>Protocol-native response metadata with sensitive headers removed.</summary>
    public HonuaProtocolMetadata ProtocolMetadata { get; init; } = new();
}

/// <summary>Creates normalized receipts at HTTP and gRPC terminal boundaries.</summary>
public static class HonuaFailureReceiptFactory
{
    private static readonly HashSet<string> SensitiveMetadata =
        new(StringComparer.OrdinalIgnoreCase) { "authorization", "cookie", "set-cookie", "x-api-key" };

    /// <summary>Creates a receipt from an HTTP response and optional GeoServices protocol code.</summary>
    /// <param name="response">Terminal HTTP response.</param>
    /// <param name="body">Response body used only for structured machine fields.</param>
    /// <param name="protocolCode">Independent body-level protocol code.</param>
    /// <returns>The normalized receipt.</returns>
    public static HonuaFailureReceipt FromHttpResponse(
        HttpResponseMessage response,
        string? body,
        int? protocolCode = null)
    {
        ArgumentNullException.ThrowIfNull(response);
        JsonElement? root = TryParseObject(body);
        JsonElement? source = root;
        if (root is { } documentRoot &&
            documentRoot.TryGetProperty("error", out JsonElement error) &&
            error.ValueKind == JsonValueKind.Object)
        {
            source = error;
        }

        int classificationStatus = protocolCode ?? (int)response.StatusCode;
        HonuaFailureKind kind = ParseKind(GetString(source, "kind")) ?? ClassifyHttp(classificationStatus, (int)response.StatusCode);
        string code = GetString(source, "machineCode") ?? GetString(source, "code") ?? DefaultCode(kind);
        bool retryable = GetBoolean(source, "retryable") ??
            (IsHttpRetryable(classificationStatus) || IsHttpRetryable((int)response.StatusCode));
        TimeSpan? retryAfter = GetSeconds(source, "retryAfterSeconds") ?? ParseRetryAfter(response.Headers.RetryAfter);
        string? correlationId = GetString(source, "correlationId") ?? GetString(root, "correlationId") ??
            FirstHeader(response.Headers, "X-Correlation-ID", "Honua-Request-Id", "X-Request-Id");

        return new HonuaFailureReceipt
        {
            TransportStatus = (int)response.StatusCode,
            ProtocolCode = protocolCode?.ToString(CultureInfo.InvariantCulture),
            Kind = kind,
            Code = code,
            Retryable = retryable,
            RetryAfter = retryAfter,
            CorrelationId = correlationId,
            FieldErrors = ParseFieldErrors(source, root),
            ProtocolMetadata = new HonuaProtocolMetadata { Initial = CopyHeaders(response) }
        };
    }

    /// <summary>Creates a receipt from a gRPC status and retained initial/trailing metadata.</summary>
    /// <param name="protocolCode">Numeric gRPC status code.</param>
    /// <param name="initialMetadata">Initial response metadata.</param>
    /// <param name="trailingMetadata">Terminal trailers.</param>
    /// <returns>The normalized receipt.</returns>
    public static HonuaFailureReceipt FromGrpc(
        int protocolCode,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? initialMetadata,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? trailingMetadata)
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> initial = FilterMetadata(initialMetadata);
        IReadOnlyDictionary<string, IReadOnlyList<string>> trailing = FilterMetadata(trailingMetadata);
        string? declaredKind = First(trailing, "honua-error-kind") ?? First(initial, "honua-error-kind");
        HonuaFailureKind kind = ParseKind(declaredKind) ?? ClassifyGrpc(protocolCode);
        string code = First(trailing, "honua-error-code") ?? First(initial, "honua-error-code") ?? DefaultCode(kind);
        bool retryable = ParseBoolean(First(trailing, "honua-error-retryable") ?? First(initial, "honua-error-retryable"))
            ?? IsGrpcRetryable(protocolCode);
        TimeSpan? retryAfter = ParseRetryAfter(
            First(trailing, "retry-after") ?? First(initial, "retry-after"));
        string? correlationId =
            First(trailing, "x-correlation-id", "honua-request-id", "x-request-id") ??
            First(initial, "x-correlation-id", "honua-request-id", "x-request-id");
        string? structuredErrors =
            First(trailing, "honua-error-details") ?? First(initial, "honua-error-details");

        return new HonuaFailureReceipt
        {
            ProtocolCode = protocolCode.ToString(CultureInfo.InvariantCulture),
            Kind = kind,
            Code = code,
            Retryable = retryable,
            RetryAfter = retryAfter,
            CorrelationId = correlationId,
            FieldErrors = ParseFieldErrors(TryParseArray(structuredErrors), null),
            ProtocolMetadata = new HonuaProtocolMetadata { Initial = initial, Trailing = trailing }
        };
    }

    private static JsonElement? TryParseObject(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? TryParseArray(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<HonuaFieldFailure> ParseFieldErrors(JsonElement? source, JsonElement? root)
    {
        JsonElement? errors = null;
        if (source is { ValueKind: JsonValueKind.Array } sourceArray)
        {
            errors = sourceArray;
        }
        else if (source is { } sourceObject && sourceObject.ValueKind == JsonValueKind.Object &&
            sourceObject.TryGetProperty("errors", out JsonElement sourceErrors))
        {
            errors = sourceErrors;
        }
        else if (root is { } rootObject && rootObject.TryGetProperty("errors", out JsonElement rootErrors))
        {
            errors = rootErrors;
        }

        if (errors is not { } errorsElement) return [];

        List<HonuaFieldFailure> failures = [];
        if (errorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in errorsElement.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object))
            {
                failures.Add(new HonuaFieldFailure
                {
                    Code = GetString(item, "code"),
                    Severity = GetString(item, "severity"),
                    Path = GetString(item, "path"),
                    FieldId = GetString(item, "fieldId"),
                    ItemIndex = GetInt32(item, "itemIndex"),
                    Message = GetString(item, "message")
                });
            }
        }
        else if (errorsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty field in errorsElement.EnumerateObject().Where(field => field.Value.ValueKind == JsonValueKind.Array))
            {
                foreach (JsonElement message in field.Value.EnumerateArray().Where(message => message.ValueKind == JsonValueKind.String))
                {
                    failures.Add(new HonuaFieldFailure { FieldId = field.Name, Path = field.Name, Message = message.GetString() });
                }
            }
        }

        return failures;
    }

    private static Dictionary<string, IReadOnlyList<string>> CopyHeaders(HttpResponseMessage response)
    {
        Dictionary<string, IReadOnlyList<string>> result = new(StringComparer.OrdinalIgnoreCase);
        AddHeaders(result, response.Headers);
        AddHeaders(result, response.Content.Headers);
        return result;
    }

    private static void AddHeaders(
        Dictionary<string, IReadOnlyList<string>> target,
        HttpHeaders headers)
    {
        foreach ((string key, IEnumerable<string> values) in headers)
        {
            if (!SensitiveMetadata.Contains(key)) target[key] = values.ToArray();
        }
    }

    private static Dictionary<string, IReadOnlyList<string>> FilterMetadata(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? metadata)
    {
        Dictionary<string, IReadOnlyList<string>> result = new(StringComparer.OrdinalIgnoreCase);
        if (metadata is null) return result;
        foreach ((string key, IReadOnlyList<string> values) in metadata)
        {
            if (!SensitiveMetadata.Contains(key)) result[key] = values.ToArray();
        }

        return result;
    }

    private static string? First(HttpHeaders headers, string key) =>
        headers.TryGetValues(key, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    private static string? FirstHeader(HttpHeaders headers, params string[] keys) =>
        keys.Select(key => First(headers, key)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? First(IReadOnlyDictionary<string, IReadOnlyList<string>> metadata, params string[] keys)
        => keys.Select(key => metadata.TryGetValue(key, out IReadOnlyList<string>? values) && values.Count > 0 ? values[0] : null)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? GetString(JsonElement? element, string propertyName) =>
        element is { ValueKind: JsonValueKind.Object } value &&
        value.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? GetString(JsonElement element, string propertyName) => GetString((JsonElement?)element, propertyName);

    private static bool? GetBoolean(JsonElement? element, string propertyName) =>
        element is { ValueKind: JsonValueKind.Object } value &&
        value.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? property.GetBoolean()
            : null;

    private static int? GetInt32(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt32(out int value) ? value : null;

    private static TimeSpan? GetSeconds(JsonElement? element, string propertyName) =>
        element is { ValueKind: JsonValueKind.Object } value &&
        value.TryGetProperty(propertyName, out JsonElement property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out double seconds) && double.IsFinite(seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static TimeSpan? ParseRetryAfter(RetryConditionHeaderValue? value) =>
        value?.Delta ?? (value?.Date is { } date ? Max(TimeSpan.Zero, date - DateTimeOffset.UtcNow) : null);

    private static TimeSpan? ParseRetryAfter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) &&
            double.IsFinite(seconds) && seconds >= 0)
            return TimeSpan.FromSeconds(seconds);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset date)
            ? Max(TimeSpan.Zero, date - DateTimeOffset.UtcNow)
            : null;
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left >= right ? left : right;

    private static bool? ParseBoolean(string? value) => value switch { "true" => true, "false" => false, _ => null };

    private static HonuaFailureKind? ParseKind(string? value) => value switch
    {
        "authentication" => HonuaFailureKind.Authentication,
        "authorization" => HonuaFailureKind.Authorization,
        "not-found" => HonuaFailureKind.NotFound,
        "validation" => HonuaFailureKind.Validation,
        "conflict" => HonuaFailureKind.Conflict,
        "throttled" => HonuaFailureKind.Throttled,
        "unavailable" => HonuaFailureKind.Unavailable,
        "unknown" => HonuaFailureKind.Unknown,
        _ => null
    };

    private static HonuaFailureKind ClassifyHttp(int code, int transportStatus) => code switch
    {
        401 => HonuaFailureKind.Authentication,
        403 or 498 or 499 => HonuaFailureKind.Authorization,
        404 => HonuaFailureKind.NotFound,
        400 or 422 => HonuaFailureKind.Validation,
        409 or 412 or 428 => HonuaFailureKind.Conflict,
        429 => HonuaFailureKind.Throttled,
        _ when IsHttpRetryable(code) || transportStatus >= 500 => HonuaFailureKind.Unavailable,
        _ => HonuaFailureKind.Unknown
    };

    private static HonuaFailureKind ClassifyGrpc(int code) => code switch
    {
        16 => HonuaFailureKind.Authentication,
        7 => HonuaFailureKind.Authorization,
        5 => HonuaFailureKind.NotFound,
        3 => HonuaFailureKind.Validation,
        6 or 10 => HonuaFailureKind.Conflict,
        8 => HonuaFailureKind.Throttled,
        _ when IsGrpcRetryable(code) => HonuaFailureKind.Unavailable,
        _ => HonuaFailureKind.Unknown
    };

    private static bool IsHttpRetryable(int status) => status is 408 or 429 or 500 or 502 or 503 or 504;
    private static bool IsGrpcRetryable(int status) => status is 4 or 8 or 10 or 14;

    private static string DefaultCode(HonuaFailureKind kind) => kind switch
    {
        HonuaFailureKind.Authentication => "authentication_required",
        HonuaFailureKind.Authorization => "permission_denied",
        HonuaFailureKind.NotFound => "resource_not_found",
        HonuaFailureKind.Validation => "validation_failed",
        HonuaFailureKind.Conflict => "resource_conflict",
        HonuaFailureKind.Throttled => "rate_limited",
        HonuaFailureKind.Unavailable => "service_unavailable",
        _ => "unknown_failure"
    };
}
