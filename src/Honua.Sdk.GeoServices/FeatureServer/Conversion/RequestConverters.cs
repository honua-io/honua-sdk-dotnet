// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.Geometry;

namespace Honua.Sdk.GeoServices.FeatureServer.Conversion;


/// <summary>
/// Converts abstractions request DTOs to FeatureServer REST request shapes
/// (query parameters, edit payloads, and form bodies).
/// </summary>
/// <remarks>
/// Migrated from <c>Honua.Mobile.Sdk.SdkFeatureTransportMappings</c>. Exposed
/// as public surface so cross-assembly consumers (notably the mobile runtime)
/// can compose these conversions without duplicating logic.
/// </remarks>
public static class RequestConverters
{
    /// <summary>Converts an abstractions apply-edits request to a FeatureServer edit request.</summary>
    public static FeatureServerEditRequest ToFeatureServerEditRequest(ApplyEditsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new FeatureServerEditRequest
        {
            Adds = ResolveFeatureServerFeatures(request.Adds, request.AddsJson, "adds"),
            Updates = ResolveFeatureServerFeatures(request.Updates, request.UpdatesJson, "updates"),
            Deletes = ResolveFeatureServerDeletes(request.Deletes, request.DeletesCsv),
            RollbackOnFailure = request.RollbackOnFailure,
            ForceWrite = request.ForceWrite,
        };
    }

