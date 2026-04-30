// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Outcome returned when a feature stream event is accepted or rejected by sequence tracking.
/// </summary>
public enum FeatureStreamEventDecision
{
    /// <summary>The event was accepted.</summary>
    Accepted = 0,

    /// <summary>The event repeats the last accepted sequence number or sequence token.</summary>
    DuplicateSequence = 1,

    /// <summary>The event is older than the last accepted sequence number.</summary>
    StaleSequence = 2
}

/// <summary>
/// Result from applying sequence tracking to a feature stream event.
/// </summary>
public sealed record FeatureStreamEventProcessResult
{
    /// <summary>Sequence decision for the event.</summary>
    public required FeatureStreamEventDecision Decision { get; init; }

    /// <summary>Whether the event should be delivered to consumers.</summary>
    public bool Accepted => Decision == FeatureStreamEventDecision.Accepted;

    /// <summary>Last accepted monotonic sequence number for the event stream key.</summary>
    public long? LastSequenceNumber { get; init; }

    /// <summary>Last accepted sequence token for the event stream key.</summary>
    public string? LastSequenceToken { get; init; }
}

/// <summary>
/// Tracks feature stream sequence state and rejects duplicate or stale events.
/// </summary>
public sealed class FeatureStreamEventProcessor
{
    private readonly Dictionary<string, StreamCursor> _cursors = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <summary>
    /// Applies sequence tracking to a normalized feature stream event.
    /// </summary>
    /// <param name="featureEvent">Event to evaluate.</param>
    /// <returns>Processing decision and the current cursor state.</returns>
    public FeatureStreamEventProcessResult Process(FeatureStreamEvent featureEvent)
    {
        ArgumentNullException.ThrowIfNull(featureEvent);

        lock (_gate)
        {
            var key = GetCursorKey(featureEvent);
            if (!_cursors.TryGetValue(key, out var cursor))
            {
                cursor = new StreamCursor();
                _cursors.Add(key, cursor);
            }

            if (featureEvent.SequenceNumber.HasValue)
            {
                var sequenceNumber = featureEvent.SequenceNumber.Value;
                if (cursor.LastSequenceNumber.HasValue)
                {
                    if (sequenceNumber == cursor.LastSequenceNumber.Value)
                    {
                        return ToResult(FeatureStreamEventDecision.DuplicateSequence, cursor);
                    }

                    if (sequenceNumber < cursor.LastSequenceNumber.Value)
                    {
                        return ToResult(FeatureStreamEventDecision.StaleSequence, cursor);
                    }
                }

                cursor.LastSequenceNumber = sequenceNumber;
                cursor.LastSequenceToken = featureEvent.SequenceToken ?? cursor.LastSequenceToken;
                return ToResult(FeatureStreamEventDecision.Accepted, cursor);
            }

            if (!string.IsNullOrWhiteSpace(featureEvent.SequenceToken) &&
                string.Equals(featureEvent.SequenceToken, cursor.LastSequenceToken, StringComparison.Ordinal))
            {
                return ToResult(FeatureStreamEventDecision.DuplicateSequence, cursor);
            }

            cursor.LastSequenceToken = featureEvent.SequenceToken ?? cursor.LastSequenceToken;
            return ToResult(FeatureStreamEventDecision.Accepted, cursor);
        }
    }

    /// <summary>
    /// Clears all tracked stream cursors.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _cursors.Clear();
        }
    }

    private static FeatureStreamEventProcessResult ToResult(
        FeatureStreamEventDecision decision,
        StreamCursor cursor)
        => new()
        {
            Decision = decision,
            LastSequenceNumber = cursor.LastSequenceNumber,
            LastSequenceToken = cursor.LastSequenceToken
        };

    private static string GetCursorKey(FeatureStreamEvent featureEvent)
        => string.Join(
            "|",
            featureEvent.SubscriptionId,
            featureEvent.Source.ServiceId ?? string.Empty,
            featureEvent.Source.LayerId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            featureEvent.Source.CollectionId ?? string.Empty,
            featureEvent.Source.TypeName ?? string.Empty);

    private sealed class StreamCursor
    {
        public long? LastSequenceNumber { get; set; }

        public string? LastSequenceToken { get; set; }
    }
}
