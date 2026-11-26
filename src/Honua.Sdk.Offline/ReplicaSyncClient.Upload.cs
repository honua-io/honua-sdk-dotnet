// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Offline;

public sealed partial class ReplicaSyncClient
{
    /// <inheritdoc />
    public async Task<ReplicaUploadResult> UploadReplicaAsync(
        string serviceId,
        string replicaId,
        ReplicaUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateServiceId(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(replicaId);
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new Dictionary<string, string>
        {
            ["replicaID"] = replicaId,
            ["syncDirection"] = "upload",
            ["replicaServerGen"] = request.ReceivedServerGen.ToString(CultureInfo.InvariantCulture),
            ["returnIdsForAdds"] = "true",
            ["edits"] = WriteUploadEdits(request.Edits),
            ["f"] = "json",
        };

        if (request.ManualReview)
        {
            parameters["conflictHandling"] = "manualReview";
        }

        if (request.UploadId is { } uploadId)
        {
            parameters["editsUploadID"] = uploadId;
        }

        var url = $"rest/services/{Uri.EscapeDataString(serviceId)}/FeatureServer/synchronizeReplica";
        using var doc = await PostAsync(url, parameters, cancellationToken).ConfigureAwait(false);
        var root = doc.RootElement;

        return new ReplicaUploadResult
        {
            Success = root.GetProperty("success").GetBoolean(),
            ServerGen = root.GetProperty("serverGen").GetInt64(),
            AppliedAdds = ReadCount(root, "appliedAdds"),
            AppliedUpdates = ReadCount(root, "appliedUpdates"),
            AppliedDeletes = ReadCount(root, "appliedDeletes"),
            EditResults = ReadUploadArray(root, "editResults", ReadLayerResult),
            Conflicts = ReadUploadArray(root, "conflicts", ReadConflict),
        };
    }

    private static string WriteUploadEdits(IReadOnlyList<LayerChangeSet> edits)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartArray();
        foreach (var layer in edits)
        {
            writer.WriteStartObject();
            writer.WriteNumber("id", layer.LayerId);
            WriteFeatures(writer, "adds", layer.AddFeaturesJson);
            WriteFeatures(writer, "updates", layer.UpdateFeaturesJson);
            writer.WriteStartArray("deletes");
            foreach (var id in layer.DeleteIds ?? [])
            {
                writer.WriteNumberValue(id);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteFeatures(Utf8JsonWriter writer, string property, IReadOnlyList<string>? features)
    {
        writer.WriteStartArray(property);
        foreach (var feature in features ?? [])
        {
            using var doc = JsonDocument.Parse(feature);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Each uploaded feature must be a JSON object.", nameof(features));
            }

            doc.RootElement.WriteTo(writer);
        }

        writer.WriteEndArray();
    }

    private static T[] ReadUploadArray<T>(JsonElement node, string property, Func<JsonElement, T> read)
        => node.TryGetProperty(property, out var array) && array.ValueKind != JsonValueKind.Null
            ? array.EnumerateArray().Select(read).ToArray()
            : [];

    private static int ReadCount(JsonElement node, string property)
        => node.TryGetProperty(property, out var count) && count.ValueKind != JsonValueKind.Null
            ? count.GetInt32()
            : 0;

    private static string? ReadOptionalString(JsonElement node, string property)
        => node.TryGetProperty(property, out var value) ? value.GetString() : null;

    private static ReplicaUploadLayerResult ReadLayerResult(JsonElement node) => new()
    {
        LayerId = node.GetProperty("id").GetInt32(),
        AddResults = ReadUploadArray(node, "addResults", ReadEditResult),
        UpdateResults = ReadUploadArray(node, "updateResults", ReadEditResult),
        DeleteResults = ReadUploadArray(node, "deleteResults", ReadEditResult),
    };

    private static FeatureEditResult ReadEditResult(JsonElement node) => new()
    {
        ObjectId = node.TryGetProperty("objectId", out var id) && id.ValueKind != JsonValueKind.Null
            ? id.GetInt64()
            : null,
        Id = ReadOptionalString(node, "globalId"),
        Succeeded = node.GetProperty("success").GetBoolean(),
        Error = node.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null
            ? new FeatureEditError
            {
                Code = error.GetProperty("code").GetInt32(),
                Message = ReadOptionalString(error, "description") ?? string.Empty,
            }
            : null,
    };

    private static ReplicaUploadConflict ReadConflict(JsonElement node) => new()
    {
        LayerId = node.GetProperty("layerId").GetInt32(),
        ObjectId = node.GetProperty("objectId").GetInt64(),
        ConflictType = node.GetProperty("conflictType").GetInt32(),
        Applied = node.GetProperty("applied").GetBoolean(),
        EditIndex = node.GetProperty("editIndex").GetInt32(),
        ConflictId = ReadOptionalString(node, "conflictId"),
    };
}
