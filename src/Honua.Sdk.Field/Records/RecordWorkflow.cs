// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Records;

/// <summary>
/// Enforces portable field record workflow transitions.
/// </summary>
public static class RecordWorkflow
{
    /// <summary>
    /// Determines whether a status transition is allowed.
    /// </summary>
    /// <param name="from">Current status.</param>
    /// <param name="to">Target status.</param>
    /// <returns><see langword="true"/> when the transition is valid.</returns>
    public static bool CanTransition(RecordStatus from, RecordStatus to)
    {
        return (from, to) switch
        {
            (RecordStatus.Draft, RecordStatus.ReadyToSubmit) => true,
            (RecordStatus.Draft, RecordStatus.Submitted) => true,
            (RecordStatus.Draft, RecordStatus.Deleted) => true,
            (RecordStatus.ReadyToSubmit, RecordStatus.Submitted) => true,
            (RecordStatus.Submitted, RecordStatus.Approved) => true,
            (RecordStatus.Submitted, RecordStatus.Rejected) => true,
            (RecordStatus.Submitted, RecordStatus.Deleted) => true,
            (RecordStatus.Approved, RecordStatus.Reopened) => true,
            (RecordStatus.Rejected, RecordStatus.Submitted) => true,
            (RecordStatus.Rejected, RecordStatus.Reopened) => true,
            (RecordStatus.Rejected, RecordStatus.Deleted) => true,
            (RecordStatus.Reopened, RecordStatus.ReadyToSubmit) => true,
            (RecordStatus.Reopened, RecordStatus.Submitted) => true,
            (RecordStatus.Reopened, RecordStatus.Deleted) => true,
            _ when from == to => true,
            _ => false,
        };
    }

    /// <summary>
    /// Transitions a record and updates workflow timestamps.
    /// </summary>
    /// <param name="record">Record to transition.</param>
    /// <param name="targetStatus">Target status.</param>
    /// <param name="transitionTimeUtc">Optional transition time. Defaults to current UTC time.</param>
    public static void Transition(FieldRecord record, RecordStatus targetStatus, DateTimeOffset? transitionTimeUtc = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!CanTransition(record.Status, targetStatus))
        {
            throw new InvalidOperationException($"Invalid status transition from {record.Status} to {targetStatus}.");
        }

        record.Status = targetStatus;
        var now = transitionTimeUtc ?? DateTimeOffset.UtcNow;

        if (targetStatus == RecordStatus.Submitted)
        {
            record.SubmittedAtUtc = now;
            record.CompletedAtUtc = null;
        }

        if (targetStatus is RecordStatus.Approved or RecordStatus.Rejected or RecordStatus.Deleted)
        {
            record.CompletedAtUtc = now;
        }

        if (targetStatus is RecordStatus.Reopened or RecordStatus.ReadyToSubmit)
        {
            record.CompletedAtUtc = null;
        }
    }
}
