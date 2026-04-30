// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class FeatureStreamContractTests
{
    [Fact]
    public void Processor_RejectsDuplicateAndStaleSequencesPerSubscription()
    {
        var processor = new FeatureStreamEventProcessor();

        var first = processor.Process(Event(sequenceNumber: 10, subscriptionId: "parks"));
        var duplicate = processor.Process(Event(sequenceNumber: 10, subscriptionId: "parks"));
        var stale = processor.Process(Event(sequenceNumber: 9, subscriptionId: "parks"));
        var next = processor.Process(Event(sequenceNumber: 11, subscriptionId: "parks"));
        var otherSubscription = processor.Process(Event(sequenceNumber: 1, subscriptionId: "roads"));

        Assert.True(first.Accepted);
        Assert.Equal(FeatureStreamEventDecision.DuplicateSequence, duplicate.Decision);
        Assert.Equal(FeatureStreamEventDecision.StaleSequence, stale.Decision);
        Assert.True(next.Accepted);
        Assert.True(otherSubscription.Accepted);
        Assert.Equal(11, next.LastSequenceNumber);
    }

    [Fact]
    public void Processor_RejectsDuplicateOpaqueSequenceToken()
    {
        var processor = new FeatureStreamEventProcessor();

        var first = processor.Process(Event(sequenceToken: "token-1"));
        var duplicate = processor.Process(Event(sequenceToken: "token-1"));
        var next = processor.Process(Event(sequenceToken: "token-2"));

        Assert.True(first.Accepted);
        Assert.Equal(FeatureStreamEventDecision.DuplicateSequence, duplicate.Decision);
        Assert.True(next.Accepted);
        Assert.Equal("token-2", next.LastSequenceToken);
    }

    [Fact]
    public async Task Buffer_RejectsIncomingEventWhenFull()
    {
        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 1,
            Mode = FeatureStreamBackpressureMode.Reject
        });

        var first = buffer.TryWrite(Event("first"));
        var second = buffer.TryWrite(Event("second"));
        buffer.Complete();

        var events = await DrainAsync(buffer.ReadAllAsync());

        Assert.True(first.Accepted);
        Assert.Equal(FeatureStreamBufferWriteDecision.BackpressureRejected, second.Decision);
        var item = Assert.Single(events);
        Assert.Equal("first", item.FeatureId);
    }

    [Fact]
    public async Task Buffer_DropsOldestWhenConfigured()
    {
        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 1,
            Mode = FeatureStreamBackpressureMode.DropOldest
        });

        var first = buffer.TryWrite(Event("first"));
        var second = buffer.TryWrite(Event("second"));
        buffer.Complete();

        var events = await DrainAsync(buffer.ReadAllAsync());

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(second.DroppedOldest);
        var item = Assert.Single(events);
        Assert.Equal("second", item.FeatureId);
    }

    [Fact]
    public async Task Buffer_AppliesSequenceProcessorBeforeQueueing()
    {
        var processor = new FeatureStreamEventProcessor();
        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 2,
            Mode = FeatureStreamBackpressureMode.Reject
        }, processor);

        var first = buffer.TryWrite(Event("first", sequenceNumber: 1));
        var duplicate = buffer.TryWrite(Event("first-duplicate", sequenceNumber: 1));
        buffer.Complete();

        var events = await DrainAsync(buffer.ReadAllAsync());

        Assert.True(first.Accepted);
        Assert.Equal(FeatureStreamBufferWriteDecision.DuplicateSequence, duplicate.Decision);
        var item = Assert.Single(events);
        Assert.Equal("first", item.FeatureId);
    }

    [Fact]
    public async Task Interface_ModelsConnectReconnectHeartbeatAndSubscriptionWorkflows()
    {
        var client = new FakeStreamClient();
        var subscription = new FeatureStreamSubscribeRequest
        {
            SubscriptionId = "sub-1",
            Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
            Filter = "status = 'open'",
            ReturnGeometry = true
        };

        var connection = await client.ConnectAsync(new FeatureStreamConnectRequest
        {
            ClientId = "mobile-1",
            Subscriptions = [subscription],
            HeartbeatInterval = TimeSpan.FromSeconds(30),
            Backpressure = new FeatureStreamBackpressureOptions { Capacity = 32 }
        });
        var heartbeat = await client.HeartbeatAsync(new FeatureStreamHeartbeatRequest
        {
            ConnectionId = connection.ConnectionId,
            LastSequenceNumber = 2,
            ResumeToken = "resume-2"
        });
        var reconnected = await client.ReconnectAsync(new FeatureStreamReconnectRequest
        {
            ConnectionId = connection.ConnectionId,
            LastSequenceNumber = heartbeat.LastSequenceNumber,
            ResumeToken = heartbeat.ResumeToken,
            Subscriptions = [subscription]
        });
        await client.UnsubscribeAsync(new FeatureStreamUnsubscribeRequest
        {
            ConnectionId = reconnected.ConnectionId,
            SubscriptionId = subscription.SubscriptionId
        });

        var events = await DrainAsync(client.SubscribeAsync(subscription));

        Assert.Equal("fake-stream", client.ProviderName);
        Assert.True(client.StreamCapabilities.SupportsReconnect);
        Assert.Equal(FeatureStreamConnectionState.Connected, connection.State);
        Assert.Equal(2, heartbeat.LastSequenceNumber);
        Assert.Equal("resume-2", reconnected.ResumeToken);
        Assert.Equal([FeatureStreamEventKind.Subscribed, FeatureStreamEventKind.Insert], events.Select(item => item.Kind));
    }

    private static FeatureStreamEvent Event(
        string featureId = "feature-1",
        long? sequenceNumber = null,
        string? sequenceToken = null,
        string subscriptionId = "sub-1")
        => new()
        {
            ProviderName = "test",
            SubscriptionId = subscriptionId,
            Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
            Kind = FeatureStreamEventKind.Update,
            FeatureId = featureId,
            Timestamp = Timestamp(),
            SequenceNumber = sequenceNumber,
            SequenceToken = sequenceToken
        };

    private static DateTimeOffset Timestamp(int minute = 0, int second = 0)
        => new(2026, 4, 30, 0, minute, second, TimeSpan.Zero);

    private static async Task<List<T>> DrainAsync<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class FakeStreamClient : IHonuaFeatureStreamClient
    {
        public string ProviderName => "fake-stream";

        public FeatureStreamCapabilities StreamCapabilities { get; } = new()
        {
            SupportsConnect = true,
            SupportsReconnect = true,
            SupportsHeartbeat = true,
            SupportsSubscribe = true,
            SupportsUnsubscribe = true,
            SupportsSequenceNumbers = true,
            SupportsResumeTokens = true,
            SupportsBackpressure = true,
            NativeSurface = "test"
        };

        public Task<FeatureStreamConnection> ConnectAsync(
            FeatureStreamConnectRequest request,
            CancellationToken ct = default)
            => Task.FromResult(new FeatureStreamConnection
            {
                ConnectionId = request.ClientId ?? "connection-1",
                State = FeatureStreamConnectionState.Connected,
                ConnectedAt = Timestamp(),
                HeartbeatInterval = request.HeartbeatInterval,
                LastSequenceNumber = request.Subscriptions.Count
            });

        public Task<FeatureStreamConnection> ReconnectAsync(
            FeatureStreamReconnectRequest request,
            CancellationToken ct = default)
            => Task.FromResult(new FeatureStreamConnection
            {
                ConnectionId = request.ConnectionId,
                State = FeatureStreamConnectionState.Connected,
                ConnectedAt = Timestamp(minute: 1),
                LastSequenceNumber = request.LastSequenceNumber,
                ResumeToken = request.ResumeToken
            });

        public async IAsyncEnumerable<FeatureStreamEvent> SubscribeAsync(
            FeatureStreamSubscribeRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            yield return Event(sequenceNumber: request.StartAfterSequenceNumber, subscriptionId: request.SubscriptionId) with
            {
                Kind = FeatureStreamEventKind.Subscribed,
                FeatureId = null
            };
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            yield return Event("feature-1", sequenceNumber: 2, subscriptionId: request.SubscriptionId) with
            {
                Kind = FeatureStreamEventKind.Insert,
                Source = request.Source
            };
        }

        public Task UnsubscribeAsync(
            FeatureStreamUnsubscribeRequest request,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<FeatureStreamHeartbeat> HeartbeatAsync(
            FeatureStreamHeartbeatRequest request,
            CancellationToken ct = default)
            => Task.FromResult(new FeatureStreamHeartbeat
            {
                ConnectionId = request.ConnectionId,
                Timestamp = Timestamp(second: 30),
                LastSequenceNumber = request.LastSequenceNumber,
                LastSequenceToken = request.LastSequenceToken,
                ResumeToken = request.ResumeToken
            });
    }
}
