using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Cli;

internal sealed record DiagnosticBundle(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("contentClassification")] string ContentClassification,
    [property: JsonPropertyName("consent")] DiagnosticConsent Consent,
    [property: JsonPropertyName("envelopes")] IReadOnlyList<DiagnosticEnvelope> Envelopes,
    [property: JsonPropertyName("bundleId")] string? BundleId = null);

internal sealed record DiagnosticConsent(
    [property: JsonPropertyName("redactionAcknowledged")] bool RedactionAcknowledged,
    [property: JsonPropertyName("shareWithSupport")] bool ShareWithSupport,
    [property: JsonPropertyName("grantedBy")] string? GrantedBy = null);

internal sealed record DiagnosticEnvelope(
    [property: JsonPropertyName("method")] string Method,
    [property: JsonPropertyName("normalizedPath")] string NormalizedPath,
    [property: JsonPropertyName("statusCode")] int? StatusCode = null,
    [property: JsonPropertyName("mediaType")] string? MediaType = null,
    [property: JsonPropertyName("correlationId")] string? CorrelationId = null,
    [property: JsonPropertyName("traceId")] string? TraceId = null,
    [property: JsonPropertyName("capturedAt")] string? CapturedAt = null,
    [property: JsonPropertyName("requestHeaders")] IReadOnlyList<DiagnosticHeader>? RequestHeaders = null,
    [property: JsonPropertyName("responseHeaders")] IReadOnlyList<DiagnosticHeader>? ResponseHeaders = null,
    [property: JsonPropertyName("requestBody")] DiagnosticBodyPreview? RequestBody = null,
    [property: JsonPropertyName("responseBody")] DiagnosticBodyPreview? ResponseBody = null);

internal sealed record DiagnosticHeader(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);

internal sealed record DiagnosticBodyPreview(
    [property: JsonPropertyName("originalByteSize")] long OriginalByteSize,
    [property: JsonPropertyName("redactionApplied")] bool RedactionApplied,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("preview")] string? Preview = null,
    [property: JsonPropertyName("contentSha256")] string? ContentSha256 = null);

internal sealed record DiagnosticExchangeInput(
    string Method,
    string Url,
    int? StatusCode = null,
    string? MediaType = null,
    string? CorrelationId = null,
    string? TraceId = null,
    string? CapturedAt = null,
    IReadOnlyDictionary<string, string>? RequestHeaders = null,
    IReadOnlyDictionary<string, string>? ResponseHeaders = null,
    byte[]? RequestBody = null,
    byte[]? ResponseBody = null);

internal static class DiagnosticJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}
