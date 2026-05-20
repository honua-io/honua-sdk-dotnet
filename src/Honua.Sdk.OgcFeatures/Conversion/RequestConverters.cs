// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures.Conversion;


/// <summary>
/// Converts abstractions request DTOs to OGC API Features request and payload
/// shapes.
/// </summary>
/// <remarks>
/// Migrated from <c>Honua.Mobile.Sdk.SdkFeatureTransportMappings</c>. Exposed
/// as public surface so cross-assembly consumers (notably the mobile runtime)
/// can compose these conversions without duplicating logic.
/// </remarks>
public static class RequestConverters
{
    /// <summary>Converts a provider-neutral feature edit payload to an OGC GeoJSON feature.</summary>
    public static OgcFeature ToOgcFeature(FeatureEditFeature feature, string? featureId = null)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var id = featureId ?? ResolveOptionalFeatureId(feature);
        return new OgcFeature
        {
            Id = id is null
                ? null
                : JsonSerializer.SerializeToElement(id, OgcMobileTransportJsonContext.Default.String),
            Geometry = feature.Geometry?.Clone(),
            Properties = feature.Attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
        };
    }

    /// <summary>Formats a numeric object ID for use as an OGC feature identifier.</summary>
    public static string ToOgcFeatureId(long objectId)
        => objectId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Converts an abstractions OGC items request to OGC items query parameters.</summary>
    public static OgcItemsParams ToOgcItemsParams(OgcItemsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new OgcItemsParams
        {
            Limit = request.Limit,
            Offset = request.Offset,
            Properties = request.PropertyNames is { Count: > 0 } ? string.Join(',', request.PropertyNames) : null,
            Filter = request.CqlFilter,
            Format = ToOgcFeaturesFormat(request.ResponseFormat),
        };
    }

    /// <summary>Coerces an opaque OGC feature payload into an <see cref="OgcFeature"/>.</summary>
    public static OgcFeature ToOgcFeature(object feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        if (feature is OgcFeature ogcFeature)
        {
            return ogcFeature;
        }

        var json = feature switch
        {
            JsonElement element => element.GetRawText(),
            JsonDocument document => document.RootElement.GetRawText(),
            FeatureEditFeature editFeature => JsonSerializer.Serialize(
                ToOgcFeature(editFeature),
                OgcMobileTransportJsonContext.Default.OgcFeature),
            _ => SerializeWithContext(feature),
        };

        return JsonSerializer.Deserialize(json, OgcMobileTransportJsonContext.Default.OgcFeature)
            ?? throw new ArgumentException("OGC feature payload could not be deserialized.", nameof(feature));
    }

    /// <summary>Coerces an opaque value into a JSON element (used for OGC patch payloads).</summary>
    public static JsonElement ToJsonElement(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            JsonElement element => element.Clone(),
            JsonDocument document => document.RootElement.Clone(),
            _ => SerializeToElementWithContext(value),
        };
    }

    /// <summary>Serializes an OGC collection list to a legacy <see cref="JsonDocument"/> payload.</summary>
    public static JsonDocument ToJsonDocument(IReadOnlyList<OgcCollection> collections)
    {
        ArgumentNullException.ThrowIfNull(collections);
        return JsonDocument.Parse(JsonSerializer.Serialize(
            new OgcCollectionsEnvelope { Collections = collections },
            OgcMobileTransportJsonContext.Default.OgcCollectionsEnvelope));
    }

    /// <summary>Serializes an OGC feature collection to a legacy <see cref="JsonDocument"/> payload.</summary>
    public static JsonDocument ToJsonDocument(OgcFeatureCollection response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            OgcMobileTransportJsonContext.Default.OgcFeatureCollection));
    }

    /// <summary>Serializes an OGC feature to a legacy <see cref="JsonDocument"/> payload.</summary>
    public static JsonDocument ToJsonDocument(OgcFeature response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            OgcMobileTransportJsonContext.Default.OgcFeature));
    }

    private static string SerializeWithContext(object value)
    {
        try
        {
            return JsonSerializer.Serialize(value, value.GetType(), OgcMobileTransportJsonContext.Default);
        }
        catch (NotSupportedException ex)
        {
            throw new ArgumentException(
                "OGC feature payload must be an OgcFeature, FeatureEditFeature, JsonElement, JsonDocument, or a source-generated JSON type.",
                nameof(value),
                ex);
        }
    }

    private static JsonElement SerializeToElementWithContext(object value)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, value.GetType(), OgcMobileTransportJsonContext.Default);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch (NotSupportedException ex)
        {
            throw new ArgumentException(
                "OGC patch payload must be a JsonElement, JsonDocument, or a source-generated JSON type.",
                nameof(value),
                ex);
        }
    }

    private static string? ResolveOptionalFeatureId(FeatureEditFeature feature)
        => !string.IsNullOrWhiteSpace(feature.Id)
            ? feature.Id
            : feature.ObjectId?.ToString(CultureInfo.InvariantCulture);

    private static OgcFeaturesFormat? ToOgcFeaturesFormat(string? responseFormat)
    {
        var trimmed = responseFormat?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.Json;
        }

        if (trimmed.Equals("geojson", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.GeoJson;
        }

        if (trimmed.Equals("html", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.Html;
        }

        if (trimmed.Equals("gml", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.Gml;
        }

        if (trimmed.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.Csv;
        }

        if (trimmed.Equals("flatgeobuf", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.FlatGeobuf;
        }

        if (trimmed.Equals("parquet", StringComparison.OrdinalIgnoreCase))
        {
            return OgcFeaturesFormat.Parquet;
        }

        return null;
    }
}

internal sealed class OgcCollectionsEnvelope
{
    [JsonPropertyName("collections")]
    public IReadOnlyList<OgcCollection> Collections { get; init; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OgcCollectionsEnvelope))]
[JsonSerializable(typeof(OgcFeatureCollection))]
[JsonSerializable(typeof(OgcFeature))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(string))]
internal sealed partial class OgcMobileTransportJsonContext : JsonSerializerContext
{
}

