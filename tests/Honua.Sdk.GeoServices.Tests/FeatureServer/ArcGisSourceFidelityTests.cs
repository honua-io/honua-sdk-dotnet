// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Geometry;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

/// <summary>
/// Tests for the source-client fidelity/completeness gaps closed for #341: termination-guaranteed
/// object-ID-batched paging, int64/GUID/null-geometry preservation, Z/M round-tripping, coded-value
/// domain preservation, ArcGIS token/basic/bearer credential handling, and 429 Retry-After capture.
/// Fixtures are hand-written raw JSON matching the real ArcGIS REST wire format (per the acceptance
/// criteria's "distinguish true preservation from source metadata merely being recognized"), with
/// independently computed expected values, not a serialize/deserialize snapshot of SDK output.
/// </summary>
public sealed class ArcGisSourceFidelityTests
{
    [Fact]
    public async Task QueryAllFeaturesByObjectIdBatchesAsync_SourceIgnoresResultOffset_CompletesWithoutDuplicatesOrGaps()
    {
        // A source with 7 features that ignores resultOffset entirely (always returns the same first
        // page) is exactly the "supportsPagination=false" failure mode the acceptance criteria calls
        // out: naive offset paging would either loop forever or (per QueryPagesAsync's non-advancing-
        // cursor guard) silently stop after page one, losing features 4-7.
        var allIds = new long[] { 1, 2, 3, 4, 5, 6, 7 };
        var idsRequests = new List<string>();
        var batchRequests = new List<IReadOnlyList<long>>();

        IHonuaFeatureServerClient client = TestHelpers.CreateFeatureServerClient(req =>
        {
            var query = req.RequestUri!.Query;
            if (query.Contains("returnIdsOnly=true", StringComparison.Ordinal))
            {
                idsRequests.Add(query);
                return Task.FromResult(TestHelpers.CreateRawJsonResponse(
                    $$"""{ "objectIdFieldName": "OBJECTID", "objectIds": [{{string.Join(",", allIds)}}] }"""));
            }

            var decoded = Uri.UnescapeDataString(query);
            var objectIdsParam = decoded.Split('&').First(p => p.StartsWith("objectIds=", StringComparison.Ordinal))["objectIds=".Length..];
            var requestedIds = objectIdsParam.Split(',').Select(long.Parse).ToArray();
            batchRequests.Add(requestedIds);

            var features = requestedIds.Select(id => $$"""{ "attributes": { "OBJECTID": {{id}} } }""");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(
                $$"""{ "objectIdFieldName": "OBJECTID", "features": [{{string.Join(",", features)}}] }"""));
        });

        var seenIds = new List<long>();
        await foreach (var page in client.QueryAllFeaturesByObjectIdBatchesAsync(
            "parks", 0, new FeatureServerQueryParams { Where = "1=1" }, batchSize: 3))
        {
            seenIds.AddRange(page.Features!.Select(f => f.Attributes!["OBJECTID"].GetInt64()));
        }

        Assert.Single(idsRequests);
        Assert.Equal(3, batchRequests.Count);
        Assert.Equal([1L, 2L, 3L], batchRequests[0]);
        Assert.Equal([4L, 5L, 6L], batchRequests[1]);
        Assert.Equal([7L], batchRequests[2]);

