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

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var page = await ReadSuccessAsync(response, WorkTaskJsonContext.Default.WorkTaskPage, cancellationToken).ConfigureAwait(false);
        if (page.Items is null || page.Items.Any(task => task is null || task.ServiceId != serviceId || task.Assignee != assignee))
        {
            throw InvalidResponse(response, "The task listing contains items outside the requested service or assignee.");
        }

        return page;
    }

    /// <summary>Creates an asset-linked request with stable client-generated task and command identifiers.</summary>
    public async Task<WorkTaskReceipt> CreateAssetRequestAsync(WorkTaskAssetRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(request.TaskId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(request.CommandId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(request.ProfileId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ServiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AssetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

        var body = new WorkTaskAssetRequestBody(request.CommandId, request.TaskId, request.ServiceId.Trim(),
            request.ProfileId, request.AssetId.Trim(), request.Title.Trim(), "request");
        return await PostAsync((body.TaskId, body.CommandId, TaskRoute),
            JsonContent.Create(body, WorkTaskJsonContext.Default.WorkTaskAssetRequestBody),
            cancellationToken, body).ConfigureAwait(false);
    }

    /// <summary>Submits a versioned transition with a caller-generated command identifier.</summary>
    public Task<WorkTaskReceipt> TransitionAsync(Guid taskId, WorkTaskTransitionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Action);
        ValidateCommand(taskId, request.CommandId, request.ExpectedVersion);
        return PostAsync(
            (taskId, request.CommandId, $"{TaskRoute}/{taskId:D}/transitions"),
            JsonContent.Create(request, WorkTaskJsonContext.Default.WorkTaskTransitionRequest), cancellationToken);
    }

    /// <summary>Submits a versioned completion with a caller-generated command identifier.</summary>
    public Task<WorkTaskReceipt> CompleteAsync(Guid taskId, WorkTaskCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommand(taskId, request.CommandId, request.ExpectedVersion);
        return PostAsync(
            (taskId, request.CommandId, $"{TaskRoute}/{taskId:D}/completion"),
            JsonContent.Create(request, WorkTaskJsonContext.Default.WorkTaskCompletionRequest), cancellationToken);
    }

    private async Task<WorkTaskReceipt> PostAsync(
        (Guid TaskId, Guid CommandId, string Route) command, JsonContent content, CancellationToken cancellationToken,
        WorkTaskAssetRequestBody? assetRequest = null)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, new Uri(command.Route, UriKind.Relative))
        {
            Content = content,
        };
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Conflict
            && response.Headers.TryGetValues("Honua-Command-Outcome", out var outcomes)
            && outcomes.Contains("conflict", StringComparer.Ordinal))
        {
            var conflict = await ReadBodyAsync(response, WorkTaskJsonContext.Default.WorkTaskConflict, cancellationToken).ConfigureAwait(false);
            if (conflict.CommandId != command.CommandId || string.IsNullOrWhiteSpace(conflict.Reason))
            {
                throw InvalidResponse(response, "The task conflict does not identify the submitted command and reason.");
            }

            throw new WorkTaskVersionConflictException(conflict.CommandId, conflict.Reason, conflict.CurrentVersion);
        }

        var receipt = await ReadSuccessAsync(response, WorkTaskJsonContext.Default.WorkTaskReceipt, cancellationToken).ConfigureAwait(false);
        if (receipt.CommandId != command.CommandId || receipt.Task?.TaskId != command.TaskId
            || receipt.Outcome is not ("accepted" or "duplicate"))
        {
            throw InvalidResponse(response, "The task receipt does not confirm acceptance of the submitted command.");
        }

        if (assetRequest is not null
            && (receipt.Task.ServiceId != assetRequest.ServiceId || receipt.Task.ProfileId != assetRequest.ProfileId
                || !MatchesAssetId(assetRequest.AssetId, receipt.Task.AssetId) || receipt.Task.Type != assetRequest.Type))
        {
            throw InvalidResponse(response, "The task receipt does not match the asset-linked request.");
        }

        return receipt;
    }

    private static bool MatchesAssetId(string requested, string? returned)
    {
        // The Server preserves text IDs and canonicalizes integer and UUID identity fields.
        return requested == returned
            || (long.TryParse(requested, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                && number.ToString(CultureInfo.InvariantCulture) == returned)
            || (Guid.TryParse(requested, out var id) && id.ToString("D") == returned);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new WorkTaskApiException("The task request failed.", (int?)ex.StatusCode, ex);
        }
    }

    private static Task<T> ReadSuccessAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new WorkTaskApiException("The task response could not be read.", (int)response.StatusCode, ex);
        }

        return ReadBodyAsync(response, typeInfo, cancellationToken);
    }

    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("The server returned a null task response.");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or InvalidOperationException)
        {
            throw new WorkTaskApiException("The task response could not be read.", (int)response.StatusCode, ex);
        }
    }

    private static WorkTaskApiException InvalidResponse(HttpResponseMessage response, string message)
        => new(message, (int)response.StatusCode, new JsonException(message));

    private static void ValidateCommand(Guid taskId, Guid commandId, long expectedVersion)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(taskId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(commandId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedVersion);
    }
}

/// <summary>The task request failed or the server returned an invalid task response.</summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors",
    Justification = "The protocol failure requires the underlying error and available HTTP status.")]
public sealed class WorkTaskApiException : HonuaException
{
    internal WorkTaskApiException(string message, int? httpStatus, Exception innerException) : base(message, innerException)
    {
        HttpStatus = httpStatus;
    }

    /// <summary>HTTP status associated with the failure, when available.</summary>
    public override int? HttpStatus { get; }
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

internal sealed record WorkTaskAssetRequestBody(
    Guid CommandId, Guid TaskId, string ServiceId, Guid ProfileId, string AssetId, string Title, string Type);

internal sealed record WorkTaskConflict(Guid CommandId, string Reason, long? CurrentVersion);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WorkTaskPage))]
[JsonSerializable(typeof(WorkTaskReceipt))]
[JsonSerializable(typeof(WorkTaskTransitionRequest))]
[JsonSerializable(typeof(WorkTaskCompletionRequest))]
[JsonSerializable(typeof(WorkTaskConflict))]
[JsonSerializable(typeof(WorkTaskAssetRequestBody))]
internal sealed partial class WorkTaskJsonContext : JsonSerializerContext;
