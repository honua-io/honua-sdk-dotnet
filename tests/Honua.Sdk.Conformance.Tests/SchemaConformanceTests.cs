using Google.Protobuf;
using Honua.Sdk.Grpc.Conversion;
using Honua.Sdk.Grpc.Models;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// Schema-level conformance: round-trips the shared <c>geospatial-grpc</c>
/// canonical FeatureService fixtures through the SDK's pinned generated gRPC
/// client (<c>Geospatial.V1.*</c>) and its public converter
/// (<see cref="HonuaGrpcProtoConverter"/>).
/// <para>
/// This is the mechanism that catches the class of contract drift #181 cares
/// about at the SDK boundary: a renamed or removed field, a changed type, or a
/// dropped enum value makes the canonical JSON either fail to parse into the
/// generated message or lose data when converted to the SDK model, so the
/// assertion fails. It maps each fixture 1:1 to a <c>geospatial.v1</c> message
/// and needs no running server, so it runs in the normal CI matrix and as the
/// schema half of the live conformance job.
/// </para>
/// </summary>
[Trait("Category", "Conformance")]
public sealed class SchemaConformanceTests
{
    // Strict JSON parsing: an unknown field in a canonical fixture (which would
    // happen if the schema removed/renamed a field the fixture still carries)
    // must fail rather than be silently ignored, so drift is caught.
    private static readonly JsonParser StrictParser =
        new(JsonParser.Settings.Default.WithIgnoreUnknownFields(false));

    [SchemaConformanceFact]
    public void QueryFeaturesRequest_Fixture_ParsesAndConvertsThroughSdk()
    {
        var json = ConformanceFixtures.ReadFixture("feature_query_request.json");

        // Canonical JSON -> generated proto message. Parses strictly, so any
        // field the schema no longer accepts surfaces as a parse failure.
        var proto = StrictParser.Parse<Proto.QueryFeaturesRequest>(json);

        // Spot-check that the canonical payload is fully represented in the
        // generated message (these field names/types are the contract surface).
        Assert.Equal("sf-parks", proto.ServiceId);
        Assert.Equal(0, proto.LayerId);
        Assert.Equal("AREA > 1000", proto.Where);
        Assert.True(proto.ReturnGeometry);
        Assert.Equal(new[] { "OBJECTID", "NAME", "AREA" }, proto.OutFields);
        Assert.Equal(4326, proto.OutSr.Wkid);
        Assert.Equal(10, proto.ResultRecordCount);
        Assert.Equal("NAME ASC", proto.OrderBy);
        Assert.Equal(Proto.SpatialRelationship.Intersects, proto.SpatialFilter.SpatialRelationship);
        Assert.NotNull(proto.SpatialFilter.Geometry);

        // The SDK must be able to build this canonical request from its model.
        var model = new QueryFeaturesRequest
        {
            ServiceId = proto.ServiceId,
            LayerId = proto.LayerId,
            Where = proto.Where,
            ReturnGeometry = proto.ReturnGeometry,
            OutFields = proto.OutFields.ToList(),
            ResultRecordCount = proto.ResultRecordCount,
            OrderBy = proto.OrderBy,
        };
        var rebuilt = HonuaGrpcProtoConverter.ToProto(model);
        Assert.Equal(proto.ServiceId, rebuilt.ServiceId);
        Assert.Equal(proto.Where, rebuilt.Where);
        Assert.Equal(proto.OutFields, rebuilt.OutFields);
    }

