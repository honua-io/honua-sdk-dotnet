// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Processes.Models;

/// <summary>
/// Completed process outputs, preserving document or raw response semantics.
/// </summary>
public sealed class HonuaProcessExecutionResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessExecutionResponse"/> class.
    /// </summary>
    /// <param name="document">Document-mode outputs, or null for raw mode.</param>
    /// <param name="rawContent">Raw-mode bytes, or empty for document mode.</param>
    /// <param name="mediaType">Original response Content-Type.</param>
    /// <param name="job">Successful terminal job, when observed.</param>
    public HonuaProcessExecutionResponse(HonuaProcessResults? document, ReadOnlyMemory<byte> rawContent,
        string? mediaType, HonuaProcessJobStatus? job)
    {
        Document = document;
        RawContent = rawContent;
        MediaType = mediaType;
        Job = job;
    }

    /// <summary>
    /// Document-mode outputs, or null for raw mode.
    /// </summary>
    public HonuaProcessResults? Document { get; }

    /// <summary>
    /// Original raw-mode bytes (including multipart framing), or empty for document mode.
    /// </summary>
    public ReadOnlyMemory<byte> RawContent { get; }

    /// <summary>
    /// Response Content-Type, including parameters such as multipart boundaries.
    /// </summary>
    public string? MediaType { get; }

    /// <summary>
    /// Successful terminal job status when execution returned a job; otherwise null.
    /// </summary>
    public HonuaProcessJobStatus? Job { get; }
}
