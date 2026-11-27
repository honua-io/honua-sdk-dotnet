// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Field.Tasks;

/// <summary>Filters one page by service and exact assignee.</summary>
public sealed record AssignedWorkTaskQuery
{
    /// <summary>Canonical service identifier from Server metadata.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Exact assignee handle established for the signed-in viewer.</summary>
    public required string Assignee { get; init; }

    /// <summary>Continuation cursor from the previous page.</summary>
    public string? Cursor { get; init; }

    /// <summary>Requested page size.</summary>
    public int? Limit { get; init; }
}

/// <summary>One page of explicitly assigned work tasks.</summary>
public sealed record WorkTaskPage
{
    /// <summary>Assigned tasks in the page.</summary>
    public required IReadOnlyList<WorkTask> Items { get; init; }

    /// <summary>Continuation cursor, when another page exists.</summary>
    public string? NextCursor { get; init; }
}

/// <summary>A server-owned work task.</summary>
public sealed record WorkTask
{
    /// <summary>Task identifier.</summary>
    public required Guid TaskId { get; init; }

    /// <summary>Task service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Server task type.</summary>
    public required string Type { get; init; }

    /// <summary>Server workflow status.</summary>
    public required string Status { get; init; }

    /// <summary>Version used for compare-and-set commands.</summary>
    public required long Version { get; init; }

    /// <summary>Task title.</summary>
    public required string Title { get; init; }

    /// <summary>Assigned user handle.</summary>
    public string? Assignee { get; init; }

    /// <summary>Associated asset profile identifier.</summary>
    public Guid? ProfileId { get; init; }

    /// <summary>Associated asset identifier.</summary>
    public string? AssetId { get; init; }

    /// <summary>Server task priority.</summary>
    public string? Priority { get; init; }

    /// <summary>Due time, with its offset.</summary>
    public DateTimeOffset? DueAt { get; init; }

    /// <summary>Task geometry returned by the server.</summary>
    public JsonElement? Geometry { get; init; }

    /// <summary>Pinned form reference.</summary>
    public WorkTaskFormReference? Form { get; init; }

    /// <summary>Actions permitted by the server when included.</summary>
    public IReadOnlyList<string>? PermittedActions { get; init; }
}

/// <summary>A reference to the form version a task uses.</summary>
public sealed record WorkTaskFormReference
{
    /// <summary>Form identifier.</summary>
    public required string FormId { get; init; }

    /// <summary>Pinned form version.</summary>
    public required int Version { get; init; }
}

/// <summary>Creates an unassigned request for an existing asset.</summary>
public sealed record WorkTaskAssetRequest
{
    /// <summary>Stable command identifier to reuse on replay.</summary>
    public required Guid CommandId { get; init; }

    /// <summary>Stable client-generated identifier of the requested task.</summary>
    public required Guid TaskId { get; init; }

    /// <summary>Canonical service identifier of the asset.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Asset profile identifier.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Canonical asset identifier from the specified profile, as returned by Honua Server.</summary>
    public required string AssetId { get; init; }

    /// <summary>Title of the request.</summary>
    public required string Title { get; init; }
}

/// <summary>A compare-and-set task transition.</summary>
public sealed record WorkTaskTransitionRequest
{
    /// <summary>Stable client command identifier to reuse on replay.</summary>
    public required Guid CommandId { get; init; }

    /// <summary>Expected current task version.</summary>
    public required long ExpectedVersion { get; init; }

    /// <summary>Server workflow action.</summary>
    public required string Action { get; init; }

    /// <summary>Target assignee if the action requires one.</summary>
    public string? Assignee { get; init; }

    /// <summary>Reason if the action requires one.</summary>
    public string? Reason { get; init; }

    /// <summary>Observed action time.</summary>
    public DateTimeOffset? ObservedAt { get; init; }
}

/// <summary>A compare-and-set task completion.</summary>
public sealed record WorkTaskCompletionRequest
{
    /// <summary>Stable client command identifier to reuse on replay.</summary>
    public required Guid CommandId { get; init; }

    /// <summary>Expected current task version.</summary>
    public required long ExpectedVersion { get; init; }

    /// <summary>Asset summary revision observed by the worker.</summary>
    public long? SummaryRevision { get; init; }

    /// <summary>Observed completion time.</summary>
    public DateTimeOffset? ObservedAt { get; init; }

    /// <summary>Pinned form reference used during capture.</summary>
    public WorkTaskFormReference? Form { get; init; }

    /// <summary>Condition recorded by the worker.</summary>
    public int? Condition { get; init; }

    /// <summary>Form answers keyed by field identifier.</summary>
    public JsonElement? Answers { get; init; }

    /// <summary>Worker notes.</summary>
    public string? Notes { get; init; }
}

/// <summary>A server receipt for a task command.</summary>
public sealed record WorkTaskReceipt
{
    /// <summary>Server receipt outcome, including duplicate on replay.</summary>
    public required string Outcome { get; init; }

    /// <summary>Identifier of the submitted command.</summary>
    public required Guid CommandId { get; init; }

    /// <summary>Task recorded in the command receipt.</summary>
    public required WorkTask Task { get; init; }

    /// <summary>Inspection created by completion, when applicable.</summary>
    public Guid? InspectionId { get; init; }

    /// <summary>Service record created by completion, when applicable.</summary>
    public Guid? ServiceRecordId { get; init; }
}
