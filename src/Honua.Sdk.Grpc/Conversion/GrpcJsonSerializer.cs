// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Buffers;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Grpc.Conversion;

internal enum NullPropertyHandling
{
    Include,
    Omit
}

// Native AOT-safe JSON serialization helpers for gRPC feature attributes and GeoServices geometries.
internal static class GrpcJsonSerializer
{
    public static JsonElement SerializeDictionaryToElement(IReadOnlyDictionary<string, object?> dictionary)
        => SerializeDictionary(dictionary, NullPropertyHandling.Include);

    public static JsonElement SerializeDictionaryOmittingNulls(IReadOnlyDictionary<string, object?> dictionary)
        => SerializeDictionary(dictionary, NullPropertyHandling.Omit);

    public static JsonElement SerializeValueToElement(object? value)
        => SerializeValue(value, NullPropertyHandling.Include);

    public static JsonElement SerializeValueOmittingNulls(object? value)
        => SerializeValue(value, NullPropertyHandling.Omit);

    private static JsonElement SerializeDictionary(
        IReadOnlyDictionary<string, object?> dictionary,
        NullPropertyHandling nullHandling)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteDictionary(writer, dictionary, nullHandling);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static JsonElement SerializeValue(object? value, NullPropertyHandling nullHandling)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteValue(writer, value, nullHandling);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value, NullPropertyHandling nullHandling)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        if (TryWritePrimitive(writer, value))
        {
            return;
        }

        if (TryWriteCollection(writer, value, nullHandling))
        {
            return;
        }

        WriteSourceGeneratedFallback(writer, value);
    }

    private static bool TryWritePrimitive(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case JsonElement element:
                element.WriteTo(writer);
                return true;
            case JsonDocument document:
                document.RootElement.WriteTo(writer);
                return true;
            case string text:
                writer.WriteStringValue(text);
                return true;
            case bool flag:
                writer.WriteBooleanValue(flag);
                return true;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                return true;
            case ReadOnlyMemory<byte> memory:
                writer.WriteBase64StringValue(memory.Span);
                return true;
            default:
                return TryWriteTemporalOrGuid(writer, value) || TryWriteNumber(writer, value);
        }
    }

    private static bool TryWriteTemporalOrGuid(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case DateTime dateTime:
                writer.WriteStringValue(dateTime);
                return true;
            case DateTimeOffset dateTimeOffset:
                writer.WriteStringValue(dateTimeOffset);
                return true;
            case DateOnly dateOnly:
                writer.WriteStringValue(dateOnly.ToString("O", CultureInfo.InvariantCulture));
                return true;
            case TimeOnly timeOnly:
                writer.WriteStringValue(timeOnly.ToString("O", CultureInfo.InvariantCulture));
                return true;
            case Guid guid:
                writer.WriteStringValue(guid);
                return true;
            default:
                return false;
        }
    }

    private static bool TryWriteNumber(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case int i:
                writer.WriteNumberValue(i);
                return true;
            case long l:
                writer.WriteNumberValue(l);
                return true;
            case double d:
                writer.WriteNumberValue(d);
                return true;
            case float f:
                writer.WriteNumberValue(f);
                return true;
            case decimal m:
                writer.WriteNumberValue(m);
                return true;
            case short s:
                writer.WriteNumberValue(s);
                return true;
            case byte b:
                writer.WriteNumberValue(b);
                return true;
            case sbyte sb:
                writer.WriteNumberValue(sb);
                return true;
            case ushort us:
                writer.WriteNumberValue(us);
                return true;
            case uint ui:
                writer.WriteNumberValue(ui);
                return true;
            case ulong ul:
                writer.WriteNumberValue(ul);
                return true;
            default:
                return false;
        }
    }

    private static bool TryWriteCollection(
        Utf8JsonWriter writer,
        object value,
        NullPropertyHandling nullHandling)
    {
        if (value is IReadOnlyDictionary<string, object?> readOnlyDict)
        {
            WriteDictionary(writer, readOnlyDict, nullHandling);
            return true;
        }

        if (value is IDictionary<string, object?> mutableDict)
        {
            WriteDictionary(writer, mutableDict, nullHandling);
            return true;
        }

        if (value is IDictionary nonGenericDict)
        {
            WriteNonGenericDictionary(writer, nonGenericDict, nullHandling);
            return true;
        }

        if (value is IEnumerable items)
        {
            WriteArray(writer, items, nullHandling);
            return true;
        }

        return false;
    }

    private static void WriteDictionary(
        Utf8JsonWriter writer,
        IEnumerable<KeyValuePair<string, object?>> entries,
        NullPropertyHandling nullHandling)
    {
        writer.WriteStartObject();
        foreach (var entry in entries)
        {
            if (nullHandling == NullPropertyHandling.Omit && entry.Value is null)
            {
                continue;
            }

            writer.WritePropertyName(entry.Key);
            WriteValue(writer, entry.Value, nullHandling);
        }

        writer.WriteEndObject();
    }

    private static void WriteNonGenericDictionary(
        Utf8JsonWriter writer,
        IDictionary entries,
        NullPropertyHandling nullHandling)
    {
        writer.WriteStartObject();
        foreach (DictionaryEntry entry in entries)
        {
            if (nullHandling == NullPropertyHandling.Omit && entry.Value is null)
            {
                continue;
            }

            var key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty;
            writer.WritePropertyName(key);
            WriteValue(writer, entry.Value, nullHandling);
        }

        writer.WriteEndObject();
    }

    private static void WriteArray(Utf8JsonWriter writer, IEnumerable items, NullPropertyHandling nullHandling)
    {
        writer.WriteStartArray();
        foreach (var item in items)
        {
            WriteValue(writer, item, nullHandling);
        }

        writer.WriteEndArray();
    }

    private static void WriteSourceGeneratedFallback(Utf8JsonWriter writer, object value)
    {
        var typeInfo = GrpcJsonContext.Default.GetTypeInfo(value.GetType());
        if (typeInfo is null)
        {
            throw new NotSupportedException(
                $"Value of type '{value.GetType().FullName}' cannot be serialized to a JSON element in Native AOT mode.");
        }

        JsonSerializer.Serialize(writer, value, typeInfo);
    }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(decimal))]
[JsonSerializable(typeof(byte[]))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(DateTimeOffset))]
[JsonSerializable(typeof(DateOnly))]
[JsonSerializable(typeof(TimeOnly))]
[JsonSerializable(typeof(Guid))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class GrpcJsonContext : JsonSerializerContext
{
}
