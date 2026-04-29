// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Offline;

/// <summary>
/// Options for the provider-neutral offline sync engine.
/// </summary>
public sealed record OfflineSyncEngineOptions
{
    /// <summary>Maximum number of local edit operations to upload in one push run.</summary>
    public int BatchSize { get; init; } = 50;

    /// <summary>Maximum number of attempts before a local operation is marked failed.</summary>
    public int MaxAttempts { get; init; } = 8;

    /// <summary>Default delay recorded for retryable upload failures.</summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>Conflict strategy used when providers reject an edit due to stale local state.</summary>
    public OfflineConflictStrategy ConflictStrategy { get; init; } = OfflineConflictStrategy.ManualReview;

    /// <summary>
    /// Validates option values.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an option value is outside its supported range.</exception>
    public void Validate()
    {
        if (BatchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "Batch size must be greater than zero.");
        }

        if (MaxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxAttempts), MaxAttempts, "Max attempts must be greater than zero.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RetryDelay), RetryDelay, "Retry delay cannot be negative.");
        }
    }
}
