// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Deploy-control workflow: preflight readiness checks, deploy plans, and
/// deploy operations (submit and rollback).
/// </summary>
public interface IHonuaAdminDeployClient
{
    /// <summary>
    /// Runs preflight checks to determine deployment readiness.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preflight check result.</returns>
    Task<DeployPreflightResult> GetDeployPreflightAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs preflight checks to determine deployment readiness.
    /// </summary>
    /// <param name="includeDiagnostics">Whether to include diagnostic detail in the response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preflight check result.</returns>
    Task<DeployPreflightResult> GetDeployPreflightAsync(bool includeDiagnostics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new deploy plan.
    /// </summary>
    /// <param name="request">The deploy plan creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created deploy plan.</returns>
    Task<DeployPlan> CreateDeployPlanAsync(CreateDeployPlanRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new deploy operation from a plan.
    /// </summary>
    /// <param name="request">The deploy operation creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created deploy operation.</returns>
    Task<DeployOperation> CreateDeployOperationAsync(CreateDeployOperationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a deploy operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deploy operation.</returns>
    Task<DeployOperation> GetDeployOperationAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a deploy operation for execution.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> SubmitDeployOperationAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a deploy operation for execution.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="request">The submit request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> SubmitDeployOperationAsync(string operationId, SubmitDeployOperationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a deploy operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> RollbackDeployOperationAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back a deploy operation.
    /// </summary>
    /// <param name="operationId">The operation identifier.</param>
    /// <param name="request">The rollback request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated deploy operation.</returns>
    Task<DeployOperation> RollbackDeployOperationAsync(string operationId, RollbackDeployOperationRequest request, CancellationToken cancellationToken = default);
}
