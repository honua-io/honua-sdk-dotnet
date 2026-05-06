// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Reads a vector payload into typed feature records and NetTopologySuite geometries.
/// </summary>
public interface IVectorPayloadReader
{
    /// <summary>The vector format handled by this reader.</summary>
    VectorPayloadFormat Format { get; }

    /// <summary>
    /// Reads a vector payload stream.
    /// </summary>
    /// <param name="stream">Payload stream.</param>
    /// <param name="options">Optional reader settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The typed feature collection.</returns>
    Task<VectorPayloadFeatureSet> ReadAsync(
        Stream stream,
        VectorPayloadReadOptions? options = null,
        CancellationToken ct = default);
}
