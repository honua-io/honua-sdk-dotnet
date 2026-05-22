// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.OgcFeatures.Wfs.Formats;

/// <summary>
/// Handles deserialization of a WFS GetFeature response in a specific output format.
/// </summary>
/// <remarks>
/// When <see cref="MediaType"/> is an XML-based format (e.g. GML), the client skips probing
/// successful (HTTP 200) responses for OGC ExceptionReport to avoid materializing the entire
/// body. If the server returns a 200-status ExceptionReport instead of the expected format,
/// it will be passed through to <see cref="ReadAsync"/> as-is. Handlers for XML formats
/// should be prepared to handle this edge case.
/// </remarks>
/// <typeparam name="TResult">The deserialized result type.</typeparam>
public interface IWfsOutputFormatHandler<TResult>
{
    /// <summary>
    /// The MIME type to request from the WFS server (used as the OUTPUTFORMAT parameter value).
    /// </summary>
    string MediaType { get; }

    /// <summary>
    /// Indicates whether the handler retains ownership of the HTTP response stream after
    /// <see cref="ReadAsync"/> returns. When <c>true</c>, the client will not dispose the
    /// underlying HTTP response, and the caller is responsible for disposing the returned result.
    /// Default is <c>false</c>.
    /// </summary>
    bool OwnsResponseStream => false;

    /// <summary>
    /// Reads and deserializes the response stream.
    /// </summary>
    /// <param name="responseStream">The HTTP response body stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized result.</returns>
    Task<TResult> ReadAsync(Stream responseStream, CancellationToken cancellationToken = default);
}
