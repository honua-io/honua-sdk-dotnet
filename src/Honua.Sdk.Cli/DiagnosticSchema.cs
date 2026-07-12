using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Honua.Sdk.Cli;

internal sealed class DiagnosticSchema
{
    internal const string ResourceName = "Honua.Sdk.Cli.Schemas.diagnostic-bundle.v1.json";
    internal const string CanonicalUrl = "https://honua.io/schemas/diagnostic-bundle.v1.json";
    internal const string SourceCommit = "0c990fbe8f519a00a57e26dab21cbb8f80d559ea";
    internal const string Sha256 = "4dd7282d17bb417d56f1c3cfa243e03b612a401e5d22be766658849287e431a9";
    internal const int ByteCount = 6494;

    private const int MaxErrors = 50;
    private readonly JsonDocument _schema;
    private readonly JsonElement _root;

    internal static DiagnosticSchema Instance { get; } = new();

    private DiagnosticSchema()
    {
        byte[] bytes = LoadCanonicalBytes();
        VerifyCanonicalBytes(bytes);
        _schema = JsonDocument.Parse(bytes);
        _root = _schema.RootElement;
    }

    internal static byte[] LoadCanonicalBytes()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded diagnostic schema '{ResourceName}' was not found.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    internal static void VerifyCanonicalBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteCount)
            throw new InvalidOperationException("Embedded diagnostic schema byte count does not match canonical provenance.");

        string digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(digest, Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Embedded diagnostic schema SHA-256 does not match canonical provenance.");
    }

    internal IReadOnlyList<string> Validate(JsonElement instance)
    {
        List<string> errors = [];
        ValidateNode(_root, instance, "$", errors);
        return errors;
    }

    internal void AssertValid(JsonElement instance)
    {
        IReadOnlyList<string> errors = Validate(instance);
        if (errors.Count > 0)
            throw new DiagnosticSafetyException("schema-validation", "Diagnostic bundle failed pinned v1 schema validation.");
    }

    private void ValidateNode(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (errors.Count >= MaxErrors)
            return;

        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("$ref", out JsonElement reference))
        {
            ValidateNode(ResolveReference(reference.GetString()), instance, path, errors);
            return;
        }

        string? type = schema.TryGetProperty("type", out JsonElement typeElement) ? typeElement.GetString() : null;
        if (type is not null && !MatchesType(type, instance))
        {
            errors.Add($"{path}: expected type '{type}' but found '{instance.ValueKind}'.");
            return;
        }

        if (schema.TryGetProperty("const", out JsonElement constant) && !JsonElement.DeepEquals(constant, instance))
            errors.Add($"{path}: value must equal the constant '{constant.GetRawText()}'.");

        if (schema.TryGetProperty("enum", out JsonElement choices)
            && choices.ValueKind == JsonValueKind.Array
            && !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(choice, instance)))
        {
            errors.Add($"{path}: value is not one of the allowed values.");
        }

        switch (instance.ValueKind)
        {
            case JsonValueKind.String:
                ValidateString(schema, instance, path, errors);
                break;
            case JsonValueKind.Number:
                ValidateNumber(schema, instance, path, errors);
                break;
            case JsonValueKind.Array:
                ValidateArray(schema, instance, path, errors);
                break;
            case JsonValueKind.Object:
                ValidateObject(schema, instance, path, errors);
                break;
        }
    }

    private static void ValidateString(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        int length = (instance.GetString() ?? string.Empty).EnumerateRunes().Count();
        if (schema.TryGetProperty("maxLength", out JsonElement maximum) && length > maximum.GetInt32())
            errors.Add($"{path}: string length {length} exceeds maxLength {maximum.GetInt32()}.");
        if (schema.TryGetProperty("minLength", out JsonElement minimum) && length < minimum.GetInt32())
            errors.Add($"{path}: string length {length} is below minLength {minimum.GetInt32()}.");
    }

    private static void ValidateNumber(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        if (schema.TryGetProperty("type", out JsonElement type)
            && type.GetString() == "integer"
            && !IsIntegral(instance))
        {
            errors.Add($"{path}: expected an integer.");
            return;
        }

        double value = instance.GetDouble();
        if (schema.TryGetProperty("minimum", out JsonElement minimum) && value < minimum.GetDouble())
            errors.Add($"{path}: value {value} is below minimum {minimum.GetDouble()}.");
        if (schema.TryGetProperty("maximum", out JsonElement maximum) && value > maximum.GetDouble())
            errors.Add($"{path}: value {value} exceeds maximum {maximum.GetDouble()}.");
    }

    private static bool IsIntegral(JsonElement instance)
    {
        if (instance.TryGetInt64(out _))
            return true;
        return instance.TryGetDouble(out double value)
            && !double.IsInfinity(value)
            && value == Math.Floor(value);
    }

    private void ValidateArray(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        int count = instance.GetArrayLength();
        if (schema.TryGetProperty("maxItems", out JsonElement maximum) && count > maximum.GetInt32())
            errors.Add($"{path}: array length {count} exceeds maxItems {maximum.GetInt32()}.");
        if (schema.TryGetProperty("minItems", out JsonElement minimum) && count < minimum.GetInt32())
            errors.Add($"{path}: array length {count} is below minItems {minimum.GetInt32()}.");

        if (!schema.TryGetProperty("items", out JsonElement itemSchema))
            return;

        int index = 0;
        foreach (JsonElement item in instance.EnumerateArray())
        {
            ValidateNode(itemSchema, item, $"{path}[{index}]", errors);
            index++;
        }
    }

    private void ValidateObject(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        bool hasProperties = schema.TryGetProperty("properties", out JsonElement properties);
        if (schema.TryGetProperty("required", out JsonElement required))
        {
            foreach (JsonElement requiredName in required.EnumerateArray())
            {
                string name = requiredName.GetString() ?? string.Empty;
                if (!instance.TryGetProperty(name, out _))
                    errors.Add($"{path}: missing required property '{name}'.");
            }
        }

        bool allowAdditional = !schema.TryGetProperty("additionalProperties", out JsonElement additional)
            || additional.ValueKind != JsonValueKind.False;
        foreach (JsonProperty property in instance.EnumerateObject())
        {
            if (!hasProperties || !properties.TryGetProperty(property.Name, out JsonElement propertySchema))
            {
                if (!allowAdditional)
                    errors.Add($"{path}: unexpected property '{property.Name}'.");
                continue;
            }

            ValidateNode(propertySchema, property.Value, $"{path}.{property.Name}", errors);
        }
    }

    private JsonElement ResolveReference(string? pointer)
    {
        if (string.IsNullOrEmpty(pointer) || !pointer.StartsWith("#/", StringComparison.Ordinal))
            throw new InvalidOperationException("Canonical diagnostic schema contains an unsupported reference.");

        JsonElement current = _root;
        foreach (string rawSegment in pointer[2..].Split('/'))
        {
            string segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (!current.TryGetProperty(segment, out JsonElement next))
                throw new InvalidOperationException("Canonical diagnostic schema contains an unresolved reference.");
            current = next;
        }
        return current;
    }

    private static bool MatchesType(string type, JsonElement instance) => type switch
    {
        "object" => instance.ValueKind == JsonValueKind.Object,
        "array" => instance.ValueKind == JsonValueKind.Array,
        "string" => instance.ValueKind == JsonValueKind.String,
        "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "integer" => instance.ValueKind == JsonValueKind.Number,
        "number" => instance.ValueKind == JsonValueKind.Number,
        _ => true
    };
}
