// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Geometry;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Conversion;

/// <summary>
/// Converts between proto-generated types and SDK domain models.
/// </summary>
internal static class ProtoAdapter
{
    /// <summary>
    /// Converts a domain query request to a proto request.
    /// </summary>
    public static Proto.QueryFeaturesRequest ToProtoRequest(Models.QueryFeaturesRequest request)
    {
        var proto = new Proto.QueryFeaturesRequest
        {
            ServiceId = request.ServiceId,
            LayerId = request.LayerId,
            Where = request.Where ?? "1=1",
            ReturnGeometry = request.ReturnGeometry,
            ResultOffsetLong = request.ResultOffset,
            ResultRecordCountLong = request.ResultRecordCount ?? 0,
            OrderBy = request.OrderBy ?? string.Empty,
            ReturnDistinct = request.ReturnDistinct,
            ReturnCountOnly = request.ReturnCountOnly,
            ReturnIdsOnly = request.ReturnIdsOnly,
            ReturnExtentOnly = request.ReturnExtentOnly,
            GeometryPrecision = request.GeometryPrecision,
            MaxAllowableOffset = request.MaxAllowableOffset,
        };

        if (request.ObjectIds is not null)
        {
            proto.ObjectIds.AddRange(request.ObjectIds);
        }

        if (request.OutFields is not null)
        {
            proto.OutFields.AddRange(request.OutFields);
        }

        if (request.OutSr is not null)
        {
            proto.OutSr = new Proto.SpatialReference
            {
                Wkid = request.OutSr.Wkid,
                LatestWkid = request.OutSr.LatestWkid,
                Wkt = request.OutSr.Wkt,
            };
        }

        if (request.OutStatistics is not null)
        {
            foreach (var stat in request.OutStatistics)
            {
                proto.OutStatistics.Add(new Proto.StatisticDefinition
                {
                    OnStatisticField = stat.OnStatisticField,
                    StatisticType = (Proto.StatisticType)stat.StatisticType,
                    OutStatisticFieldName = stat.OutStatisticFieldName,
                });
            }
        }

        if (request.GroupBy is not null)
        {
            proto.GroupBy.AddRange(request.GroupBy);
        }

        if (request.SpatialFilter is not null)
        {
            proto.SpatialFilter = ConvertSpatialFilter(request.SpatialFilter);
        }

        return proto;
    }

    /// <summary>
    /// Converts a proto query response to a domain response.
    /// </summary>
    public static Models.QueryFeaturesResponse FromProtoResponse(Proto.QueryFeaturesResponse response)
    {
        return new Models.QueryFeaturesResponse
        {
            ObjectIdFieldName = response.ObjectIdFieldName,
            GeometryType = (Models.GeometryType)response.GeometryType,
            SpatialReference = response.SpatialReference is not null ? ConvertSpatialReference(response.SpatialReference) : null,
            Fields = response.Fields.Select(ConvertField).ToList(),
            Features = response.Features.Select(ConvertFeature).ToList(),
            ExceededTransferLimit = response.ExceededTransferLimit,
            Count = response.Count,
            ObjectIds = response.ObjectIds.ToList(),
            Extent = response.Extent is not null ? ConvertExtent(response.Extent) : null,
        };
    }

    /// <summary>
    /// Converts a proto feature page to a domain feature page.
    /// </summary>
    public static Models.FeaturePage FromProtoPage(Proto.FeaturePage page)
    {
        return new Models.FeaturePage
        {
            ObjectIdFieldName = page.ObjectIdFieldName,
            GeometryType = (Models.GeometryType)page.GeometryType,
            SpatialReference = page.SpatialReference is not null ? ConvertSpatialReference(page.SpatialReference) : null,
            Fields = page.Fields.Select(ConvertField).ToList(),
            Features = page.Features.Select(ConvertFeature).ToList(),
            IsLastPage = page.IsLastPage,
        };
    }

