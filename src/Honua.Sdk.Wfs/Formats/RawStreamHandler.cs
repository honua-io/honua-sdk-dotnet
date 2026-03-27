// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Formats;

/// <summary>
/// Returns the raw WFS response stream without parsing.
/// Useful for GML, CSV, or other formats where the caller handles deserialization.
/// </summary>
/// <remarks>
/// The caller is responsible for disposing the returned stream.
/// </remarks>
public sealed class RawStreamHandler : IWfsOutputFormatHandler<Stream>
{
    private readonly string _mediaType;

    /// <summary>
    /// Initializes a new instance with the specified media type.
    /// </summary>
    /// <param name="mediaType">The MIME type to request (default: GML 3.2).</param>
    public RawStreamHandler(string mediaType = "application/gml+xml; version=3.2")
    {
        _mediaType = mediaType;
    }

    /// <inheritdoc />
    public string MediaType => _mediaType;

    /// <inheritdoc />
    public Task<Stream> ReadAsync(Stream responseStream, CancellationToken ct = default)
        => Task.FromResult(responseStream);
}
