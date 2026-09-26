// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Operations;

namespace Honua.Sdk.Studio.Packages;

/// <summary>A completed publication request or an operation awaiting separate approval.</summary>
public sealed class StudioPublicationSubmission
{
    private StudioPublicationSubmission(StudioPublicationRequest? publication, HonuaOperationHandle? operation)
    {
        Publication = publication;
        Operation = operation;
    }

    /// <summary>The persisted publication request, absent while approval is required.</summary>
    public StudioPublicationRequest? Publication { get; }

    /// <summary>The pending operation; its proposal identity is not a publication request identity.</summary>
    public HonuaOperationHandle? Operation { get; }

    /// <summary>Whether a separate authorized actor must approve before publication can proceed.</summary>
    public bool RequiresApproval => Operation is not null;

    /// <summary>Creates an outcome from a persisted publication request.</summary>
    /// <param name="publication">The server publication request.</param>
    public static StudioPublicationSubmission FromPublication(StudioPublicationRequest publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        return new(publication, null);
    }

    /// <summary>Creates an outcome from an operation awaiting approval, without fabricating a publication.</summary>
    /// <param name="operation">The server operation with stable invocation, correlation and proposal identities.</param>
    public static StudioPublicationSubmission AwaitingApproval(HonuaOperationHandle operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Status != HonuaOperationStatus.RequiresApproval
            || string.IsNullOrWhiteSpace(operation.OperationInstanceId)
            || string.IsNullOrWhiteSpace(operation.OperationId)
            || string.IsNullOrWhiteSpace(operation.CorrelationId)
            || string.IsNullOrWhiteSpace(operation.ProposalId))
        {
            throw new ArgumentException("An approval outcome requires a pending operation and its invocation, correlation and proposal identities.", nameof(operation));
        }

        return new(null, operation);
    }
}
