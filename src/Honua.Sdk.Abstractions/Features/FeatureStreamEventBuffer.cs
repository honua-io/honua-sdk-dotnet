// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Outcome returned when a feature stream event is written to a bounded buffer.
/// </summary>
public enum FeatureStreamBufferWriteDecision
{
    /// <summary>The event was accepted into the buffer.</summary>
    Accepted = 0,

    /// <summary>The event was rejected by sequence tracking as a duplicate.</summary>
    DuplicateSequence = 1,

    /// <summary>The event was rejected by sequence tracking as stale.</summary>
    StaleSequence = 2,

    /// <summary>The buffer was full and the configured policy rejects incoming events.</summary>
    BackpressureRejected = 3,

    /// <summary>The buffer was full and the configured policy dropped the incoming event.</summary>
    DroppedNewest = 4,

    /// <summary>The buffer has been completed and does not accept new events.</summary>
    Completed = 5
}

/// <summary>
/// Result from writing a feature stream event to a bounded buffer.
/// </summary>
public sealed record FeatureStreamBufferWriteResult
{
    /// <summary>Write decision.</summary>
    public required FeatureStreamBufferWriteDecision Decision { get; init; }

    /// <summary>Whether the incoming event was accepted into the buffer.</summary>
    public bool Accepted => Decision == FeatureStreamBufferWriteDecision.Accepted;

    /// <summary>Whether an older buffered event was dropped to accept the incoming event.</summary>
    public bool DroppedOldest { get; init; }

    /// <summary>Sequence processing result, when a processor was configured.</summary>
    public FeatureStreamEventProcessResult? SequenceResult { get; init; }
}

/// <summary>
/// Browser-safe bounded buffer for normalized feature stream events.
/// </summary>
public sealed class FeatureStreamEventBuffer : IDisposable
{
    private readonly FeatureStreamBackpressureOptions _options;
    private readonly FeatureStreamEventProcessor? _processor;
    private readonly Queue<FeatureStreamEvent> _queue = new();
    private readonly SemaphoreSlim _items = new(0);
    private readonly SemaphoreSlim _space;
    private readonly object _gate = new();
    private bool _completed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureStreamEventBuffer"/> class.
    /// </summary>
    /// <param name="options">Bounded buffer options.</param>
    /// <param name="processor">Optional sequence processor used before events enter the buffer.</param>
    public FeatureStreamEventBuffer(
        FeatureStreamBackpressureOptions? options = null,
        FeatureStreamEventProcessor? processor = null)
    {
        _options = NormalizeOptions(options);
        _processor = processor;

        // No maximum count: completion uses a baton-passing _space.Release() to wake parked
        // writers, which can transiently leave the available-slot count above capacity. A bounded
        // semaphore would throw SemaphoreFullException; capacity is still enforced by the queue
        // and the matched acquire/release accounting on the write/dequeue paths.
        _space = new SemaphoreSlim(_options.Capacity);
    }

    /// <summary>
    /// Attempts to write an event without waiting for buffer capacity.
    /// </summary>
    /// <param name="featureEvent">Event to write.</param>
    /// <returns>Write result.</returns>
    public FeatureStreamBufferWriteResult TryWrite(FeatureStreamEvent featureEvent)
    {
        ArgumentNullException.ThrowIfNull(featureEvent);

        var sequenceResult = ProcessSequence(featureEvent);
        if (sequenceResult is not null && !sequenceResult.Accepted)
        {
            return SequenceRejected(sequenceResult);
        }

        lock (_gate)
        {
            if (_completed)
            {
                return new FeatureStreamBufferWriteResult
                {
                    Decision = FeatureStreamBufferWriteDecision.Completed,
                    SequenceResult = sequenceResult
                };
            }

            // In Wait mode the _space semaphore is the single source of truth for capacity:
            // a non-blocking acquire both tests for free space and reserves it atomically,
            // avoiding the divergence a separate _queue.Count pre-check could introduce under
            // concurrent producers. Every successful acquire is matched by a _space.Release()
            // when the event is dequeued in ReadAllAsync, keeping the count balanced.
            if (_options.Mode == FeatureStreamBackpressureMode.Wait)
            {
                if (!_space.Wait(0))
                {
                    return new FeatureStreamBufferWriteResult
                    {
                        Decision = FeatureStreamBufferWriteDecision.BackpressureRejected,
                        SequenceResult = sequenceResult
                    };
                }

                _queue.Enqueue(featureEvent);
                _items.Release();
                return new FeatureStreamBufferWriteResult
                {
                    Decision = FeatureStreamBufferWriteDecision.Accepted,
                    SequenceResult = sequenceResult
                };
            }

            if (_queue.Count >= _options.Capacity)
            {
                return _options.Mode switch
                {
                    FeatureStreamBackpressureMode.DropOldest => EnqueueDroppingOldest(featureEvent, sequenceResult),
                    FeatureStreamBackpressureMode.DropNewest => new FeatureStreamBufferWriteResult
                    {
                        Decision = FeatureStreamBufferWriteDecision.DroppedNewest,
                        SequenceResult = sequenceResult
                    },
                    _ => new FeatureStreamBufferWriteResult
                    {
                        Decision = FeatureStreamBufferWriteDecision.BackpressureRejected,
                        SequenceResult = sequenceResult
                    }
                };
            }

            _queue.Enqueue(featureEvent);
            _items.Release();
            return new FeatureStreamBufferWriteResult
            {
                Decision = FeatureStreamBufferWriteDecision.Accepted,
                SequenceResult = sequenceResult
            };
        }
    }