        // Independently computed: the full id set is [1..7]; every id must appear exactly once.
        Assert.Equal(allIds.OrderBy(x => x), seenIds.OrderBy(x => x));
        Assert.Equal(allIds.Length, seenIds.Distinct().Count());
    }

    [Fact]
    public async Task QueryAllFeaturesByObjectIdBatchesAsync_EmptySource_YieldsNoBatches()
    {
        IHonuaFeatureServerClient client = TestHelpers.CreateFeatureServerClient(req =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(
                """{ "objectIdFieldName": "OBJECTID", "objectIds": [] }""")));

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryAllFeaturesByObjectIdBatchesAsync("parks", 0, new FeatureServerQueryParams()))
        {
            pages.Add(page);
        }

        Assert.Empty(pages);
    }

    [Fact]
    public void Int64ObjectId_NearLongMaxValue_SurvivesWithoutDoublePrecisionLoss()
    {
        // Independently computed: long.MaxValue - 615 = 9223372036854775192. A double round-trip of
        // this value would drift (doubles only carry ~15-17 significant decimal digits), so this
        // proves the SDK reads the attribute as a JSON integer literal, not via double coercion.
        const long expected = 9223372036854775192L;
        var json = $$"""{ "attributes": { "OBJECTID": {{expected}} } }""";

        var feature = JsonSerializer.Deserialize<FeatureServerFeature>(json, GeoServicesTestJsonOptions.CamelCase)!;

        Assert.Equal(expected, feature.Attributes!["OBJECTID"].GetInt64());
    }

    [Fact]
    public void NullGeometry_IsDistinguishedFromEmptyGeometryObject()
    {
        var nullGeometryFeature = JsonSerializer.Deserialize<FeatureServerFeature>(
            """{ "attributes": { "OBJECTID": 1 }, "geometry": null }""",
            GeoServicesTestJsonOptions.CamelCase)!;
        var emptyGeometryFeature = JsonSerializer.Deserialize<FeatureServerFeature>(
            """{ "attributes": { "OBJECTID": 2 }, "geometry": {} }""",
            GeoServicesTestJsonOptions.CamelCase)!;
        var missingGeometryFeature = JsonSerializer.Deserialize<FeatureServerFeature>(
            """{ "attributes": { "OBJECTID": 3 } }""",
            GeoServicesTestJsonOptions.CamelCase)!;

        Assert.False(nullGeometryFeature.Geometry.HasValue);
        Assert.False(missingGeometryFeature.Geometry.HasValue);

        Assert.True(emptyGeometryFeature.Geometry.HasValue);
        Assert.Equal(JsonValueKind.Object, emptyGeometryFeature.Geometry!.Value.ValueKind);
        Assert.False(emptyGeometryFeature.Geometry.Value.EnumerateObject().Any());
    }

    [Fact]
    public void GuidField_CasingAndBracesArePreservedVerbatim()
    {
        // Esri GlobalIDs are canonically upper-case, brace-wrapped GUIDs; a lossy consumer that
        // normalizes casing or strips braces breaks joins against the source system of record.
        const string expected = "{6F9619FF-8B86-D011-B42D-00C04FC964FF}";
        var json = $$"""{ "attributes": { "GlobalID": "{{expected}}" } }""";

        var feature = JsonSerializer.Deserialize<FeatureServerFeature>(json, GeoServicesTestJsonOptions.CamelCase)!;

        Assert.Equal(expected, feature.Attributes!["GlobalID"].GetString());
    }

    [Fact]
    public void PointGeometry_WithZAndM_RoundTripsExactOrdinates()
    {
        // Independently computed ordinates: z=125.5 (an arbitrary elevation), m=42.0 (an arbitrary
        // route measure). Neither is derivable from x/y, so a lossy converter that drops Z/M would
        // fail this assertion rather than merely producing a different-but-plausible value.
        var geometryJson = """{ "x": -122.4194, "y": 37.7749, "z": 125.5, "m": 42.0, "hasZ": true, "hasM": true }""";
        using var doc = JsonDocument.Parse(geometryJson);

        var geometry = GeoServicesGeometryConverter.ReadGeometry(doc.RootElement);

        var point = Assert.IsType<Point>(geometry);
        Assert.Equal(-122.4194, point.X);
        Assert.Equal(37.7749, point.Y);
        Assert.Equal(125.5, point.Z);
        Assert.Equal(42.0, point.M);
    }

    [Fact]
    public void CodedValueDomain_PreservesEveryCodeNamePairLosslessly()
    {
        var fieldJson = """
            {
              "name": "STATUS",
              "type": "esriFieldTypeSmallInteger",
              "nullable": true,
              "domain": {
                "type": "codedValue",
                "name": "StatusDomain",
                "codedValues": [
                  { "code": 0, "name": "Active" },
                  { "code": 1, "name": "Inactive" },
                  { "code": 2, "name": "Under Review" }
                ]
              }
            }
            """;

        var field = JsonSerializer.Deserialize<FeatureServerField>(fieldJson, GeoServicesTestJsonOptions.CamelCase)!;

        Assert.True(field.Domain.HasValue);
        var codedValues = field.Domain!.Value.GetProperty("codedValues");
        Assert.Equal(3, codedValues.GetArrayLength());
        Assert.Equal("Active", codedValues[0].GetProperty("name").GetString());
        Assert.Equal(0, codedValues[0].GetProperty("code").GetInt32());
        Assert.Equal("Under Review", codedValues[2].GetProperty("name").GetString());
    }

    [Fact]
    public async Task ArcGisSourceCredentialHandler_TokenMode_AppendsTokenQueryParameter()
    {
        HttpRequestMessage? captured = null;
        var inner = new CapturingHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Token, Token = "abc123" };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://source.example.com") };

        await http.GetAsync("/arcgis/rest/services/Parcels/FeatureServer/0?f=json");

        Assert.Contains("token=abc123", captured!.RequestUri!.Query);
    }

    [Fact]
    public async Task ArcGisSourceCredentialHandler_TokenProvider_TakesPrecedenceAndIsInvokedPerRequest()
    {
        HttpRequestMessage? captured = null;
        var inner = new CapturingHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var callCount = 0;
        var credential = new ArcGisSourceCredential
        {
            Mode = ArcGisCredentialMode.Token,
            Token = "static-should-not-be-used",
            TokenProvider = _ => { callCount++; return Task.FromResult<string?>("dynamic-token"); }
        };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://source.example.com") };

        await http.GetAsync("/arcgis/rest/services/Parcels/FeatureServer/0?f=json");

        Assert.Contains("token=dynamic-token", captured!.RequestUri!.Query);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ArcGisSourceCredentialHandler_BasicMode_SetsExpectedAuthorizationHeader()
    {
        HttpRequestMessage? captured = null;
        var inner = new CapturingHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var credential = new ArcGisSourceCredential
        {
            Mode = ArcGisCredentialMode.Basic,
            Username = "gis-user",
            Password = "hunter2"
        };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://source.example.com") };

        await http.GetAsync("/arcgis/rest/services/Parcels/FeatureServer/0?f=json");

        // Independently computed: base64("gis-user:hunter2").
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("gis-user:hunter2"));
        Assert.Equal("Basic", captured!.Headers.Authorization!.Scheme);
        Assert.Equal(expected, captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task ArcGisSourceCredentialHandler_BearerMode_SetsBearerHeader()
    {
        HttpRequestMessage? captured = null;
        var inner = new CapturingHandler(req =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        });
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.Bearer, BearerToken = "xyz789" };
        using var handler = new ArcGisSourceCredentialHandler(credential, inner);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://source.example.com") };

        await http.GetAsync("/arcgis/rest/services/Parcels/FeatureServer/0?f=json");

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "xyz789"), captured!.Headers.Authorization);
    }

    [Fact]
    public async Task ArcGisSourceCredentialHandler_DefaultInnerHandler_IsDisposedWithHandler()
    {
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.None };
        HttpClientHandler inner;
        using (var handler = new ArcGisSourceCredentialHandler(credential))
        {
            inner = Assert.IsType<HttpClientHandler>(handler.InnerHandler);
        }

        // A disposed HttpClientHandler rejects sends; a live one would attempt the request.
        using var invoker = new HttpMessageInvoker(inner, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://source.example.com/");
        await Assert.ThrowsAsync<ObjectDisposedException>(() => invoker.SendAsync(request, CancellationToken.None));
    }

    [Fact]
    public void ArcGisSourceCredentialHandler_SuppliedInnerHandler_IsDisposedExactlyOnce()
    {
        var credential = new ArcGisSourceCredential { Mode = ArcGisCredentialMode.None };
        var inner = new DisposalTrackingHandler();
        var handler = new ArcGisSourceCredentialHandler(credential, inner);

        Assert.Equal(0, inner.DisposeCount);
        handler.Dispose();
        handler.Dispose();

        Assert.Equal(1, inner.DisposeCount);
    }

    [Fact]
    public async Task ErrorEnvelope_On200Response_CapturesRetryAfter()
    {
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            var response = TestHelpers.CreateGeoServicesErrorResponse(429, "Too many requests");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(15));
            return Task.FromResult(response);
        });

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("parks", 0, new FeatureServerQueryParams { Where = "1=1" }));

        Assert.Equal(429, ex.GeoServicesErrorCode);
        Assert.Equal(TimeSpan.FromSeconds(15), ex.RetryAfter);
    }

    [Fact]
    public async Task ErrorResponse_NonSuccessStatus_CapturesRetryAfter()
    {
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            var response = TestHelpers.CreateErrorResponse(HttpStatusCode.ServiceUnavailable, "Service temporarily unavailable");
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        });

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("parks", 0, new FeatureServerQueryParams { Where = "1=1" }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }

    private sealed class DisposalTrackingHandler : HttpMessageHandler
    {
        public int DisposeCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }

            base.Dispose(disposing);
        }
    }
}

internal static class GeoServicesTestJsonOptions
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
