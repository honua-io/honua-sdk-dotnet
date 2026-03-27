// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Exceptions;

/// <summary>
/// Parsed OGC ExceptionReport from a WFS error response.
/// </summary>
public sealed class WfsExceptionReport
{
    /// <summary>OGC exception code (e.g. InvalidParameterValue, OperationNotSupported).</summary>
    public string ExceptionCode { get; init; } = "NoApplicableCode";

    /// <summary>Human-readable exception text, if provided.</summary>
    public string? ExceptionText { get; init; }

    /// <summary>Parameter or element that caused the error, if provided.</summary>
    public string? Locator { get; init; }
}
