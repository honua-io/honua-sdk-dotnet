// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Operations;

/// <summary>Server-owned operation identity and approval context, independent of its protocol entry point.</summary>
public sealed class HonuaOperationHandle
{
    private IReadOnlyDictionary<string, string> _resourceIds = new Dictionary<string, string>();

    /// <summary>Stable identity of this invocation.</summary>
    [JsonPropertyName("operationInstanceId")]
    public required string OperationInstanceId { get; init; }

    /// <summary>Descriptor identifier of the operation.</summary>
    [JsonPropertyName("operationId")]
    public required string OperationId { get; init; }

    /// <summary>Current operation lifecycle status.</summary>
    [JsonPropertyName("status")]
    public required HonuaOperationStatus Status { get; init; }

    /// <summary>Independent correlation identity for diagnostics.</summary>
    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    /// <summary>Time the operation was created.</summary>
    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Time the operation was last updated.</summary>
    [JsonPropertyName("updatedAt")]
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Durable proposal identity when approval is required.</summary>
    [JsonPropertyName("proposalId")]
    public string? ProposalId { get; init; }

    /// <summary>Approval lane selected by server policy.</summary>
    [JsonPropertyName("approvalLane")]
    public string? ApprovalLane { get; init; }

    /// <summary>Durable audit identity, when available.</summary>
    [JsonPropertyName("auditId")]
    public string? AuditId { get; init; }

    /// <summary>Reason for a non-completed outcome.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>Stable resource identities targeted or produced by the operation.</summary>
    [JsonPropertyName("resourceIds")]
    public IReadOnlyDictionary<string, string> ResourceIds
    {
        get => _resourceIds;
        init => _resourceIds = value ?? new Dictionary<string, string>();
    }
}

/// <summary>Lifecycle status reported by the server operation runtime.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaOperationStatus>))]
public enum HonuaOperationStatus
{
    /// <summary>The invocation was accepted.</summary>
    Accepted,

    /// <summary>Execution completed.</summary>
    Completed,

    /// <summary>Execution was queued.</summary>
    Queued,

    /// <summary>Execution is running.</summary>
    Running,

    /// <summary>A separate authorized approval is required before execution.</summary>
    RequiresApproval,

    /// <summary>A preview is required before committed execution.</summary>
    DryRunRequired,

    /// <summary>Policy denied the invocation.</summary>
    Denied,

    /// <summary>A reviewer rejected the proposal.</summary>
    Rejected,

    /// <summary>The invocation was cancelled.</summary>
    Cancelled,

    /// <summary>Execution failed.</summary>
    Failed,

    /// <summary>The runtime could not establish a safe terminal outcome.</summary>
    Indeterminate,
}