    /// <summary>
    /// Converts a domain apply-edits request to a proto request.
    /// </summary>
    public static Proto.ApplyEditsRequest ToProtoApplyEditsRequest(Models.ApplyEditsRequest request)
    {
        var proto = new Proto.ApplyEditsRequest
        {
            ServiceId = request.ServiceId,
            LayerId = request.LayerId,
            RollbackOnFailure = request.RollbackOnFailure,
            ForceWrite = request.ForceWrite,
        };

        if (request.Adds is not null)
        {
            foreach (var feature in request.Adds)
            {
                proto.Adds.Add(ConvertFeatureToProto(feature));
            }
        }

        if (request.Updates is not null)
        {
            foreach (var feature in request.Updates)
            {
                proto.Updates.Add(ConvertFeatureToProto(feature));
            }
        }

        if (request.Deletes is not null)
        {
            proto.Deletes.AddRange(request.Deletes);
        }

        return proto;
    }

    /// <summary>
    /// Converts a proto apply-edits response to a domain response.
    /// </summary>
    public static Models.ApplyEditsResponse FromProtoApplyEditsResponse(Proto.ApplyEditsResponse response)
    {
        return new Models.ApplyEditsResponse
        {
            AddResults = response.AddResults.Select(ConvertEditResult).ToList(),
            UpdateResults = response.UpdateResults.Select(ConvertEditResult).ToList(),
            DeleteResults = response.DeleteResults.Select(ConvertEditResult).ToList(),
            Error = response.Error is not null ? ConvertEditError(response.Error) : null,
        };
    }

    internal static Proto.Feature ConvertFeatureToProto(Models.Feature feature)
    {
        var proto = new Proto.Feature { Id = feature.Id };

        foreach (var kvp in feature.Attributes)
        {
            proto.Attributes[kvp.Key] = ConvertAttributeToProto(kvp.Value);
        }

        if (feature.NtsGeometry is not null)
        {
            // NTS-native write path: convert straight to the proto with no Esri JSON round-trip.
            proto.Geometry = GrpcGeometryConverter.WriteGeometry(feature.NtsGeometry);
        }
        else if (feature.Geometry is not null)
        {
            var conversion = ConvertGeometryToProto(feature.Geometry);
            proto.Geometry = conversion.Geometry;
        }

        return proto;
    }

    internal static Proto.AttributeValue ConvertAttributeToProto(object? value)
    {
        var attr = new Proto.AttributeValue();
        switch (value)
        {
            case null:
                attr.NullValue = Proto.NullValue.NullValue;
                break;
            case string s:
                attr.StringValue = s;
                break;
            case int i:
                attr.Int32Value = i;
                break;
            case long l:
                attr.Int64Value = l;
                break;
            case double d:
                attr.DoubleValue = d;
                break;
            case float f:
                attr.FloatValue = f;
                break;
            case bool b:
                attr.BoolValue = b;
                break;
            case DateTime dateTime:
                // Treat Unspecified as UTC rather than local time. new DateTimeOffset(dateTime)
                // interprets Kind=Unspecified using the host timezone, so the same instant would
                // serialize to different wire values per machine. Normalize to UTC for a stable,
                // timezone-independent round-trip (the read path returns Kind=Utc).
                var utcDateTime = dateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                    : dateTime;
                attr.DatetimeValue = new DateTimeOffset(utcDateTime).ToUnixTimeMilliseconds();
                break;
            case DateTimeOffset dateTimeOffset:
                attr.DatetimeValue = dateTimeOffset.ToUnixTimeMilliseconds();
                break;
            case byte[] bytes:
                attr.BytesValue = Google.Protobuf.ByteString.CopyFrom(bytes);
                break;
            default:
                attr.StringValue = value.ToString() ?? string.Empty;
                break;
        }

        return attr;
    }

    private static Models.EditResult ConvertEditResult(Proto.EditResult result)
    {
        return new Models.EditResult
        {
            ObjectId = result.ObjectId,
            Success = result.Success,
            Error = result.Error is not null ? ConvertEditError(result.Error) : null,
        };
    }

    private static Models.EditError ConvertEditError(Proto.ErrorDetail error)
    {
        return new Models.EditError
        {
            Code = error.Code,
            Message = error.Message,
        };
    }