    /// <summary>Converts an apply-edits request to FeatureServer form parameters suitable for POST bodies.</summary>
    public static IReadOnlyDictionary<string, string> ToFeatureServerEditFormParameters(ApplyEditsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var edit = ToFeatureServerEditRequest(request);
        var body = new Dictionary<string, string?>
        {
            ["f"] = request.ResponseFormat,
            ["adds"] = edit.Adds is { Count: > 0 } ? SerializeFeatureServerFeatures(edit.Adds) : null,
            ["updates"] = edit.Updates is { Count: > 0 } ? SerializeFeatureServerFeatures(edit.Updates) : null,
            ["deletes"] = edit.Deletes is { Count: > 0 } ? JoinInvariant(edit.Deletes) : null,
            ["rollbackOnFailure"] = edit.RollbackOnFailure ? "true" : "false",
            ["forceWrite"] = edit.ForceWrite ? "true" : null,
        };

        return body
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value!);
    }

    /// <summary>Converts a provider-neutral feature edit payload to a FeatureServer feature.</summary>
    /// <remarks>
    /// GeoJSON geometry payloads (<c>{"type":"Point","coordinates":[...]}</c> and friends) are
    /// projected to FeatureServer's Esri JSON shape (<c>{"x":...,"y":...}</c>, <c>{"paths":[...]}</c>,
    /// <c>{"rings":[...]}</c>, etc.) so callers can supply provider-neutral GeoJSON without each
    /// caller re-implementing the conversion. Geometries that already match the FeatureServer
    /// shape, or shapes the SDK cannot translate, are passed through unchanged.
    /// </remarks>
    public static FeatureServerFeature ToFeatureServerFeature(FeatureEditFeature feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        return new FeatureServerFeature
        {
            Attributes = feature.Attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone()),
            Geometry = ProjectGeometryForFeatureServer(feature.Geometry),
        };
    }

    /// <summary>
    /// Projects an inbound geometry <see cref="JsonElement"/> to the FeatureServer (Esri JSON) shape.
    /// Recognises GeoJSON Point/MultiPoint/LineString/MultiLineString/Polygon/MultiPolygon payloads
    /// via their <c>"type"</c> discriminator and routes them through
    /// <see cref="GeoJsonGeometryConverter"/> +
    /// <see cref="GeoServicesGeometryConverter"/>. Geometries that already look like FeatureServer
    /// shapes (have <c>x</c>/<c>y</c>, <c>rings</c>, <c>paths</c>, or <c>points</c>) or which we
    /// cannot translate (GeometryCollection, etc.) are cloned and returned verbatim so the wire
    /// payload is preserved for the server.
    /// </summary>
    private static JsonElement? ProjectGeometryForFeatureServer(JsonElement? geometry)
    {
        if (!geometry.HasValue)
        {
            return null;
        }

        var value = geometry.Value;
        if (value.ValueKind != JsonValueKind.Object)
        {
            return value.Clone();
        }

        if (!IsGeoJsonGeometry(value))
        {
            // Already FeatureServer-shaped (or an unknown object we should not mutate).
            return value.Clone();
        }

        try
        {
            var nts = GeoJsonGeometryConverter.ReadGeometry(value);
            return GeoServicesGeometryConverter.WriteGeometry(nts);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
        {
            // GeoJSON shape we cannot translate (e.g., GeometryCollection or malformed payload).
            // Fall back to the original payload so callers/servers can surface a precise error
            // rather than this converter silently swallowing the geometry.
            return value.Clone();
        }
    }

    private static bool IsGeoJsonGeometry(JsonElement geometry)
    {
        if (geometry.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!geometry.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return type.GetString() switch
        {
            "Point" or
            "MultiPoint" or
            "LineString" or
            "MultiLineString" or
            "Polygon" or
            "MultiPolygon" => true,
            _ => false,
        };
    }

    /// <summary>Resolves the numeric object IDs to delete from a provider-neutral edit request.</summary>
    public static IReadOnlyList<long>? ToFeatureServerDeleteObjectIds(FeatureEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var objectIds = new List<long>(request.DeleteObjectIds);
        foreach (var id in request.DeleteIds)
        {
            if (!long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
            {
                throw new ArgumentException("FeatureServer feature deletes require numeric feature IDs.", nameof(request));
            }

            objectIds.Add(objectId);
        }

        return objectIds.Count == 0 ? null : objectIds;
    }

    /// <summary>Converts an abstractions query request to FeatureServer query parameters.</summary>
    public static FeatureServerQueryParams ToFeatureServerQueryParams(QueryFeaturesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new FeatureServerQueryParams
        {
            Where = request.Where,
            ObjectIds = request.ObjectIds,
            OutFields = request.OutFields is { Count: > 0 } ? string.Join(',', request.OutFields) : "*",
            ReturnGeometry = request.ReturnGeometry,
            ResultOffset = request.ResultOffset,
            ResultRecordCount = request.ResultRecordCount,
            OrderByFields = request.OrderBy,
            ReturnDistinctValues = request.ReturnDistinct ? true : null,
            ReturnCountOnly = request.ReturnCountOnly ? true : null,
            ReturnIdsOnly = request.ReturnIdsOnly ? true : null,
            ReturnExtentOnly = request.ReturnExtentOnly ? true : null,
            Format = ToFeatureServerFormat(request.ResponseFormat),
        };
    }

    /// <summary>Serializes a FeatureServer query response to a legacy <see cref="JsonDocument"/> payload.</summary>
    public static JsonDocument ToJsonDocument(FeatureServerQueryResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            MobileTransportJsonContext.Default.FeatureServerQueryResponse));
    }

    /// <summary>Serializes a FeatureServer edit response to a legacy <see cref="JsonDocument"/> payload.</summary>
    public static JsonDocument ToJsonDocument(FeatureServerEditResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            MobileTransportJsonContext.Default.FeatureServerEditResponse));
    }

    private static FeatureServerFeature[]? ResolveFeatureServerFeatures(
        IReadOnlyList<FeatureEditFeature>? features,
        string? json,
        string payloadName)
    {
        if (features is { Count: > 0 })
        {
            return features.Select(ToFeatureServerFeature).ToArray();
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(
                json,
                MobileTransportJsonContext.Default.FeatureServerFeatureArray);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"FeatureServer {payloadName} JSON payload is invalid.", payloadName, ex);
        }
    }

    private static IReadOnlyList<long>? ResolveFeatureServerDeletes(IReadOnlyList<long>? deletes, string? deletesCsv)
    {
        if (deletes is { Count: > 0 })
        {
            return deletes;
        }

        if (string.IsNullOrWhiteSpace(deletesCsv))
        {
            return null;
        }

        var objectIds = new List<long>();
        foreach (var value in deletesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
            {
                throw new ArgumentException("FeatureServer deletesCsv payload must contain numeric object IDs.", nameof(deletesCsv));
            }

            objectIds.Add(objectId);
        }

        return objectIds;
    }

    private static string SerializeFeatureServerFeatures(IReadOnlyList<FeatureServerFeature> features)
        => JsonSerializer.Serialize(
            features.ToArray(),
            MobileTransportJsonContext.Default.FeatureServerFeatureArray);

    private static string JoinInvariant(IEnumerable<long> values)
        => string.Join(',', values.Select(value => value.ToString(CultureInfo.InvariantCulture)));

    private static FeatureServerFormat? ToFeatureServerFormat(string? responseFormat)
    {
        var trimmed = responseFormat?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureServerFormat.Json;
        }

        if (trimmed.Equals("geojson", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureServerFormat.GeoJson;
        }

        if (trimmed.Equals("pbf", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureServerFormat.Pbf;
        }

        if (trimmed.Equals("flatgeobuf", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureServerFormat.FlatGeobuf;
        }

        if (trimmed.Equals("parquet", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureServerFormat.Parquet;
        }

        return null;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FeatureServerFeature[]))]
[JsonSerializable(typeof(FeatureServerQueryResponse))]
[JsonSerializable(typeof(FeatureServerEditResponse))]
internal sealed partial class MobileTransportJsonContext : JsonSerializerContext
{
}

