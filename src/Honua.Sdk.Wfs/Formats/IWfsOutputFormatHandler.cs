// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Formats;

/// <summary>
/// Handles deserialization of a WFS GetFeature response in a specific output format.
/// </summary>
/// <typeparam name="TResult">The deserialized result type.</typeparam>
public interface IWfsOutputFormatHandler<TResult>
{
    /// <summary>
    /// The MIME type to request from the WFS server (used as the OUTPUTFORMAT parameter value).
    /// </summary>
    string MediaType { get; }

    /// <summary>
    /// Reads and deserializes the response stream.
    /// </summary>
    /// <param name="responseStream">The HTTP response body stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deserialized result.</returns>
    Task<TResult> ReadAsync(Stream responseStream, CancellationToken ct = default);
}