    internal static Models.Feature ConvertFeature(Proto.Feature feature)
    {
        var attributes = new Dictionary<string, object?>();
        foreach (var kvp in feature.Attributes)
        {
            attributes[kvp.Key] = ConvertAttribute(kvp.Value);
        }

        NetTopologySuite.Geometries.Geometry? ntsGeometry = null;
        IReadOnlyDictionary<string, object?>? esriGeometry = null;
        if (feature.Geometry is not null && feature.Geometry.ShapeCase != Proto.Geometry.ShapeOneofCase.None)
        {
            // Read the proto geometry once into NTS (the proto-native shape) and derive the Esri
            // JSON dictionary from that same instance, so there is no serialize-then-reparse and the
            // existing proto->NTS converter is the source of truth for SDK query results.
            ntsGeometry = GrpcGeometryConverter.ReadGeometry(feature.Geometry);
            esriGeometry = ConvertGeometry(ntsGeometry);
        }

        return new Models.Feature
        {
            Id = feature.Id,
            Attributes = attributes,
            NtsGeometry = ntsGeometry,
            Geometry = esriGeometry,
        };
    }

    internal static object? ConvertAttribute(Proto.AttributeValue attr)
    {
        return attr.ValueCase switch
        {
            Proto.AttributeValue.ValueOneofCase.StringValue => attr.StringValue,
            Proto.AttributeValue.ValueOneofCase.Int32Value => attr.Int32Value,
            Proto.AttributeValue.ValueOneofCase.Int64Value => attr.Int64Value,
            Proto.AttributeValue.ValueOneofCase.DoubleValue => attr.DoubleValue,
            Proto.AttributeValue.ValueOneofCase.FloatValue => (double)attr.FloatValue,
            Proto.AttributeValue.ValueOneofCase.BoolValue => attr.BoolValue,
            Proto.AttributeValue.ValueOneofCase.DatetimeValue => DateTimeOffset.FromUnixTimeMilliseconds(attr.DatetimeValue).UtcDateTime,
            Proto.AttributeValue.ValueOneofCase.BytesValue => attr.BytesValue.ToByteArray(),
            Proto.AttributeValue.ValueOneofCase.NullValue => null,
            Proto.AttributeValue.ValueOneofCase.None => null,
            _ => null,
        };
    }

    internal static IReadOnlyDictionary<string, object?>? ConvertGeometry(Proto.Geometry geometry)
    {
        if (geometry.ShapeCase == Proto.Geometry.ShapeOneofCase.None)
        {
            return null;
        }

        return ConvertGeometry(GrpcGeometryConverter.ReadGeometry(geometry));
    }

    private static IReadOnlyDictionary<string, object?>? ConvertGeometry(
        NetTopologySuite.Geometries.Geometry ntsGeometry)
    {
        var geoServicesJson = GeoServicesGeometryConverter.WriteGeometry(ntsGeometry);
        return UnwrapGeometryJsonValue(geoServicesJson) as IReadOnlyDictionary<string, object?>;
    }

    private static Proto.SpatialFilter ConvertSpatialFilter(Models.SpatialFilter spatialFilter)
    {
        var proto = new Proto.SpatialFilter
        {
            SpatialRelationship = (Proto.SpatialRelationship)spatialFilter.SpatialRelationship,
            Distance = spatialFilter.Distance,
            DistanceUnit = (Proto.DistanceUnit)spatialFilter.DistanceUnit,
            NearestCount = spatialFilter.NearestCount,
            ReturnDistance = spatialFilter.ReturnDistance
        };

        Proto.SpatialReference? geometrySpatialReference = null;
        if (spatialFilter.Geometry is not null)
        {
            var conversion = ConvertGeometryToProto(spatialFilter.Geometry);
            proto.Geometry = conversion.Geometry;
            geometrySpatialReference = conversion.SpatialReference;
        }

        if (spatialFilter.SpatialReference is not null)
        {
            proto.SpatialReference = new Proto.SpatialReference
            {
                Wkid = spatialFilter.SpatialReference.Wkid,
                LatestWkid = spatialFilter.SpatialReference.LatestWkid,
                Wkt = spatialFilter.SpatialReference.Wkt ?? string.Empty
            };
        }
        else if (geometrySpatialReference is not null)
        {
            proto.SpatialReference = geometrySpatialReference;
        }

        return proto;
    }

    private static (Proto.Geometry Geometry, Proto.SpatialReference? SpatialReference) ConvertGeometryToProto(
        IReadOnlyDictionary<string, object?> geometry)
    {
        var spatialReference = TryGetSpatialReference(geometry);
        var geometryJson = JsonSerializer.SerializeToElement(geometry);
        var ntsGeometry = GeoServicesGeometryConverter.ReadGeometry(geometryJson);
        return (GrpcGeometryConverter.WriteGeometry(ntsGeometry), spatialReference);
    }

