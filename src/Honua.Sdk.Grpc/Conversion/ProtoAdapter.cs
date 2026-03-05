// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Collections;
using System.Text.Json;
using Proto = Honua.Server.Features.Grpc.Proto;

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
            Where = request.Where,
            ReturnGeometry = request.ReturnGeometry,
            ResultOffset = request.ResultOffset,
            ResultRecordCount = request.ResultRecordCount,
            OrderBy = request.OrderBy,
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

    internal static Models.Feature ConvertFeature(Proto.Feature feature)
    {
        var attributes = new Dictionary<string, object?>();
        foreach (var kvp in feature.Attributes)
        {
            attributes[kvp.Key] = ConvertAttribute(kvp.Value);
        }

        return new Models.Feature
        {
            Id = feature.Id,
            Attributes = attributes,
            Geometry = feature.Geometry is not null ? ConvertGeometry(feature.Geometry) : null,
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
            Proto.AttributeValue.ValueOneofCase.DatetimeValue => attr.DatetimeValue,
            Proto.AttributeValue.ValueOneofCase.BytesValue => attr.BytesValue.ToByteArray(),
            Proto.AttributeValue.ValueOneofCase.NullValue => null,
            Proto.AttributeValue.ValueOneofCase.None => null,
            _ => null,
        };
    }

    internal static IReadOnlyDictionary<string, object?>? ConvertGeometry(Proto.Geometry geometry)
    {
        return geometry.ShapeCase switch
        {
            Proto.Geometry.ShapeOneofCase.Point => ConvertPoint(geometry.Point),
            Proto.Geometry.ShapeOneofCase.MultiPoint => ConvertMultiPoint(geometry.MultiPoint),
            Proto.Geometry.ShapeOneofCase.Polyline => ConvertPolyline(geometry.Polyline),
            Proto.Geometry.ShapeOneofCase.Polygon => ConvertPolygon(geometry.Polygon),
            Proto.Geometry.ShapeOneofCase.MultiPolygon => ConvertMultiPolygon(geometry.MultiPolygon),
            _ => null,
        };
    }

    private static Dictionary<string, object?> ConvertPoint(Proto.PointGeometry point)
    {
        var result = new Dictionary<string, object?>
        {
            ["x"] = point.X,
            ["y"] = point.Y,
        };
        if (point.HasZ)
            result["z"] = point.Z;
        if (point.HasM)
            result["m"] = point.M;
        return result;
    }

    private static Dictionary<string, object?> ConvertMultiPoint(Proto.MultiPointGeometry multiPoint)
    {
        var points = new List<object?>();
        foreach (var p in multiPoint.Points)
        {
            var coords = new List<object?> { p.X, p.Y };
            if (p.HasZ)
            {
                coords.Add(p.Z);
            }
            else if (p.HasM)
            {
                coords.Add(null);
            }
            if (p.HasM)
            {
                coords.Add(p.M);
            }
            points.Add(coords);
        }
        return new Dictionary<string, object?> { ["points"] = points };
    }

    private static Dictionary<string, object?> ConvertPolyline(Proto.PolylineGeometry polyline)
    {
        var paths = new List<object?>();
        foreach (var path in polyline.Paths)
        {
            var coords = new List<object?>();
            foreach (var c in path.Coords)
            {
                var coord = new List<object?> { c.X, c.Y };
                if (c.HasZ)
                {
                    coord.Add(c.Z);
                }
                else if (c.HasM)
                {
                    coord.Add(null);
                }
                if (c.HasM)
                {
                    coord.Add(c.M);
                }
                coords.Add(coord);
            }
            paths.Add(coords);
        }
        return new Dictionary<string, object?> { ["paths"] = paths };
    }

    private static Dictionary<string, object?> ConvertPolygon(Proto.PolygonGeometry polygon)
    {
        var rings = new List<object?>();
        foreach (var ring in polygon.Rings)
        {
            var coords = new List<object?>();
            foreach (var c in ring.Coords)
            {
                var coord = new List<object?> { c.X, c.Y };
                if (c.HasZ)
                {
                    coord.Add(c.Z);
                }
                else if (c.HasM)
                {
                    coord.Add(null);
                }
                if (c.HasM)
                {
                    coord.Add(c.M);
                }
                coords.Add(coord);
            }
            rings.Add(coords);
        }
        return new Dictionary<string, object?> { ["rings"] = rings };
    }

    private static Dictionary<string, object?> ConvertMultiPolygon(Proto.MultiPolygonGeometry multiPolygon)
    {
        var rings = new List<object?>();
        foreach (var poly in multiPolygon.Polygons)
        {
            foreach (var ring in poly.Rings)
            {
                var coords = new List<object?>();
                foreach (var c in ring.Coords)
                {
                    var coord = new List<object?> { c.X, c.Y };
                    if (c.HasZ)
                    {
                        coord.Add(c.Z);
                    }
                    else if (c.HasM)
                    {
                        coord.Add(null);
                    }
                    if (c.HasM)
                    {
                        coord.Add(c.M);
                    }
                    coords.Add(coord);
                }
                rings.Add(coords);
            }
        }
        return new Dictionary<string, object?> { ["rings"] = rings };
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
        var proto = new Proto.Geometry();
        var spatialReference = TryGetSpatialReference(geometry);

        if (TryGetNumber(geometry, "x", out var x) && TryGetNumber(geometry, "y", out var y))
        {
            var point = new Proto.PointGeometry
            {
                X = x,
                Y = y
            };
            if (TryGetNumber(geometry, "z", out var z))
            {
                point.Z = z;
            }

            if (TryGetNumber(geometry, "m", out var m))
            {
                point.M = m;
            }

            proto.Point = point;
            return (proto, spatialReference);
        }

        if (TryGetNumber(geometry, "xmin", out var xmin) &&
            TryGetNumber(geometry, "ymin", out var ymin) &&
            TryGetNumber(geometry, "xmax", out var xmax) &&
            TryGetNumber(geometry, "ymax", out var ymax))
        {
            var polygon = new Proto.PolygonGeometry();
            polygon.Rings.Add(new Proto.CoordinateSequence
            {
                Coords =
                {
                    CreateCoordinate(xmin, ymin),
                    CreateCoordinate(xmax, ymin),
                    CreateCoordinate(xmax, ymax),
                    CreateCoordinate(xmin, ymax),
                    CreateCoordinate(xmin, ymin)
                }
            });
            proto.Polygon = polygon;
            return (proto, spatialReference);
        }

        if (TryGetEnumerable(geometry, "points", out var points))
        {
            var multipoint = new Proto.MultiPointGeometry();
            foreach (var pointValue in points)
            {
                multipoint.Points.Add(CreatePointGeometry(pointValue, "points"));
            }

            proto.MultiPoint = multipoint;
            return (proto, spatialReference);
        }

        if (TryGetEnumerable(geometry, "paths", out var paths))
        {
            var polyline = new Proto.PolylineGeometry();
            foreach (var path in paths)
            {
                polyline.Paths.Add(CreateCoordinateSequence(path, "paths"));
            }

            proto.Polyline = polyline;
            return (proto, spatialReference);
        }

        if (TryGetEnumerable(geometry, "rings", out var rings))
        {
            var polygon = new Proto.PolygonGeometry();
            foreach (var ring in rings)
            {
                polygon.Rings.Add(CreateCoordinateSequence(ring, "rings"));
            }

            proto.Polygon = polygon;
            return (proto, spatialReference);
        }

        throw new ArgumentException("Unsupported geometry shape for gRPC spatial filter.");
    }

    private static Proto.PointGeometry CreatePointGeometry(object? pointValue, string context)
    {
        if (!TryAsEnumerable(pointValue, out var values))
        {
            throw new ArgumentException($"Invalid coordinate array in {context}.");
        }

        var list = values.ToList();
        if (list.Count < 2)
        {
            throw new ArgumentException($"Invalid coordinate array in {context}.");
        }

        var point = new Proto.PointGeometry
        {
            X = ConvertToDouble(list[0], context),
            Y = ConvertToDouble(list[1], context)
        };

        if (list.Count > 2 && list[2] is not null)
        {
            point.Z = ConvertToDouble(list[2], context);
        }

        if (list.Count > 3 && list[3] is not null)
        {
            point.M = ConvertToDouble(list[3], context);
        }

        return point;
    }

    private static Proto.CoordinateSequence CreateCoordinateSequence(object? sequenceValue, string context)
    {
        if (!TryAsEnumerable(sequenceValue, out var values))
        {
            throw new ArgumentException($"Invalid coordinate sequence in {context}.");
        }

        var sequence = new Proto.CoordinateSequence();
        foreach (var coordinate in values)
        {
            sequence.Coords.Add(CreateCoordinate(coordinate, context));
        }

        return sequence;
    }

    private static Proto.Coordinate CreateCoordinate(object? coordinateValue, string context)
    {
        if (!TryAsEnumerable(coordinateValue, out var values))
        {
            throw new ArgumentException($"Invalid coordinate in {context}.");
        }

        var list = values.ToList();
        if (list.Count < 2)
        {
            throw new ArgumentException($"Invalid coordinate in {context}.");
        }

        var coordinate = new Proto.Coordinate
        {
            X = ConvertToDouble(list[0], context),
            Y = ConvertToDouble(list[1], context)
        };

        if (list.Count > 2 && list[2] is not null)
        {
            coordinate.Z = ConvertToDouble(list[2], context);
        }

        if (list.Count > 3 && list[3] is not null)
        {
            coordinate.M = ConvertToDouble(list[3], context);
        }

        return coordinate;
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

    private static bool TryGetEnumerable(IReadOnlyDictionary<string, object?> values, string key, out IEnumerable<object?> result)
    {
        if (TryGetValue(values, key, out var value) &&
            TryAsEnumerable(value, out var enumerable))
        {
            result = enumerable;
            return true;
        }

        result = Array.Empty<object?>();
        return false;
    }

    private static bool TryAsEnumerable(object? value, out IEnumerable<object?> result)
    {
        if (value is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            result = element.EnumerateArray().Select(UnwrapJsonValue).ToList();
            return true;
        }

        if (value is IEnumerable enumerable && value is not string)
        {
            result = enumerable.Cast<object?>();
            return true;
        }

        result = Array.Empty<object?>();
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

    private static double ConvertToDouble(object? value, string context)
    {
        if (TryConvertToDouble(value, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Coordinate value in {context} is not a number.");
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

    private static object? UnwrapJsonValue(JsonElement element)
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

    private static Proto.Coordinate CreateCoordinate(double x, double y)
        => new() { X = x, Y = y };

    private static Models.SpatialReference ConvertSpatialReference(Proto.SpatialReference sr)
    {
        return new Models.SpatialReference
        {
            Wkid = sr.Wkid,
            LatestWkid = sr.LatestWkid,
            Wkt = sr.Wkt,
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
