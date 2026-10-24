// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Honua.Sdk.Processes.Exceptions;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes;

public sealed partial class HonuaProcessesClient
{
    /// <inheritdoc />
    public Task<HonuaProcessExecutionResponse> ExecuteSyncAsync(string processId,
        IReadOnlyDictionary<string, JsonElement> inputs, TimeSpan timeout,
        TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        return ExecuteSyncAsync(processId, new HonuaProcessExecuteRequest
        {
            Inputs = HonuaProcessExecuteInputs.FromDirectInputs(inputs)
        }, timeout, pollingInterval, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HonuaProcessExecutionResponse> ExecuteSyncAsync(string processId,
        HonuaProcessExecuteRequest request, TimeSpan timeout,
        TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
    {
        EnsureId(processId, nameof(processId));
        ArgumentNullException.ThrowIfNull(request);
        if (request.Response is not ("document" or "raw"))
        {
            throw new ArgumentException("Response must be document or raw.", nameof(request));
        }

        var interval = ValidateWait(timeout, pollingInterval);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        string? jobId = null;
        try
        {
            var description = await GetProcessAsync(processId, deadline.Token).ConfigureAwait(false);
            var supportsSync = description.JobControlOptions?.Contains("sync-execute", StringComparer.Ordinal) == true;
            var supportsAsync = description.JobControlOptions?.Contains("async-execute", StringComparer.Ordinal) == true;
            if (!supportsSync && !supportsAsync)
            {
                throw new HonuaProcessesException(HttpStatusCode.BadGateway,
                    "Process description does not advertise a supported execution mode (sync-execute or async-execute).");
            }

            using var message = new HttpRequestMessage(HttpMethod.Post,
                CreateRequestUri($"{BasePath}/processes/{Uri.EscapeDataString(processId)}/execution"))
            {
                Content = JsonContent.Create(request, ProcessesJsonContext.Default.HonuaProcessExecuteRequest)
            };
            if (!supportsSync)
            {
                message.Headers.TryAddWithoutValidation("Prefer", "respond-async");
            }

            using var response = await _http.SendAsync(message, deadline.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return await ReadExecutionResultAsync(response, request.Response, null, deadline.Token).ConfigureAwait(false);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw CreateException(response.StatusCode,
                    await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false));
            }

            if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.Accepted))
            {
                throw new HonuaProcessesException(response.StatusCode, "Unexpected process execution response status.");
            }

            var submitted = await ReadAsync(response, ProcessesJsonContext.Default.HonuaProcessJobStatus, deadline.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(submitted.JobId))
            {
                throw new HonuaProcessesException(response.StatusCode, "Process execution returned a job without a jobID.");
            }

            jobId = submitted.JobId;
            var terminal = await WaitForTerminalCoreAsync(jobId, submitted, interval, deadline.Token).ConfigureAwait(false);
            using var resultsResponse = await _http.GetAsync(
                CreateRequestUri($"{BasePath}/jobs/{Uri.EscapeDataString(jobId)}/results"), deadline.Token).ConfigureAwait(false);
            return await ReadExecutionResultAsync(resultsResponse, request.Response, terminal, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw CreateWaitTimeout(jobId, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && ex.InnerException is TimeoutException)
        {
            throw CreateRequestTimeout(jobId, ex);
        }
        catch (HonuaProcessesException ex)
        {
            ex.JobId ??= jobId;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessJobStatus> WaitForTerminalAsync(string jobId, TimeSpan timeout,
        TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
    {
        EnsureId(jobId, nameof(jobId));
        var interval = ValidateWait(timeout, pollingInterval);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            return await WaitForTerminalCoreAsync(jobId, null, interval, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw CreateWaitTimeout(jobId, ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && ex.InnerException is TimeoutException)
        {
            throw CreateRequestTimeout(jobId, ex);
        }
        catch (HonuaProcessesException ex) when (ex.JobId is null)
        {
            ex.JobId = jobId;
            throw;
        }
    }

    private async Task<HonuaProcessJobStatus> WaitForTerminalCoreAsync(string jobId,
        HonuaProcessJobStatus? initial, TimeSpan interval, CancellationToken cancellationToken)
    {
        var status = initial ?? await GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(status.JobId, jobId, StringComparison.Ordinal))
            {
                throw new HonuaProcessesException(HttpStatusCode.BadGateway, "Job status returned a missing or mismatched jobID.") { JobId = jobId };
            }

            switch (status.Status)
            {
                case "successful":
                    return status;
                case "failed":
                case "dismissed":
                    throw new HonuaProcessesException(HttpStatusCode.UnprocessableEntity,
                        $"Process job '{jobId}' {status.Status}: {status.Message}") { JobId = jobId };
                case "accepted":
                case "running":
                    break;
                default:
                    throw new HonuaProcessesException(HttpStatusCode.BadGateway,
                        $"Process job '{jobId}' returned an unknown status '{status.Status}'.") { JobId = jobId };
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            status = await GetJobAsync(jobId, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<HonuaProcessExecutionResponse> ReadExecutionResultAsync(HttpResponseMessage response,
        string mode, HonuaProcessJobStatus? job, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw CreateException(response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new HonuaProcessesException(response.StatusCode, "Unexpected process results response status.") { JobId = job?.JobId };
        }

        var mediaType = response.Content.Headers.ContentType?.ToString();
        if (mode == "document")
        {
            var document = await ReadAsync(response, ProcessesJsonContext.Default.HonuaProcessResults, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new HonuaProcessExecutionResponse(document, default, mediaType, job);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new HonuaProcessExecutionResponse(null, bytes, mediaType, job);
    }

    private static TimeSpan ValidateWait(TimeSpan timeout, TimeSpan? pollingInterval)
    {
        // CancellationTokenSource and Task.Delay require a finite duration below UInt32.MaxValue milliseconds.
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive and finite, at most 4294967294 milliseconds.");
        }

        var interval = pollingInterval ?? TimeSpan.FromSeconds(1);
        if (interval <= TimeSpan.Zero || interval.TotalMilliseconds > uint.MaxValue - 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pollingInterval), "Polling interval must be positive and finite, at most 4294967294 milliseconds.");
        }

        return interval;
    }

    private static HonuaProcessesException CreateWaitTimeout(string? jobId, OperationCanceledException innerException)
        => new(HttpStatusCode.RequestTimeout,
            "Process execution exceeded the elapsed timeout. The server job may still be running.", null, innerException) { JobId = jobId };

    private static HonuaProcessesException CreateRequestTimeout(string? jobId, OperationCanceledException innerException)
        => new(HttpStatusCode.RequestTimeout,
            "A process HTTP request timed out. The server job may still be running.", null, innerException) { JobId = jobId };
}
