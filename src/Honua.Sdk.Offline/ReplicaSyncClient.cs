// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Offline;

/// <summary>
/// HTTP-based <see cref="IReplicaSyncClient"/> implementation targeting the GeoServices
/// FeatureServer replica sync endpoints (<c>createReplica</c>, <c>extractChanges</c>,
/// <c>synchronizeReplica</c>, <c>unRegisterReplica</c>).
/// </summary>
public sealed class ReplicaSyncClient : IReplicaSyncClient
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplicaSyncClient"/> class.
    /// </summary>
    /// <param name="httpClient">An <see cref="HttpClient"/> configured with the base address of the feature server.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    public ReplicaSyncClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<CreateReplicaResult> CreateReplicaAsync(
        string serviceId,
        string replicaName,
        IReadOnlyList<int>? layerIds = null,
        CancellationToken cancellationToken = default)
    {
        ValidateServiceId(serviceId);
        var url = $"rest/services/{Uri.EscapeDataString(serviceId)}/FeatureServer/createReplica";
        var parameters = new Dictionary<string, string>
        {
            ["replicaName"] = replicaName,
            ["syncModel"] = "perLayer",
            ["f"] = "json",
        };

        if (layerIds is { Count: > 0 })
        {
            parameters["layers"] = string.Join(
                ',',
                layerIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        }

        using var doc = await PostAsync(url, parameters, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        var replicaId = root.GetProperty("replicaID").GetString()
            ?? throw new ReplicaSyncException("Server did not return a replicaID.");
        var serverGen = root.GetProperty("serverGen").GetInt64();

        return new CreateReplicaResult(replicaId, serverGen);
    }

    /// <inheritdoc />
    public Task<ExtractChangesResult> ExtractChangesAsync(
        string serviceId,
        string replicaId,
        CancellationToken cancellationToken = default)
        => ExtractChangesAsync(serviceId, replicaId, sinceServerGen: null, cancellationToken);

    /// <inheritdoc />
    public async Task<ExtractChangesResult> ExtractChangesAsync(
        string serviceId,
        string replicaId,
        string? sinceServerGen,
        CancellationToken cancellationToken = default)
    {
        ValidateServiceId(serviceId);
        var url = $"rest/services/{Uri.EscapeDataString(serviceId)}/FeatureServer/extractChanges";
        var parameters = new Dictionary<string, string>
        {
            ["replicaID"] = replicaId,
            ["f"] = "json",
        };

        // Scope the extract to changes since the supplied server generation. The GeoServices
        // replica protocol expects an array of per-layer generations; a single value is broadcast
        // to all layers by the server. Without it the server returns the full change set every
        // run, defeating delta sync.
        if (!string.IsNullOrWhiteSpace(sinceServerGen))
        {
            parameters["serverGens"] = $"[{sinceServerGen}]";
            parameters["replicaServerGen"] = sinceServerGen;
        }

        using var doc = await PostAsync(url, parameters, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        var serverGen = root.GetProperty("serverGen").GetInt64();
        var layerChanges = new List<LayerChangeSet>();

        if (root.TryGetProperty("layerChanges", out var layerChangesElement)
            && layerChangesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var layerElement in layerChangesElement.EnumerateArray())
            {
                layerChanges.Add(ParseLayerChangeSet(layerElement));
            }
        }

        return new ExtractChangesResult
        {
            LayerChanges = layerChanges,
            ServerGen = serverGen,
        };
    }

    /// <inheritdoc />
    public async Task<SynchronizeResult> SynchronizeReplicaAsync(
        string serviceId,
        string replicaId,
        string syncDirection = "download",
        CancellationToken cancellationToken = default)
    {
        ValidateServiceId(serviceId);
        var url = $"rest/services/{Uri.EscapeDataString(serviceId)}/FeatureServer/synchronizeReplica";
        var parameters = new Dictionary<string, string>
        {
            ["replicaID"] = replicaId,
            ["syncDirection"] = syncDirection,
            ["f"] = "json",
        };

        using var doc = await PostAsync(url, parameters, cancellationToken).ConfigureAwait(false);
        var serverGen = doc.RootElement.GetProperty("serverGen").GetInt64();

        return new SynchronizeResult(replicaId, serverGen);
    }

    /// <inheritdoc />
    public async Task UnRegisterReplicaAsync(
        string serviceId,
        string replicaId,
        CancellationToken cancellationToken = default)
    {
        ValidateServiceId(serviceId);
        var url = $"rest/services/{Uri.EscapeDataString(serviceId)}/FeatureServer/unRegisterReplica";
        var parameters = new Dictionary<string, string>
        {
            ["replicaID"] = replicaId,
            ["f"] = "json",
        };

        using var doc = await PostAsync(url, parameters, cancellationToken).ConfigureAwait(false);
        _ = doc;
    }

    private async Task<JsonDocument> PostAsync(
        string url,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(parameters);
        HttpResponseMessage? response = null;
        try
        {
            var requestUri = new Uri(url, UriKind.Relative);
            response = await _httpClient.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new ReplicaSyncException(
                    $"Replica sync HTTP request to '{url}' failed with status {(int)response.StatusCode} ({response.ReasonPhrase}).",
                    response.StatusCode,
                    serverErrorCode: null);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            ThrowIfError(doc.RootElement, response.StatusCode);
            return doc;
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static LayerChangeSet ParseLayerChangeSet(JsonElement element)
    {
        var layerId = element.GetProperty("id").GetInt32();
        string[]? adds = null;
        string[]? updates = null;
        long[]? deletes = null;

        if (element.TryGetProperty("addFeatures", out var addFeatures)
            && addFeatures.ValueKind == JsonValueKind.Array)
        {
            adds = addFeatures.EnumerateArray()
                .Select(f => f.GetRawText())
                .ToArray();
        }

        if (element.TryGetProperty("updateFeatures", out var updateFeatures)
            && updateFeatures.ValueKind == JsonValueKind.Array)
        {
            updates = updateFeatures.EnumerateArray()
                .Select(f => f.GetRawText())
                .ToArray();
        }

        if (element.TryGetProperty("deleteIds", out var deleteIds)
            && deleteIds.ValueKind == JsonValueKind.Array)
        {
            deletes = deleteIds.EnumerateArray()
                .Select(d => d.GetInt64())
                .ToArray();
        }

        return new LayerChangeSet
        {
            LayerId = layerId,
            AddFeaturesJson = adds,
            UpdateFeaturesJson = updates,
            DeleteIds = deletes,
        };
    }

    private static void ThrowIfError(JsonElement root, System.Net.HttpStatusCode statusCode)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var message = error.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String
            ? msg.GetString()
            : "Unknown server error";

        var code = error.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
            ? parsedCode
            : (int?)null;

        throw new ReplicaSyncException(
            $"Replica sync error{(code.HasValue ? $" ({code.Value})" : string.Empty)}: {message}",
            statusCode,
            code);
    }

    private static void ValidateServiceId(string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            throw new ArgumentException(
                "Service ID must be a non-empty value and cannot consist only of whitespace.",
                nameof(serviceId));
        }
    }
}
