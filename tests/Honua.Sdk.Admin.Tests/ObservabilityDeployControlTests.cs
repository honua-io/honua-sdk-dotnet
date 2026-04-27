// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class ObservabilityDeployControlTests
{
    [Fact]
    public async Task GetRecentErrorsAsync_ReadsRawRecentErrorsResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/observability/errors?limit=2", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(new
            {
                capacity = 25,
                instanceId = "node-1",
                errors = new[]
                {
                    new
                    {
                        timestamp = DateTimeOffset.Parse("2026-04-27T10:00:00Z"),
                        correlationId = "corr-1",
                        path = "/api/v1/admin/config",
                        statusCode = 500,
                        message = "boom"
                    }
                }
            }));
        });

        var result = await client.GetRecentErrorsAsync(2);

        var error = Assert.Single(result);
        Assert.Equal("corr-1", error.CorrelationId);
        Assert.Equal("/api/v1/admin/config", error.Path);
        Assert.Equal(500, error.StatusCode);
        Assert.Equal("boom", error.Message);
    }

    [Fact]
    public async Task GetTelemetryStatusAsync_ReadsRawTelemetryResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/observability/telemetry", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(new
            {
                tracingEnabled = true,
                otlpConfigured = true,
                otlpEndpoint = "http://collector:4317"
            }));
        });

        var result = await client.GetTelemetryStatusAsync();

        Assert.True(result.TracingEnabled);
        Assert.True(result.OtlpConfigured);
        Assert.Equal("http://collector:4317", result.OtlpEndpoint);
    }

    [Fact]
    public async Task GetMigrationStatusAsync_ReadsRawMigrationResponse()
    {
        var generatedAt = DateTimeOffset.Parse("2026-04-27T10:00:00Z");
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/observability/migrations", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(new
            {
                status = "succeeded",
                isReady = true,
                isFailed = false,
                message = "ready",
                planAvailable = true,
                upgradeRequired = false,
                pendingScripts = Array.Empty<string>(),
                executedButNotDiscoveredScripts = Array.Empty<string>(),
                planError = (string?)null,
                generatedAt
            }));
        });

        var result = await client.GetMigrationStatusAsync();

        Assert.Equal("succeeded", result.Status);
        Assert.True(result.IsReady);
        Assert.False(result.IsFailed);
        Assert.True(result.PlanAvailable);
        Assert.False(result.UpgradeRequired);
        Assert.Equal(generatedAt, result.GeneratedAt);
    }

    [Fact]
    public async Task GetDeployPreflightAsync_ReadsRawPreflightResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/deploy/preflight", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(new
            {
                status = "ready",
                readyForCoordinatedDeploy = true,
                message = "Instance is ready for coordinated deployment.",
                serverVersion = "0.1.0",
                environment = "Development",
                deploymentMode = "SingleInstance",
                instanceName = "node-1",
                generatedAt = DateTimeOffset.Parse("2026-04-27T10:00:00Z"),
                readiness = new
                {
                    isReady = true,
                    statusCode = 200,
                    message = "ready"
                },
                migration = (object?)null,
                databaseCompatibility = (object?)null
            }));
        });

        var result = await client.GetDeployPreflightAsync();

        Assert.Equal("ready", result.Status);
        Assert.True(result.ReadyForCoordinatedDeploy);
        Assert.Equal("Development", result.Environment);
        Assert.Equal(200, result.Readiness?.StatusCode);
    }

    [Fact]
    public async Task CreateDeployPlanAsync_UsesSingularPlanRouteAndReadsRawResponse()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal("/api/v1/admin/deploy/plan", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return TestHelpers.CreateRawJsonResponse(CreatePlanPayload());
        });

        var result = await client.CreateDeployPlanAsync(new CreateDeployPlanRequest
        {
            TargetId = "service-a",
            DesiredRevision = "rev-2",
            CurrentRevision = "rev-1",
            Parameters = new Dictionary<string, string> { ["slot"] = "blue" }
        });

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("service-a", sent.RootElement.GetProperty("targetId").GetString());
        Assert.Equal("rev-2", sent.RootElement.GetProperty("desiredRevision").GetString());
        Assert.Equal("service-a", result.Target?.TargetId);
        Assert.True(result.ReadyToSubmit);
    }

    [Fact]
    public async Task CreateDeployOperationAsync_SendsRequiredFieldsAndReadsRawResponse()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal("/api/v1/admin/deploy/operations", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return TestHelpers.CreateRawJsonResponse(CreateOperationPayload());
        });

        var result = await client.CreateDeployOperationAsync(new CreateDeployOperationRequest
        {
            TargetId = "service-a",
            DesiredRevision = "rev-2",
            CurrentRevision = "rev-1",
            Reason = "roll forward",
            IdempotencyKey = "idem-1",
            CorrelationId = "corr-1",
            Priority = "Normal",
            SubmitImmediately = false,
            Parameters = new Dictionary<string, string> { ["slot"] = "blue" }
        });

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("service-a", sent.RootElement.GetProperty("targetId").GetString());
        Assert.Equal("rev-2", sent.RootElement.GetProperty("desiredRevision").GetString());
        Assert.False(sent.RootElement.TryGetProperty("planId", out _));
        Assert.Equal("op-1", result.OperationId);
        Assert.Equal("AwaitingApproval", result.Status);
    }

    [Fact]
    public async Task GetDeployOperationAsync_ReadsRawOperationResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/deploy/operations/op-1", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(CreateOperationPayload()));
        });

        var result = await client.GetDeployOperationAsync("op-1");

        Assert.Equal("op-1", result.OperationId);
        Assert.Equal("service-a", result.Target?.TargetId);
    }

    [Theory]
    [InlineData("submit")]
    [InlineData("rollback")]
    public async Task DeployOperationActions_ReadRawOperationResponse(string action)
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal($"/api/v1/admin/deploy/operations/op-1/{action}", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(CreateOperationPayload()));
        });

        var result = action == "submit"
            ? await client.SubmitDeployOperationAsync("op-1")
            : await client.RollbackDeployOperationAsync("op-1");

        Assert.Equal("op-1", result.OperationId);
        Assert.Equal("deploy", result.Kind);
    }

    private static object CreatePlanPayload()
        => new
        {
            target = CreateTargetPayload(),
            readyToSubmit = true,
            requiresApproval = false,
            requiresOutOfBandMigrations = false,
            backendRegistered = true,
            capabilities = new
            {
                supportsRollback = true,
                supportsCancellation = true,
                supportsTrafficShifting = false,
                requiresOutOfBandMigrations = false,
                supportsProgressPolling = true,
                supportsRevisionPinning = true
            },
            warnings = Array.Empty<string>(),
            blockingReasons = Array.Empty<string>(),
            generatedAt = DateTimeOffset.Parse("2026-04-27T10:00:00Z")
        };

    private static object CreateOperationPayload()
        => new
        {
            operationId = "op-1",
            kind = "deploy",
            status = "AwaitingApproval",
            priority = "Normal",
            target = CreateTargetPayload(),
            providerOperationId = (string?)null,
            currentPhase = "planning",
            observedState = "pending",
            errorMessage = (string?)null,
            warnings = Array.Empty<string>(),
            blockingReasons = Array.Empty<string>(),
            requestedBy = "admin",
            reason = "roll forward",
            correlationId = "corr-1",
            createdAt = DateTimeOffset.Parse("2026-04-27T10:00:00Z"),
            updatedAt = DateTimeOffset.Parse("2026-04-27T10:01:00Z"),
            completedAt = (DateTimeOffset?)null
        };

    private static object CreateTargetPayload()
        => new
        {
            targetId = "service-a",
            targetKind = "service",
            backend = "honua",
            environment = "prod",
            targetName = "Service A",
            artifactReference = "registry/service-a:rev-2",
            runtimeProfile = "default",
            currentRevision = "rev-1",
            desiredRevision = "rev-2",
            parameters = new Dictionary<string, string> { ["slot"] = "blue" }
        };
}
