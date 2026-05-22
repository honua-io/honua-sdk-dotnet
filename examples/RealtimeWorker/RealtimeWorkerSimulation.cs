using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace RealtimeWorker;

public static class RealtimeWorkerSimulation
{
    public static async Task<RealtimeWorkerRunSummary> RunAsync(
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);

        var subscription = CreateSubscription();
        using var buffer = new FeatureStreamEventBuffer(
            new FeatureStreamBackpressureOptions
            {
                Capacity = 16,
                Mode = FeatureStreamBackpressureMode.Reject
            },
            new FeatureStreamEventProcessor());

        var writeResults = new List<RealtimeWorkerWriteRecord>();

        await output.WriteLineAsync("Mode: simulated");
        await output.WriteLineAsync("Transport: deterministic FeatureStreamEvent envelopes");
        await output.WriteLineAsync(
            $"Subscription: {subscription.SubscriptionId} source={subscription.Source.ServiceId}/{subscription.Source.LayerId?.ToString(CultureInfo.InvariantCulture) ?? "none"}");
        await output.WriteLineAsync();
        await output.WriteLineAsync("Write decisions:");

        foreach (var featureEvent in CreateEvents())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = buffer.TryWrite(featureEvent);
            var record = new RealtimeWorkerWriteRecord(featureEvent, result);
            writeResults.Add(record);
            await output.WriteLineAsync($"  {FormatWriteRecord(record)}");
        }

        buffer.Complete();

        var projection = new Dictionary<string, IncidentProjection>(StringComparer.Ordinal);
        var closedCount = 0;
        await foreach (var featureEvent in buffer.ReadAllAsync(cancellationToken))
        {
            if (featureEvent.FeatureId is null)
            {
                continue;
            }

            if (featureEvent.Kind == FeatureStreamEventKind.Delete)
            {
                projection.Remove(featureEvent.FeatureId);
                closedCount++;
                continue;
            }

            projection[featureEvent.FeatureId] = new IncidentProjection(
                featureEvent.FeatureId,
                ReadAttribute(featureEvent, "status"),
                featureEvent.SequenceNumber ?? 0,
                featureEvent.ResumeToken);
        }

        var acceptedCount = writeResults.Count(result => result.Result.Accepted);
        var lastAccepted = writeResults
            .Where(result => result.Result.Accepted)
            .Select(result => result.Event)
            .Last();

        await output.WriteLineAsync();
        await output.WriteLineAsync("Projection:");
        foreach (var incident in projection.Values.OrderBy(item => item.IncidentId, StringComparer.Ordinal))
        {
            await output.WriteLineAsync(
                $"  {incident.IncidentId} status={incident.Status} sequence={incident.LastSequenceNumber} resume={incident.ResumeToken}");
        }

        await output.WriteLineAsync(
            $"  active={projection.Count} closed={closedCount} lastSequence={lastAccepted.SequenceNumber} resume={lastAccepted.ResumeToken}");

        return new RealtimeWorkerRunSummary(
            AcceptedCount: acceptedCount,
            RejectedCount: writeResults.Count - acceptedCount,
            ActiveIncidentCount: projection.Count,
            ClosedIncidentCount: closedCount,
            LastSequenceNumber: lastAccepted.SequenceNumber,
            ResumeToken: lastAccepted.ResumeToken,
            Decisions: writeResults.Select(result => result.Result.Decision).ToArray());
    }

    public static FeatureStreamSubscribeRequest CreateSubscription() => new()
    {
        SubscriptionId = "incidents-active",
        Source = new FeatureSource { ServiceId = "incidents", LayerId = 0 },
        Filter = "status <> 'closed'",
        FilterLanguage = FeatureFilterLanguage.SqlWhere,
        OutFields = ["incidentId", "status", "priority", "assignedUnit"],
        ReturnGeometry = true
    };

    public static IReadOnlyList<FeatureStreamEvent> CreateEvents() =>
    [
        CreateEvent(FeatureStreamEventKind.Subscribed, sequence: 1, featureId: null),
        CreateEvent(FeatureStreamEventKind.Insert, sequence: 2, status: "open", priority: "high"),
        CreateEvent(FeatureStreamEventKind.Update, sequence: 2, status: "duplicate"),
        CreateEvent(FeatureStreamEventKind.Update, sequence: 1, status: "stale"),
        CreateEvent(FeatureStreamEventKind.Update, sequence: 3, status: "assigned", assignedUnit: "unit-7"),
        CreateEvent(FeatureStreamEventKind.Delete, sequence: 4, status: "closed")
    ];

    private static FeatureStreamEvent CreateEvent(
        FeatureStreamEventKind kind,
        long sequence,
        string? featureId = "incident-42",
        string? status = null,
        string? priority = null,
        string? assignedUnit = null)
    {
        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (featureId is not null)
        {
            attributes["incidentId"] = JsonSerializer.SerializeToElement(featureId);
        }

        if (status is not null)
        {
            attributes["status"] = JsonSerializer.SerializeToElement(status);
        }

        if (priority is not null)
        {
            attributes["priority"] = JsonSerializer.SerializeToElement(priority);
        }

        if (assignedUnit is not null)
        {
            attributes["assignedUnit"] = JsonSerializer.SerializeToElement(assignedUnit);
        }

        return new FeatureStreamEvent
        {
            ProviderName = "simulated-realtime",
            ConnectionId = "connection-dotnet-demo",
            SubscriptionId = "incidents-active",
            Source = new FeatureSource { ServiceId = "incidents", LayerId = 0 },
            Kind = kind,
            FeatureId = featureId,
            ObjectId = featureId is null ? null : 42,
            Timestamp = new DateTimeOffset(2026, 5, 6, 12, 0, (int)sequence, TimeSpan.Zero),
            SequenceNumber = sequence,
            SequenceToken = $"seq-{sequence}",
            ResumeToken = $"resume-{sequence}",
            Attributes = attributes
        };
    }

    private static string FormatWriteRecord(RealtimeWorkerWriteRecord record)
    {
        var feature = record.Event.FeatureId is null ? string.Empty : $" {record.Event.FeatureId}";
        var sequence = record.Event.SequenceNumber?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var last = record.Result.SequenceResult?.LastSequenceNumber?.ToString(CultureInfo.InvariantCulture) ?? "none";
        var decision = record.Result.Decision switch
        {
            FeatureStreamBufferWriteDecision.Accepted => "accepted",
            FeatureStreamBufferWriteDecision.DuplicateSequence => "duplicate-sequence",
            FeatureStreamBufferWriteDecision.StaleSequence => "stale-sequence",
            _ => record.Result.Decision.ToString()
        };

        return $"#{sequence} {record.Event.Kind}{feature} {decision} last={last} resume={record.Event.ResumeToken}";
    }

    private static string ReadAttribute(FeatureStreamEvent featureEvent, string name)
    {
        if (!featureEvent.Attributes.TryGetValue(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return value.GetString() ?? string.Empty;
    }

    private sealed record IncidentProjection(
        string IncidentId,
        string Status,
        long LastSequenceNumber,
        string? ResumeToken);
}

public sealed record RealtimeWorkerRunSummary(
    int AcceptedCount,
    int RejectedCount,
    int ActiveIncidentCount,
    int ClosedIncidentCount,
    long? LastSequenceNumber,
    string? ResumeToken,
    IReadOnlyList<FeatureStreamBufferWriteDecision> Decisions);

public sealed record RealtimeWorkerWriteRecord(
    FeatureStreamEvent Event,
    FeatureStreamBufferWriteResult Result);
