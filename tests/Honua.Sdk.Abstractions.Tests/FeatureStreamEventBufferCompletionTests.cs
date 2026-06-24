// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class FeatureStreamEventBufferCompletionTests
{
    [Fact]
    public async Task Complete_WakesAllConcurrentReadersWhenQueueIsEmpty()
    {
        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 4,
            Mode = FeatureStreamBackpressureMode.Wait
        });

        using var failFast = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Start several readers that immediately block on an empty queue.
        var readers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => DrainAsync(buffer.ReadAllAsync(failFast.Token)), failFast.Token))
            .ToArray();

        // Give the readers a moment to park on _items.WaitAsync.
        await Task.Delay(50, failFast.Token);

        buffer.Complete();

        // All readers must complete; a lost wakeup would hang here until the 5s timeout fires.
        var results = await Task.WhenAll(readers).WaitAsync(failFast.Token);

        Assert.All(results, items => Assert.Empty(items));
    }

    [Fact]
    public async Task Complete_WakesAllConcurrentReadersAfterFinalDrain()
    {
        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 4,
            Mode = FeatureStreamBackpressureMode.Wait
        });

        using var failFast = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var readers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() => DrainAsync(buffer.ReadAllAsync(failFast.Token)), failFast.Token))
            .ToArray();

        await Task.Delay(50, failFast.Token);

        // Enqueue a single item, then complete. One reader drains it; the rest must still wake.
        var write = await buffer.WriteAsync(Event("only"), failFast.Token);
        buffer.Complete();

        var results = await Task.WhenAll(readers).WaitAsync(failFast.Token);

        Assert.True(write.Accepted);
        Assert.Single(results.SelectMany(items => items));
    }

    [Fact]
    public async Task Complete_ReleasesWriterParkedInWaitMode()
    {
        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 1,
            Mode = FeatureStreamBackpressureMode.Wait
        });

        using var failFast = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Fill the buffer so the next writer parks on _space.WaitAsync.
        var first = await buffer.WriteAsync(Event("first"), failFast.Token);
        Assert.True(first.Accepted);

        var parkedWriter = Task.Run(() => buffer.WriteAsync(Event("second"), failFast.Token).AsTask(), failFast.Token);

        // Ensure the writer is genuinely parked before completing.
        await Task.Delay(50, failFast.Token);
        Assert.False(parkedWriter.IsCompleted);

        buffer.Complete();

        // The parked writer must observe completion gracefully (no ObjectDisposedException).
        var result = await parkedWriter.WaitAsync(failFast.Token);

        Assert.Equal(FeatureStreamBufferWriteDecision.Completed, result.Decision);
    }

    [Fact]
    public async Task Dispose_DoesNotFaultParkedWriter()
    {
        var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = 1,
            Mode = FeatureStreamBackpressureMode.Wait
        });

        using var failFast = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        Assert.True((await buffer.WriteAsync(Event("first"), failFast.Token)).Accepted);

        var parkedWriter = Task.Run(() => buffer.WriteAsync(Event("second"), failFast.Token).AsTask(), failFast.Token);
        await Task.Delay(50, failFast.Token);

        // Dispose calls Complete() before disposing the semaphores, so the parked writer
        // is woken and returns Completed rather than throwing ObjectDisposedException.
        buffer.Dispose();

        var result = await parkedWriter.WaitAsync(failFast.Token);
        Assert.Equal(FeatureStreamBufferWriteDecision.Completed, result.Decision);
    }

    [Fact]
    public async Task TryWrite_WaitMode_DoesNotSpuriouslyRejectWhenCapacityAvailable()
    {
        const int capacity = 64;
        const int producers = 8;

        using var buffer = new FeatureStreamEventBuffer(new FeatureStreamBackpressureOptions
        {
            Capacity = capacity,
            Mode = FeatureStreamBackpressureMode.Wait
        });

        using var failFast = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Concurrent producers write exactly `capacity` events total. Since no reader drains,
        // every slot is real capacity. With the single-gate fix none should be spuriously rejected.
        var perProducer = capacity / producers;
        var writers = Enumerable.Range(0, producers)
            .Select(p => Task.Run(
                () =>
                {
                    var results = new List<FeatureStreamBufferWriteResult>();
                    for (var i = 0; i < perProducer; i++)
                    {
                        results.Add(buffer.TryWrite(Event($"p{p}-{i}")));
                    }

                    return results;
                },
                failFast.Token))
            .ToArray();

        var all = (await Task.WhenAll(writers).WaitAsync(failFast.Token)).SelectMany(r => r).ToList();

        Assert.Equal(capacity, all.Count);
        Assert.All(all, r => Assert.True(r.Accepted, $"unexpected decision {r.Decision}"));

        // The (capacity+1)th write must now be rejected: capacity is genuinely exhausted.
        var overflow = buffer.TryWrite(Event("overflow"));
        Assert.Equal(FeatureStreamBufferWriteDecision.BackpressureRejected, overflow.Decision);

        buffer.Complete();
        var drained = await DrainAsync(buffer.ReadAllAsync(failFast.Token));
        Assert.Equal(capacity, drained.Count);
    }

    private static FeatureStreamEvent Event(string featureId)
        => new()
        {
            ProviderName = "test",
            SubscriptionId = "sub-1",
            Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
            Kind = FeatureStreamEventKind.Update,
            FeatureId = featureId,
            Timestamp = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero)
        };

    private static async Task<List<T>> DrainAsync<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }
}
