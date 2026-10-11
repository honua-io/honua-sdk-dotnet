// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using Honua.Sdk.Studio.Packages;
using Honua.Sdk.Studio.Tests.Fixtures;

namespace Honua.Sdk.Studio.Tests;

/// <summary>
/// The canonical content-hash input (honua-server#5449) and the final publication URL
/// (honua-server#5788) are optional, additive response fields: they deserialize when present,
/// stay null when a server omits them, and the input recomputes the reported content hash.
/// </summary>
public sealed class StudioContentHashInputTests
{
    private const string AbcSha256 = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    private static readonly Guid DraftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    // The bytes a server hashes: its serialization of the envelope with validation.generatedAt removed.
    private static readonly byte[] CanonicalInput = Encoding.UTF8.GetBytes(
        """{"family":"map","schemaVersion":"honua_map_package.v1","bindings":[],"dependencies":[],"provenance":[],"validation":{"status":"valid","diagnostics":[]},"body":{"layers":[]}}""");

    private static readonly string CanonicalInputBase64 = Convert.ToBase64String(CanonicalInput);

    private static readonly string CanonicalHash = Convert.ToHexStringLower(SHA256.HashData(CanonicalInput));

    [Fact]
    public void Compute_IsLowerCaseHexSha256()
    {
        // FIPS 180-2 test vector for "abc".
        Assert.Equal(AbcSha256, StudioContentHash.Compute("abc"u8));
    }

    [Fact]
    public void Verify_MatchingInput_ReturnsTrue()
    {
        Assert.True(StudioContentHash.Verify("YWJj", AbcSha256));
        Assert.True(StudioContentHash.Verify(CanonicalInputBase64, CanonicalHash));
    }

    [Fact]
    public void Verify_NonMatchingInput_ReturnsFalse()
    {
        Assert.False(StudioContentHash.Verify("YWJk", AbcSha256));
        Assert.False(StudioContentHash.Verify(CanonicalInputBase64, new string('0', 64)));
    }

