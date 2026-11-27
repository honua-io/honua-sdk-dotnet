// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Abstractions;

namespace Honua.Sdk.Field.Tasks;

/// <summary>Reads explicitly assigned work tasks and submits versioned worker commands.</summary>
public sealed class WorkTaskClient
{
    private const string TaskRoute = "api/v1/tasks";
    private readonly HttpClient _httpClient;

    /// <summary>Uses a caller-configured authenticated Honua Server HTTP client.</summary>
    public WorkTaskClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>Reads a page for an explicit service and assignee handle, verified against the returned records.</summary>
    public async Task<WorkTaskPage> ListAssignedAsync(AssignedWorkTaskQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.ServiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Assignee);
        var serviceId = query.ServiceId.Trim();
        var assignee = query.Assignee.Trim();
        var url = $"{TaskRoute}?serviceId={Uri.EscapeDataString(serviceId)}&assignee={Uri.EscapeDataString(assignee)}";
        if (query.Cursor is { Length: > 0 } cursor)
        {
            url += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        if (query.Limit is { } limit)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
            url += $"&limit={limit.ToString(CultureInfo.InvariantCulture)}";
        }

        using var response = await _httpClient.GetAsync(new Uri(url, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        var page = await ReadAsync(response, WorkTaskJsonContext.Default.WorkTaskPage, cancellationToken).ConfigureAwait(false);
        if (page.Items is null || page.Items.Any(task => task is null || task.ServiceId != serviceId || task.Assignee != assignee))
        {
            throw new JsonException("The task listing contains items outside the requested service or assignee.");
        }

        return page;
    }

    /// <summary>Submits a versioned transition with a caller-generated command identifier.</summary>
    public Task<WorkTaskReceipt> TransitionAsync(Guid taskId, WorkTaskTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);
        ValidateCommand(taskId, request.CommandId, request.ExpectedVersion);
        return PostAsync(
            (taskId, request.CommandId, "transitions"),
            JsonContent.Create(request, WorkTaskJsonContext.Default.WorkTaskTransitionRequest), cancellationToken);
    }

    /// <summary>Submits a versioned completion with a caller-generated command identifier.</summary>
    public Task<WorkTaskReceipt> CompleteAsync(Guid taskId, WorkTaskCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommand(taskId, request.CommandId, request.ExpectedVersion);
        return PostAsync(
            (taskId, request.CommandId, "completion"),
            JsonContent.Create(request, WorkTaskJsonContext.Default.WorkTaskCompletionRequest), cancellationToken);
    }

    private async Task<WorkTaskReceipt> PostAsync(
        (Guid TaskId, Guid CommandId, string Operation) command, JsonContent content, CancellationToken cancellationToken)
    {
        using (content)
        using (var response = await _httpClient.PostAsync(
            new Uri($"{TaskRoute}/{command.TaskId:D}/{command.Operation}", UriKind.Relative), content, cancellationToken).ConfigureAwait(false))
        {
            if (response.StatusCode == HttpStatusCode.Conflict
                && response.Headers.TryGetValues("Honua-Command-Outcome", out var outcomes)
                && outcomes.Contains("conflict", StringComparer.Ordinal))
            {
                var conflict = await response.Content.ReadFromJsonAsync(
                    WorkTaskJsonContext.Default.WorkTaskConflict, cancellationToken).ConfigureAwait(false);
                if (conflict is not null && conflict.CommandId == command.CommandId)
                {
                    throw new WorkTaskVersionConflictException(conflict.CommandId, conflict.Reason, conflict.CurrentVersion);
                }
            }

            var receipt = await ReadAsync(response, WorkTaskJsonContext.Default.WorkTaskReceipt, cancellationToken).ConfigureAwait(false);
            if (receipt.CommandId != command.CommandId || receipt.Task?.TaskId != command.TaskId
                || receipt.Outcome is not ("accepted" or "duplicate"))
            {
                throw new JsonException("The task receipt does not confirm acceptance of the submitted command.");
            }

            return receipt;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The server returned a null task response.");
    }

    private static void ValidateCommand(Guid taskId, Guid commandId, long expectedVersion)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(taskId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(commandId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedVersion);
    }
}

/// <summary>The server rejected a task command with a version conflict.</summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "A version conflict requires its server command identifier and version context.")]
public sealed class WorkTaskVersionConflictException : HonuaException
{
    internal WorkTaskVersionConflictException(Guid commandId, string reason, long? currentVersion) : base(reason)
    {
        CommandId = commandId;
        CurrentVersion = currentVersion;
    }

    /// <summary>Identifier of the rejected command.</summary>
    public Guid CommandId { get; }

    /// <summary>Current server task version, when the server supplies it.</summary>
    public long? CurrentVersion { get; }

    /// <summary>HTTP status associated with the conflict.</summary>
    public override int? HttpStatus => (int)HttpStatusCode.Conflict;
}

internal sealed record WorkTaskConflict(Guid CommandId, string Reason, long? CurrentVersion);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkTaskPage))]
[JsonSerializable(typeof(WorkTaskReceipt))]
[JsonSerializable(typeof(WorkTaskTransitionRequest))]
[JsonSerializable(typeof(WorkTaskCompletionRequest))]
[JsonSerializable(typeof(WorkTaskConflict))]
internal sealed partial class WorkTaskJsonContext : JsonSerializerContext;
