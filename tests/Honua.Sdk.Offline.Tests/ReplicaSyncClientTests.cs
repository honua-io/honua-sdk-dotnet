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
    public async Task CreateReplicaAsync_Success_ReturnsReplicaIdAndServerGen()
    {
        var responseJson = """
        {
            "replicaID": "replica-abc-123",
            "serverGen": 42
        }
        """;

        Uri? capturedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            Assert.Equal(HttpMethod.Post, request.Method);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        });

        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.CreateReplicaAsync("assets", "test-replica");

        Assert.Equal("replica-abc-123", result.ReplicaId);
        Assert.Equal(42, result.ServerGen);
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
        Assert.Empty(result.LayerChanges);
        Assert.Empty(result.LayerServerGens);
        Assert.False(result.ExceededTransferLimit);
        Assert.NotNull(capturedUri);
        Assert.Contains("rest/services/assets/FeatureServer/synchronizeReplica", capturedUri!.PathAndQuery);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task SynchronizeReplicaAsync_RemoteChangeBetweenSyncs_IsAvailableBeforeCursorIsPersisted(string operation)
    {
        var localFeatures = new Dictionary<long, string> { [2] = "original" };
        var storedServerGen = 10L;
        var currentServerGen = 10L;
        var remoteChanges = "[]";
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            // Model the pinned server: synchronize delivers edits and advances its stored
            // cursor; a subsequent generation-less extract cannot recover those edits.
            var changes = storedServerGen < currentServerGen ? remoteChanges : "[]";
            var property = request.RequestUri!.AbsolutePath.EndsWith("synchronizeReplica", StringComparison.Ordinal)
                ? "edits" : "layerChanges";
            if (property == "edits")
            {
                storedServerGen = currentServerGen;
            }

            return Task.FromResult(JsonResponse($"{{\"serverGen\":{currentServerGen},\"{property}\":{changes}}}"));
        });
        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });
        var initial = await client.SynchronizeReplicaAsync("assets", "replica");
        Assert.Equal(10, initial.ServerGen);
        Assert.Empty(initial.LayerChanges);

        currentServerGen = 11;
        remoteChanges = operation switch
        {
            "add" => """[{"id":0,"addFeatures":[{"attributes":{"objectid":1,"name":"remote"}}]}]""",
            "update" => """[{"id":0,"updateFeatures":[{"attributes":{"objectid":2,"name":"remote"}}]}]""",
            _ => """[{"id":0,"deleteIds":[2]}]""",
        };
        var result = await client.SynchronizeReplicaAsync("assets", "replica");
        var laterExtract = await client.ExtractChangesAsync("assets", "replica");
        Assert.Empty(laterExtract.LayerChanges);
        Assert.Equal(11, storedServerGen);
        var layer = Assert.Single(result.LayerChanges);
        var deliveredFeatures = (layer.AddFeaturesJson ?? []).Concat(layer.UpdateFeaturesJson ?? [])
            .Select(ReadFeatureName);
        foreach (var (objectId, name) in deliveredFeatures)
        {
            localFeatures[objectId] = name;
        }

        foreach (var id in layer.DeleteIds ?? [])
        {
            localFeatures.Remove(id);
        }

        // Persisting the delivered generation is safe only after applying its payload.
        var appliedGeneration = result.ServerGen;
        Assert.Equal(11, appliedGeneration);
        if (operation == "delete")
        {
            Assert.Empty(localFeatures);
        }
        else
        {
            Assert.Equal("remote", localFeatures[operation == "add" ? 1 : 2]);
        }
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_LimitedDelivery_PreservesLimitAndLayerGenerations()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("""
                {"serverGen":11,"edits":[{"id":3,"deleteIds":[99]}],
                 "exceededTransferLimit":true,"layerServerGens":[{"id":3,"serverGen":11}]}
                """)));
        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.SynchronizeReplicaAsync("assets", "replica");

        Assert.True(result.ExceededTransferLimit);
        Assert.Equal(new ReplicaLayerServerGeneration(3, 11), Assert.Single(result.LayerServerGens));
        Assert.Equal([99L], Assert.Single(result.LayerChanges).DeleteIds);
        Assert.Equal(11, result.ServerGen);
    }

    [Theory]
    [InlineData("add", false)]
    [InlineData("update", false)]
    [InlineData("delete", false)]
    [InlineData("add", true)]
    [InlineData("update", true)]
    [InlineData("delete", true)]
    public async Task SynchronizeReplicaAsync_AfterExtract_UsesAppliedGenerationAndDeliversInterveningChanges(string operation, bool serverCursorAlreadyAdvanced)
    {
        var storedServerGen = serverCursorAlreadyAdvanced ? 11L : 9L;
        var remoteChanges = operation switch
        {
            "add" => """[{"id":0,"addFeatures":[{"attributes":{"objectid":1}}]}]""",
            "update" => """[{"id":0,"updateFeatures":[{"attributes":{"objectid":2}}]}]""",
            _ => """[{"id":0,"deleteIds":[3]}]""",
        };
        string? syncBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var response = """{"serverGen":10,"layerChanges":[]}""";
            if (request.RequestUri!.AbsolutePath.EndsWith("synchronizeReplica", StringComparison.Ordinal))
            {
                syncBody = await request.Content!.ReadAsStringAsync(cancellationToken);
                var parameters = ParseForm(syncBody);
                var since = parameters.TryGetValue("replicaServerGen", out var value)
                    ? long.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : storedServerGen;
                storedServerGen = 11;
                response = $"{{\"serverGen\":11,\"edits\":{(since < 11 ? remoteChanges : "[]")}}}";
            }

            return JsonResponse(response);
        });
        IReplicaSyncClient client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });
        var extract = await client.ExtractChangesAsync("assets", "replica", "9");
        Assert.Equal(10, extract.ServerGen);

        // A remote edit at generation 11 arrives after the extracted generation was applied.
        // An already advanced server cursor also models retrying a download not applied locally.
        var result = await client.SynchronizeReplicaAsync("assets", "replica", receivedServerGen: extract.ServerGen);

        Assert.Equal("10", ParseForm(syncBody!)["replicaServerGen"]);
        Assert.Equal("download", ParseForm(syncBody!)["syncDirection"]);
        Assert.Equal(11, result.ServerGen);
        var layer = Assert.Single(result.LayerChanges);
        if (operation == "add")
        {
            Assert.Single(layer.AddFeaturesJson!);
        }
        else if (operation == "update")
        {
            Assert.Single(layer.UpdateFeaturesJson!);
        }
        else
        {
            Assert.Equal([3L], layer.DeleteIds);
        }
    }

    [Theory]
    [InlineData("download")]
    [InlineData("upload")]
    [InlineData("bidirectional")]
    public async Task SynchronizeReplicaAsync_WithReceivedGeneration_PreservesDirectionAndGeneration(string direction)
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""{"serverGen":10,"edits":null,"layerServerGens":null,"exceededTransferLimit":null}""");
        });
        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.SynchronizeReplicaAsync("assets", "replica", 0, direction);

        Assert.Equal(direction, ParseForm(capturedBody!)["syncDirection"]);
        Assert.Equal("0", ParseForm(capturedBody!)["replicaServerGen"]);
        Assert.Empty(result.LayerChanges);
        Assert.Empty(result.LayerServerGens);
        Assert.False(result.ExceededTransferLimit);
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_NegativeReceivedGeneration_RejectsBeforeSending()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("Must not send."));
        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.SynchronizeReplicaAsync("assets", "replica", -1));
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_LimitedWindows_ContinuesFromAppliedDelivery()
    {
        var requests = new List<string>();
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var parameters = ParseForm(await request.Content!.ReadAsStringAsync(cancellationToken));
            requests.Add(parameters["replicaServerGen"]);
            var response = parameters["replicaServerGen"] == "10"
                ? """{"serverGen":11,"edits":[{"id":0,"deleteIds":[1]}],"exceededTransferLimit":true,"layerServerGens":[{"id":0,"serverGen":11}]}"""
                : """{"serverGen":12,"edits":[{"id":0,"deleteIds":[2]}],"layerServerGens":[{"id":0,"serverGen":12}]}""";
            return JsonResponse(response);
        });
        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });
        var localIds = new HashSet<long> { 1, 2 };
        var appliedGeneration = 10L;
        SynchronizeResult result;
        do
        {
            result = await client.SynchronizeReplicaAsync("assets", "replica", appliedGeneration);
            foreach (var layer in result.LayerChanges)
            {
                localIds.ExceptWith(layer.DeleteIds ?? []);
            }

            appliedGeneration = result.ServerGen;
        } while (result.ExceededTransferLimit);

        Assert.Equal(["10", "11"], requests);
        Assert.Empty(localIds);
        Assert.Equal(12, appliedGeneration);
    }

    [Fact]
    public async Task ExtractChangesAsync_LimitedDelivery_PreservesDeliveryMetadata()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse("""
                {"serverGen":11,"layerChanges":[{"id":3,"deleteIds":[99]}],
                 "exceededTransferLimit":true,"layerServerGens":[{"id":3,"serverGen":11}]}
                """)));
        var client = new ReplicaSyncClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.honua.test") });

        var result = await client.ExtractChangesAsync("assets", "replica", "10");

        Assert.True(result.ExceededTransferLimit);
        Assert.Equal(new ReplicaLayerServerGeneration(3, 11), Assert.Single(result.LayerServerGens));
        Assert.Equal([99L], Assert.Single(result.LayerChanges).DeleteIds);
    }

    [Fact]
    public async Task SynchronizeReplicaAsync_LegacyImplementation_RejectsExplicitGenerationInsteadOfIgnoringIt()
    {
        IReplicaSyncClient client = new LegacyReplicaSyncClient();

        var legacyResult = await client.SynchronizeReplicaAsync("assets", "replica");
        Assert.Equal(42, legacyResult.ServerGen);
        await Assert.ThrowsAsync<NotSupportedException>(() => client.SynchronizeReplicaAsync("assets", "replica", 10));
    }

    private sealed class LegacyReplicaSyncClient : IReplicaSyncClient
    {
        public Task<CreateReplicaResult> CreateReplicaAsync(string serviceId, string replicaName, IReadOnlyList<int>? layerIds = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExtractChangesResult> ExtractChangesAsync(string serviceId, string replicaId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SynchronizeResult> SynchronizeReplicaAsync(string serviceId, string replicaId, string syncDirection = "download", CancellationToken cancellationToken = default)
            => Task.FromResult(new SynchronizeResult(replicaId, 42));

        public Task UnRegisterReplicaAsync(string serviceId, string replicaId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static Dictionary<string, string> ParseForm(string body)
        => body.Split('&').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

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

    private static (long ObjectId, string Name) ReadFeatureName(string featureJson)
    {
        using var feature = JsonDocument.Parse(featureJson);
        var attributes = feature.RootElement.GetProperty("attributes");
        return (attributes.GetProperty("objectid").GetInt64(), attributes.GetProperty("name").GetString()!);
    }

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