    private static Proto.SpatialReference? TryGetSpatialReference(IReadOnlyDictionary<string, object?> geometry)
    {
        if (!TryGetValue(geometry, "spatialReference", out var spatialReferenceValue) ||
            !TryAsDictionary(spatialReferenceValue, out var sr))
        {
            return null;
        }

        var proto = new Proto.SpatialReference();

        if (TryGetNumber(sr, "wkid", out var wkid))
        {
            proto.Wkid = (int)wkid;
        }

        if (TryGetNumber(sr, "latestWkid", out var latestWkid))
        {
            proto.LatestWkid = (int)latestWkid;
        }

        if (TryGetValue(sr, "wkt", out var wkt) && wkt is string wktText)
        {
            proto.Wkt = wktText;
        }

        return proto.Wkid == 0 && proto.LatestWkid == 0 && string.IsNullOrWhiteSpace(proto.Wkt)
            ? null
            : proto;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> values, string key, out object? value)
    {
        if (values.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var pair in values)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = pair.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryAsDictionary(object? value, out IReadOnlyDictionary<string, object?> result)
    {
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Object)
        {
            var jsonObject = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                jsonObject[property.Name] = UnwrapJsonValue(property.Value);
            }

            result = jsonObject;
            return true;
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            result = readOnlyDictionary;
            return true;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            result = new Dictionary<string, object?>(dictionary, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        return false;
    }

    private static bool TryGetNumber(IReadOnlyDictionary<string, object?> values, string key, out double result)
    {
        if (TryGetValue(values, key, out var value))
        {
            if (TryConvertToDouble(value, out result))
            {
                return true;
            }
        }

        result = 0;
        return false;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case null:
                result = 0;
                return false;
            case JsonElement element when element.ValueKind == JsonValueKind.Number:
                return element.TryGetDouble(out result);
            case JsonElement element when element.ValueKind == JsonValueKind.String:
                return double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case decimal dec:
                result = (double)dec;
                return true;
            case byte b:
                result = b;
                return true;
            case short s:
                result = s;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case string str:
                return double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            default:
                if (value is IConvertible convertible)
                {
                    try
                    {
                        result = convertible.ToDouble(CultureInfo.InvariantCulture);
                        return true;
                    }
                    catch (FormatException)
                    {
                    }
                    catch (InvalidCastException)
                    {
                    }
                    catch (OverflowException)
                    {
                    }
                }

                result = 0;
                return false;
        }
    }

    internal static object? UnwrapJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var i64)
                ? i64
                : element.TryGetDouble(out var dbl)
                    ? dbl
                    : (object?)element.GetRawText(),
            JsonValueKind.Array => element.EnumerateArray().Select(UnwrapJsonValue).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                prop => prop.Name,
                prop => UnwrapJsonValue(prop.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    internal static object? UnwrapGeometryJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.Array => element.EnumerateArray().Select(UnwrapGeometryJsonValue).ToList(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                prop => prop.Name,
                prop => UnwrapGeometryJsonValue(prop.Value),
                StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    private static Models.SpatialReference ConvertSpatialReference(Proto.SpatialReference sr)
    {
        var spatialReference = GrpcGeometryConverter.ReadSpatialReference(sr);
        return new Models.SpatialReference
        {
            Wkid = spatialReference?.Wkid ?? sr.Wkid,
            LatestWkid = spatialReference?.LatestWkid ?? sr.LatestWkid,
            Wkt = spatialReference?.Wkt ?? sr.Wkt,
        };
    }

    private static Models.Extent ConvertExtent(Proto.Extent extent)
    {
        return new Models.Extent
        {
            Xmin = extent.Xmin,
            Ymin = extent.Ymin,
            Xmax = extent.Xmax,
            Ymax = extent.Ymax,
            SpatialReference = extent.SpatialReference is not null ? ConvertSpatialReference(extent.SpatialReference) : null,
        };
    }

    private static Models.FieldDefinition ConvertField(Proto.FieldDefinition field)
    {
        return new Models.FieldDefinition
        {
            Name = field.Name,
            FieldType = (Models.FieldType)field.FieldType,
            Length = field.Length,
            Nullable = field.Nullable,
        };
    }
}
