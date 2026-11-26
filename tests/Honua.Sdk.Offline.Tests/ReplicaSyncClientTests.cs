// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Offline;
using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Offline.Tests;

public sealed class ReplicaSyncClientTests
{
    [Fact]
    public async Task CreateReplicaAsync_RequestsPerReplicaAndReturnsScalarServerGen()
    {
        var responseJson = """
        {
            "replicaID": "replica-abc-123",
            "syncModel": "perReplica",
            "serverGen": 4294967296
        }
        """;

        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var parameters = ParseForm(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("perReplica", parameters["syncModel"]);
            capturedUri = request.RequestUri;
            Assert.Equal(HttpMethod.Post, request.Method);
            return JsonResponse(responseJson);
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.CreateReplicaAsync("assets", "test-replica");

        Assert.Equal("replica-abc-123", result.ReplicaId);
        Assert.Equal(4294967296L, result.ServerGen);
        Assert.NotNull(capturedUri);
        Assert.Contains("rest/services/assets/FeatureServer/createReplica", capturedUri!.PathAndQuery);
    }

    [Fact]
    public async Task CreateReplicaAsync_WithLayerIds_SendsLayersParameter()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"replicaID":"r1","serverGen":1}""", Encoding.UTF8, "application/json"),
            };
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.CreateReplicaAsync("assets", "test-replica", [0, 3, 5]);

        Assert.NotNull(capturedBody);
        Assert.Contains("layers=0%2C3%2C5", capturedBody);
    }

    [Fact]
    public async Task ExtractChangesAsync_Success_ParsesAddsUpdatesDeletes()
    {
        var responseJson = """
        {
            "serverGen": 55,
            "layerChanges": [
                {
                    "id": 0,
                    "addFeatures": [
                        { "attributes": { "objectid": 1, "name": "New Feature" } }
                    ],
                    "updateFeatures": [
                        { "attributes": { "objectid": 2, "name": "Updated Feature" } }
                    ],
                    "deleteIds": [3, 4]
                }
            ]
        }
        """;

        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.ExtractChangesAsync("assets", "replica-abc-123");

        Assert.Equal(55, result.ServerGen);
        var layerChange = Assert.Single(result.LayerChanges);
        Assert.Equal(0, layerChange.LayerId);
        Assert.NotNull(layerChange.AddFeaturesJson);
        Assert.Single(layerChange.AddFeaturesJson);
        Assert.NotNull(layerChange.UpdateFeaturesJson);
        Assert.Single(layerChange.UpdateFeaturesJson);
        Assert.NotNull(layerChange.DeleteIds);
        Assert.Equal([3L, 4L], layerChange.DeleteIds);
        Assert.NotNull(capturedUri);
        Assert.Contains("rest/services/assets/FeatureServer/extractChanges", capturedUri!.PathAndQuery);
    }

    [Fact]
    public async Task ExtractChangesAsync_WithServerGen_SendsServerGenParameters()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"serverGen":70,"layerChanges":[]}""", Encoding.UTF8, "application/json"),
            };
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.ExtractChangesAsync("assets", "replica-abc-123", "55");

        Assert.NotNull(capturedBody);
        // serverGens is URL-encoded ([55] -> %5B55%5D) and replicaServerGen carries the same value.
        Assert.Contains("serverGens=%5B55%5D", capturedBody);
        Assert.Contains("replicaServerGen=55", capturedBody);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExtractChangesAsync_WithoutServerGen_OmitsServerGenParameters(string? serverGen)
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"serverGen":70,"layerChanges":[]}""", Encoding.UTF8, "application/json"),
            };
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.ExtractChangesAsync("assets", "replica-abc-123", serverGen);

        Assert.NotNull(capturedBody);
        Assert.DoesNotContain("serverGens", capturedBody);
        Assert.DoesNotContain("replicaServerGen", capturedBody);
    }

    [Fact]
    public async Task ExtractChangesAsync_EmptyChanges_ReturnsEmptyLayerChanges()
    {
        var responseJson = """
        {
            "serverGen": 60,
            "layerChanges": []
        }
        """;

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.ExtractChangesAsync("assets", "replica-abc-123");

        Assert.Equal(60, result.ServerGen);
        Assert.Empty(result.LayerChanges);
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_Success_ReturnsSyncResult()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"serverGen":100}""", Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.SynchronizeReplicaAsync("assets", "replica-abc-123");

        Assert.Equal("replica-abc-123", result.ReplicaId);
        Assert.Equal(100, result.ServerGen);
        Assert.NotNull(capturedUri);
        Assert.Contains("rest/services/assets/FeatureServer/synchronizeReplica", capturedUri!.PathAndQuery);
    }

    [Fact]
    public async Task UnRegisterReplicaAsync_Success_CompletesWithoutError()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true}""", Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.UnRegisterReplicaAsync("assets", "replica-abc-123");

        Assert.NotNull(capturedUri);
        Assert.Contains("rest/services/assets/FeatureServer/unRegisterReplica", capturedUri!.PathAndQuery);
    }

    [Fact]
    public async Task CreateReplicaAsync_NonSuccessStatusCode_ThrowsReplicaSyncException()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var ex = await Assert.ThrowsAsync<ReplicaSyncException>(() => client.CreateReplicaAsync("assets", "test-replica"));
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task ExtractChangesAsync_NonSuccessStatusCode_ThrowsReplicaSyncException()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await Assert.ThrowsAsync<ReplicaSyncException>(() => client.ExtractChangesAsync("assets", "replica-abc-123"));
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_NonSuccessStatusCode_ThrowsReplicaSyncException()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await Assert.ThrowsAsync<ReplicaSyncException>(() => client.SynchronizeReplicaAsync("assets", "replica-abc-123"));
    }

    [Fact]
    public async Task UnRegisterReplicaAsync_NonSuccessStatusCode_ThrowsReplicaSyncException()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await Assert.ThrowsAsync<ReplicaSyncException>(() => client.UnRegisterReplicaAsync("assets", "replica-abc-123"));
    }

    [Fact]
    public async Task CreateReplicaAsync_ServerReturnsError_ThrowsReplicaSyncException()
    {
        var responseJson = """
        {
            "error": {
                "code": 400,
                "message": "Invalid replica name"
            }
        }
        """;

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var ex = await Assert.ThrowsAsync<ReplicaSyncException>(() => client.CreateReplicaAsync("assets", "bad-name"));
        Assert.Contains("Invalid replica name", ex.Message);
        Assert.Equal(400, ex.ServerErrorCode);
    }

    [Fact]
    public async Task ExtractChangesAsync_ServerReturnsError_ThrowsReplicaSyncException()
    {
        var responseJson = """
        {
            "error": {
                "code": 500,
                "message": "Replica not found"
            }
        }
        """;

        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        }));

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var ex = await Assert.ThrowsAsync<ReplicaSyncException>(() => client.ExtractChangesAsync("assets", "bad-replica"));
        Assert.Contains("Replica not found", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateReplicaAsync_InvalidServiceId_ThrowsArgumentException(string? serviceId)
    {
        var client = new ReplicaSyncClient(new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.honua.test"),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateReplicaAsync(serviceId!, "r"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\t")]
    public async Task ExtractChangesAsync_InvalidServiceId_ThrowsArgumentException(string? serviceId)
    {
        var client = new ReplicaSyncClient(new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.honua.test"),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => client.ExtractChangesAsync(serviceId!, "replica"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SynchronizeReplicaAsync_InvalidServiceId_ThrowsArgumentException(string? serviceId)
    {
        var client = new ReplicaSyncClient(new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.honua.test"),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => client.SynchronizeReplicaAsync(serviceId!, "replica"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n")]
    public async Task UnRegisterReplicaAsync_InvalidServiceId_ThrowsArgumentException(string? serviceId)
    {
        var client = new ReplicaSyncClient(new HttpClient(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
        {
            BaseAddress = new Uri("https://api.honua.test"),
        });

        await Assert.ThrowsAsync<ArgumentException>(() => client.UnRegisterReplicaAsync(serviceId!, "replica"));
    }

    [Fact]
    public async Task CreateReplicaAsync_ServiceIdWithSlashAndDots_IsUrlEscaped()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"replicaID":"r","serverGen":1}""", Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.CreateReplicaAsync("../etc/passwd", "test-replica");

        Assert.NotNull(capturedUri);
        var pathAndQuery = capturedUri!.PathAndQuery;
        // The path-traversal sequence must not appear verbatim in the URL.
        Assert.DoesNotContain("../etc/passwd", pathAndQuery);
        // The escaped form ('/' -> %2F, '.' is unreserved and left as-is) must appear.
        Assert.Contains("..%2Fetc%2Fpasswd", pathAndQuery);
    }

    [Fact]
    public async Task ExtractChangesAsync_ServiceIdWithSlash_IsUrlEscaped()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"serverGen":1,"layerChanges":[]}""", Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.ExtractChangesAsync("foo/bar", "replica");

        Assert.NotNull(capturedUri);
        Assert.Contains("foo%2Fbar", capturedUri!.PathAndQuery);
        Assert.DoesNotContain("rest/services/foo/bar/FeatureServer", capturedUri.PathAndQuery);
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_ServiceIdWithSpecialChars_IsUrlEscaped()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"serverGen":1}""", Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.SynchronizeReplicaAsync("a b&c", "replica");

        Assert.NotNull(capturedUri);
        Assert.Contains("a%20b%26c", capturedUri!.PathAndQuery);
    }

    [Fact]
    public async Task UnRegisterReplicaAsync_ServiceIdWithSlash_IsUrlEscaped()
    {
        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"success":true}""", Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await client.UnRegisterReplicaAsync("svc/inject", "replica");

        Assert.NotNull(capturedUri);
        Assert.Contains("svc%2Finject", capturedUri!.PathAndQuery);
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ReplicaSyncClient(null!));
    }

    [Fact]
    public async Task UploadReplicaAsync_SendsLayerEditsAndReadsAssignedIdsAndConflicts()
    {
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal("/rest/services/assets/FeatureServer/synchronizeReplica", request.RequestUri!.AbsolutePath);
            var parameters = ParseForm(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal("r1", parameters["replicaID"]);
            Assert.Equal("upload", parameters["syncDirection"]);
            Assert.Equal("4294967296", parameters["replicaServerGen"]);
            Assert.Equal("true", parameters["returnIdsForAdds"]);
            Assert.Equal("manualReview", parameters["conflictHandling"]);
            Assert.Equal("batch-r1-1", parameters["editsUploadID"]);
            using var edits = JsonDocument.Parse(parameters["edits"]);
            var layer = Assert.Single(edits.RootElement.EnumerateArray());
            Assert.Equal(2, layer.GetProperty("id").GetInt32());
            Assert.Equal("new", Assert.Single(layer.GetProperty("adds").EnumerateArray())
                .GetProperty("attributes").GetProperty("name").GetString());
            Assert.Equal(9, Assert.Single(layer.GetProperty("updates").EnumerateArray())
                .GetProperty("attributes").GetProperty("objectid").GetInt64());
            Assert.Equal(4294967297L, Assert.Single(layer.GetProperty("deletes").EnumerateArray()).GetInt64());
            return JsonResponse("""
                {
                  "success":true,"replicaID":"r1","syncDirection":"upload","serverGen":4294967296,
                  "appliedAdds":1,"appliedUpdates":0,"appliedDeletes":1,
                  "editResults":[{"id":2,"addResults":[{"objectId":4294967300,"globalId":"g1","success":true}],
                    "updateResults":null,"deleteResults":null}],
                  "conflicts":[{"layerId":2,"objectId":9,"conflictType":1,"applied":false,"editIndex":1,"conflictId":"c1"}]
                }
                """);
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") };
        IReplicaUploadClient client = new ReplicaSyncClient(http);

        var result = await client.UploadReplicaAsync("assets", "r1", new ReplicaUploadRequest(4294967296L,
            [new LayerChangeSet
            {
                LayerId = 2,
                AddFeaturesJson = ["""{"attributes":{"name":"new"}}"""],
                UpdateFeaturesJson = ["""{"attributes":{"objectid":9}}"""],
                DeleteIds = [4294967297L],
            }])
        {
            ManualReview = true,
            UploadId = "batch-r1-1",
        });

        Assert.True(result.Success);
        Assert.Equal(4294967296L, result.ServerGen);
        Assert.Equal(1, result.AppliedAdds);
        Assert.Equal(0, result.AppliedUpdates);
        Assert.Equal(1, result.AppliedDeletes);
        var layerResult = Assert.Single(result.EditResults);
        Assert.Equal(2, layerResult.LayerId);
        var added = Assert.Single(layerResult.AddResults);
        Assert.True(added.Succeeded);
        Assert.Equal(4294967300L, added.ObjectId);
        Assert.Equal("g1", added.Id);
        Assert.Empty(layerResult.UpdateResults);
        Assert.Empty(layerResult.DeleteResults);
        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal(2, conflict.LayerId);
        Assert.Equal(9, conflict.ObjectId);
        Assert.Equal(1, conflict.ConflictType);
        Assert.Equal(1, conflict.EditIndex);
        Assert.False(conflict.Applied);
        Assert.Equal("c1", conflict.ConflictId);
    }

    [Fact]
    public async Task UploadReplicaAsync_ReportsRecognizedNonSuccessEnvelope()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":400,"message":"Uploaded replica edits failed to apply.","details":[]}}""",
                Encoding.UTF8, "application/json"),
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") };
        var client = new ReplicaSyncClient(http);

        var error = await Assert.ThrowsAsync<ReplicaSyncException>(
            () => client.UploadReplicaAsync("assets", "r1", new ReplicaUploadRequest(0, [])));

        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        Assert.Equal(400, error.ServerErrorCode);
        Assert.Contains("Uploaded replica edits failed to apply.", error.Message);
    }

    private static Dictionary<string, string> ParseForm(string body)
        => body.Split('&').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

    // The returned message is owned and disposed by the HttpClient pipeline.
    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