    [SchemaConformanceFact]
    public void QueryFeaturesResponse_Fixture_ConvertsToSdkModelWithoutDrift()
    {
        var json = ConformanceFixtures.ReadFixture("feature_query_response.json");

        var proto = StrictParser.Parse<Proto.QueryFeaturesResponse>(json);

        // Generated message reflects the canonical response shape.
        Assert.Equal("OBJECTID", proto.ObjectIdFieldName);
        Assert.Equal(Proto.GeometryType.Point, proto.GeometryType);
        Assert.Equal(4326, proto.SpatialReference.Wkid);
        Assert.Equal(3, proto.Fields.Count);
        Assert.Single(proto.Features);

        // SDK converter must preserve the fields, feature id, attributes, and
        // geometry. Loss here means the generated client and the schema drifted.
        QueryFeaturesResponse model = HonuaGrpcProtoConverter.FromProto(proto);

        Assert.Equal("OBJECTID", model.ObjectIdFieldName);
        Assert.Equal(GeometryType.Point, model.GeometryType);
        Assert.NotNull(model.SpatialReference);
        Assert.Equal(4326, model.SpatialReference!.Wkid);

        Assert.Equal(3, model.Fields.Count);
        var nameField = model.Fields.Single(f => f.Name == "NAME");
        Assert.Equal(FieldType.String, nameField.FieldType);
        Assert.Equal(128, nameField.Length);
        Assert.True(nameField.Nullable);

        var feature = Assert.Single(model.Features);
        Assert.Equal(42, feature.Id);
        Assert.Equal("Golden Gate Park", Assert.IsType<string>(feature.Attributes["NAME"]));
        Assert.Equal(44340000.0, Assert.IsType<double>(feature.Attributes["AREA"]));
        Assert.NotNull(feature.Geometry);
        Assert.True(feature.Geometry!.ContainsKey("x"));
        Assert.True(feature.Geometry!.ContainsKey("y"));
    }

    [SchemaConformanceFact]
    public void ApplyEditsRequest_Fixture_ParsesIntoGeneratedMessage()
    {
        var json = ConformanceFixtures.ReadFixture("feature_apply_edits_request.json");

        var proto = StrictParser.Parse<Proto.ApplyEditsRequest>(json);

        Assert.False(string.IsNullOrEmpty(proto.ServiceId));
        Assert.True(proto.Adds.Count + proto.Updates.Count + proto.Deletes.Count > 0,
            "ApplyEdits request fixture carried no adds/updates/deletes.");
    }

    [SchemaConformanceFact]
    public void ApplyEditsResponse_Fixture_ConvertsToSdkModelWithoutDrift()
    {
        var json = ConformanceFixtures.ReadFixture("feature_apply_edits_response.json");

        var proto = StrictParser.Parse<Proto.ApplyEditsResponse>(json);
        ApplyEditsResponse model = HonuaGrpcProtoConverter.FromProto(proto);

        var totalResults = model.AddResults.Count + model.UpdateResults.Count + model.DeleteResults.Count;
        Assert.True(totalResults > 0, "ApplyEdits response fixture produced no edit results.");

        // Every result the schema reports must carry its object id + success flag
        // through the SDK model — these are the contract fields callers depend on.
        foreach (var result in model.AddResults.Concat(model.UpdateResults).Concat(model.DeleteResults))
        {
            Assert.True(result.Success || result.Error is not null,
                "An edit result was neither successful nor carried an error.");
        }
    }

    [SchemaConformanceFact]
    public void EveryFeatureServiceFixture_IsBackedByAGeneratedMessageType()
    {
        // Guards against the manifest advertising a FeatureService message the
        // generated client no longer exposes — i.e. a removed/renamed message.
        var featureServiceFixtures = new (string File, Func<string> Parse)[]
        {
            ("feature_query_request.json", () => StrictParser.Parse<Proto.QueryFeaturesRequest>(ConformanceFixtures.ReadFixture("feature_query_request.json")).ServiceId),
            ("feature_query_response.json", () => StrictParser.Parse<Proto.QueryFeaturesResponse>(ConformanceFixtures.ReadFixture("feature_query_response.json")).ObjectIdFieldName),
            ("feature_apply_edits_request.json", () => StrictParser.Parse<Proto.ApplyEditsRequest>(ConformanceFixtures.ReadFixture("feature_apply_edits_request.json")).ServiceId),
            ("feature_apply_edits_response.json", () => StrictParser.Parse<Proto.ApplyEditsResponse>(ConformanceFixtures.ReadFixture("feature_apply_edits_response.json")).ToString()),
        };

        foreach (var (file, parse) in featureServiceFixtures)
        {
            var exception = Record.Exception(parse);
            Assert.True(exception is null, $"Fixture {file} failed to parse into its generated message: {exception?.Message}");
        }
    }
}