    [Fact]
    public void Verify_UpperCaseDigest_ReturnsFalse()
    {
        // The contract is lower-case hex; a digest in any other form does not verify.
        Assert.False(StudioContentHash.Verify("YWJj", AbcSha256.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("YWJ")]
    public void Verify_InvalidBase64_ReturnsFalse(string input)
    {
        Assert.False(StudioContentHash.Verify(input, CanonicalHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_AbsentInput_ReturnsNull(string? input)
    {
        Assert.Null(StudioContentHash.Verify(input, CanonicalHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Verify_AbsentDigest_ReturnsFalse(string? digest)
    {
        Assert.False(StudioContentHash.Verify(CanonicalInputBase64, digest));
    }

    [Fact]
    public async Task GetVersionAsync_WithContentHashInput_DeserializesAndVerifies()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(VersionEnvelope(CanonicalHash, CanonicalInputBase64));
        });
        var client = new HonuaStudioPackageClient(http);

        var version = await client.GetVersionAsync(ItemId, VersionId);

        Assert.Equal($"/api/v1/studio/content-items/{ItemId}/versions/{VersionId}", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(CanonicalInputBase64, version.ContentHashInput);
        Assert.Equal(CanonicalHash, version.ContentHash);
        Assert.True(version.VerifyContentHash());
    }

    [Fact]
    public async Task GetVersionAsync_WithoutContentHashInput_IsBackwardCompatible()
    {
        using var http = CreateHttpClient(_ => JsonResponse(VersionEnvelope(CanonicalHash, contentHashInput: null)));
        var client = new HonuaStudioPackageClient(http);

        var version = await client.GetVersionAsync(ItemId, VersionId);

        Assert.Null(version.ContentHashInput);
        Assert.Equal(CanonicalHash, version.ContentHash);
        Assert.Null(version.VerifyContentHash());
    }

    [Fact]
    public async Task GetVersionAsync_InputNotMatchingDigest_FailsVerification()
    {
        using var http = CreateHttpClient(_ => JsonResponse(VersionEnvelope(new string('a', 64), CanonicalInputBase64)));
        var client = new HonuaStudioPackageClient(http);

        var version = await client.GetVersionAsync(ItemId, VersionId);

        Assert.Equal(CanonicalInputBase64, version.ContentHashInput);
        Assert.False(version.VerifyContentHash());
    }

    [Fact]
    public async Task CreateContentVersionAsync_SaveResponseCarriesContentHashInput()
    {
        using var http = CreateHttpClient(_ => JsonResponse(VersionEnvelope(CanonicalHash, CanonicalInputBase64), HttpStatusCode.Created));
        var client = new HonuaStudioPackageClient(http);

        var version = await client.CreateContentVersionAsync(DraftId, new SaveStudioContentVersionRequest());

        Assert.Equal(CanonicalInputBase64, version.ContentHashInput);
        Assert.True(version.VerifyContentHash());
    }

    [Fact]
    public async Task ListVersionsAsync_EnumerationWithoutInput_LeavesItNull()
    {
        var json = $$"""
            {
              "success": true,
              "data": { "itemId": "{{ItemId}}", "versions": [ {{VersionJson(CanonicalHash, contentHashInput: null)}} ] },
              "timestamp": "2026-07-01T12:00:00Z"
            }
            """;
        using var http = CreateHttpClient(_ => JsonResponse(json));
        var client = new HonuaStudioPackageClient(http);

        var list = await client.ListVersionsAsync(ItemId);

        var version = Assert.Single(list.Versions);
        Assert.Null(version.ContentHashInput);
        Assert.Null(version.VerifyContentHash());
    }

    [Fact]
    public async Task CreatePublishRequestAsync_AcceptedWithPublicationUrl_Deserializes()
    {
        using var http = CreateHttpClient(_ => JsonResponse(PublicationEnvelope("accepted", "/api/v1/studio/published/maps/parcels"), HttpStatusCode.Created));
        var client = new HonuaStudioPackageClient(http);

        var publication = await client.CreatePublishRequestAsync(ItemId, VersionId, new CreateStudioPublicationRequest());

        Assert.Equal(StudioPublicationRequestStatus.Accepted, publication.Status);
        Assert.Equal("/api/v1/studio/published/maps/parcels", publication.PublicationUrl);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("rejected")]
    [InlineData("accepted")]
    public async Task CreatePublishRequestAsync_WithoutPublicationUrl_IsBackwardCompatible(string status)
    {
        using var http = CreateHttpClient(_ => JsonResponse(PublicationEnvelope(status, publicationUrl: null), HttpStatusCode.Created));
        var client = new HonuaStudioPackageClient(http);

        var publication = await client.CreatePublishRequestAsync(ItemId, VersionId, new CreateStudioPublicationRequest());

        Assert.Null(publication.PublicationUrl);
    }

    private static string VersionEnvelope(string contentHash, string? contentHashInput) => $$"""
        {
          "success": true,
          "data": {{VersionJson(contentHash, contentHashInput)}},
          "timestamp": "2026-07-01T12:00:00Z"
        }
        """;

    private static string VersionJson(string contentHash, string? contentHashInput)
    {
        var input = contentHashInput is null ? string.Empty : "\"contentHashInput\": \"" + contentHashInput + "\",";
        return $$"""
            {
              "versionId": "{{VersionId}}",
              "itemId": "{{ItemId}}",
              "packageKey": "my-map",
              "versionNumber": 1,
              "contentHash": "{{contentHash}}",
              {{input}}
              "envelope": { "family": "map", "schemaVersion": "honua_map_package.v1", "bindings": [], "dependencies": [], "provenance": [], "validation": { "status": "valid", "diagnostics": [] }, "body": { "layers": [] } },
              "validation": { "status": "valid", "diagnostics": [] },
              "dependencies": [],
              "provenance": [],
              "createdAt": "2026-07-01T12:00:00Z"
            }
            """;
    }

    private static string PublicationEnvelope(string status, string? publicationUrl)
    {
        var url = publicationUrl is null ? string.Empty : ", \"publicationUrl\": \"" + publicationUrl + "\"";
        return $$"""
            {
              "success": true,
              "data": {
                "requestId": "44444444-4444-4444-4444-444444444444",
                "itemId": "{{ItemId}}",
                "versionId": "{{VersionId}}",
                "intent": { "route": "maps/parcels" },
                "status": "{{status}}",
                "validation": { "status": "valid" },
                "createdAt": "2026-07-01T12:00:00Z"{{url}}
              },
              "timestamp": "2026-07-01T12:00:00Z"
            }
            """;
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler)) { BaseAddress = new Uri("https://server.example") };

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    // Ownership of the response passes to HttpClient, which hands it to the client under test.
    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return response;
    }
}
