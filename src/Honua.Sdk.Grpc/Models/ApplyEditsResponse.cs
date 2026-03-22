// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>
/// Response from applying feature edits.
/// </summary>
public sealed class ApplyEditsResponse
{
    /// <summary>Results for add operations.</summary>
    public IReadOnlyList<EditResult> AddResults { get; init; } = [];

    /// <summary>Results for update operations.</summary>
    public IReadOnlyList<EditResult> UpdateResults { get; init; } = [];

    /// <summary>Results for delete operations.</summary>
    public IReadOnlyList<EditResult> DeleteResults { get; init; } = [];

    /// <summary>Top-level error, if the entire batch failed.</summary>
    public EditError? Error { get; init; }
}

/// <summary>
/// The outcome of a single create, update, or delete operation.
/// </summary>
public sealed class EditResult
{
    /// <summary>Object ID of the affected feature.</summary>
    public long ObjectId { get; init; }

    /// <summary>Whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Error details if the operation failed.</summary>
    public EditError? Error { get; init; }
}

/// <summary>
/// Provides details for a failed edit operation.
/// </summary>
public sealed class EditError
{
    /// <summary>Error code.</summary>
    public int Code { get; init; }

    /// <summary>Error message.</summary>
    public string Message { get; init; } = string.Empty;
}