    /// <summary>
    /// Writes an event, waiting for capacity when the mode is <see cref="FeatureStreamBackpressureMode.Wait"/>.
    /// </summary>
    /// <param name="featureEvent">Event to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Write result.</returns>
    public async ValueTask<FeatureStreamBufferWriteResult> WriteAsync(
        FeatureStreamEvent featureEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(featureEvent);

        if (_options.Mode != FeatureStreamBackpressureMode.Wait)
        {
            return TryWrite(featureEvent);
        }

        var sequenceResult = ProcessSequence(featureEvent);
        if (sequenceResult is not null && !sequenceResult.Accepted)
        {
            return SequenceRejected(sequenceResult);
        }

        await _space.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_completed)
            {
                // We consumed a completion wakeup, not a real capacity slot. Pass the baton on to
                // the next parked writer so every waiter wakes, unless the buffer is already being
                // disposed (the semaphore may be gone). Done under _gate to order against Dispose().
                if (!_disposed)
                {
                    _space.Release();
                }

                return new FeatureStreamBufferWriteResult
                {
                    Decision = FeatureStreamBufferWriteDecision.Completed,
                    SequenceResult = sequenceResult
                };
            }

            _queue.Enqueue(featureEvent);
            _items.Release();
            return new FeatureStreamBufferWriteResult
            {
                Decision = FeatureStreamBufferWriteDecision.Accepted,
                SequenceResult = sequenceResult
            };
        }
    }

    /// <summary>
    /// Reads buffered events until the buffer is completed or cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Buffered events.</returns>
    public async IAsyncEnumerable<FeatureStreamEvent> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await _items.WaitAsync(cancellationToken).ConfigureAwait(false);

            FeatureStreamEvent? featureEvent = null;
            var completeAfterYield = false;
            var shouldStop = false;
            lock (_gate)
            {
                if (_queue.Count == 0)
                {
                    shouldStop = _completed;
                }
                else
                {
                    featureEvent = _queue.Dequeue();
                    if (_options.Mode == FeatureStreamBackpressureMode.Wait)
                    {
                        _space.Release();
                    }

                    completeAfterYield = _completed && _queue.Count == 0;
                }
            }

            if (shouldStop)
            {
                // Completed with an empty queue. Complete() only released _items once, so a
                // single reader was woken. Pass the wakeup on to the next blocked reader before
                // exiting so every concurrent reader drains rather than hanging on a lost wakeup.
                _items.Release();
                yield break;
            }

            if (featureEvent is null)
            {
                continue;
            }

            yield return featureEvent;
            if (completeAfterYield)
            {
                // We drained the final queued item after completion. Baton-pass a wakeup so any
                // other readers blocked on an empty, completed queue also wake and exit.
                _items.Release();
                yield break;
            }
        }
    }

    /// <summary>
    /// Completes the buffer and wakes pending readers and writers.
    /// </summary>
    /// <remarks>
    /// When the queue is empty a single reader wakeup is released; that reader observes
    /// completion and passes the wakeup on to the next blocked reader (baton passing) so
    /// all blocked readers drain. A writer parked in <see cref="FeatureStreamBackpressureMode.Wait"/>
    /// mode is woken by releasing <c>_space</c>; it then observes completion under the gate and
    /// returns <see cref="FeatureStreamBufferWriteDecision.Completed"/> instead of faulting.
    /// </remarks>
    public void Complete()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;

            // Wake one reader when nothing is queued; it baton-passes the wakeup to peers.
            if (_queue.Count == 0)
            {
                _items.Release();
            }

            // Wake a writer parked on _space.WaitAsync so it observes completion gracefully
            // instead of faulting with ObjectDisposedException once the semaphore is disposed.
            // The woken writer re-checks _completed under the gate and releases _space again,
            // which keeps the wakeup propagating to any further parked writers without
            // permanently inflating the capacity count.
            if (_options.Mode == FeatureStreamBackpressureMode.Wait)
            {
                _space.Release();
            }
        }
    }

    /// <summary>
    /// Completes the buffer and releases synchronization resources.
    /// </summary>
    public void Dispose()
    {
        // Complete() (under _gate) wakes parked readers/writers before any semaphore is disposed.
        Complete();

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _items.Dispose();
        _space.Dispose();
    }

    private FeatureStreamBufferWriteResult EnqueueDroppingOldest(
        FeatureStreamEvent featureEvent,
        FeatureStreamEventProcessResult? sequenceResult)
    {
        _queue.Dequeue();
        _queue.Enqueue(featureEvent);
        return new FeatureStreamBufferWriteResult
        {
            Decision = FeatureStreamBufferWriteDecision.Accepted,
            DroppedOldest = true,
            SequenceResult = sequenceResult
        };
    }

    private FeatureStreamEventProcessResult? ProcessSequence(FeatureStreamEvent featureEvent)
        => _processor?.Process(featureEvent);

    private static FeatureStreamBufferWriteResult SequenceRejected(FeatureStreamEventProcessResult sequenceResult)
        => new()
        {
            Decision = sequenceResult.Decision == FeatureStreamEventDecision.DuplicateSequence
                ? FeatureStreamBufferWriteDecision.DuplicateSequence
                : FeatureStreamBufferWriteDecision.StaleSequence,
            SequenceResult = sequenceResult
        };

    private static FeatureStreamBackpressureOptions NormalizeOptions(FeatureStreamBackpressureOptions? options)
    {
        var normalized = options ?? new FeatureStreamBackpressureOptions();
        if (normalized.Capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Feature stream buffer capacity must be greater than zero.");
        }

        return normalized;
    }
}
