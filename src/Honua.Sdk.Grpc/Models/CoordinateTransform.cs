// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>Scale and translation parameters for decoding quantized delta-encoded coordinates.</summary>
public sealed record CoordinateTransform
{
    /// <summary>Scale factor for X ordinates.</summary>
    public double ScaleX { get; init; }

    /// <summary>Scale factor for Y ordinates.</summary>
    public double ScaleY { get; init; }

    /// <summary>Optional scale factor for Z ordinates.</summary>
    public double? ScaleZ { get; init; }

    /// <summary>Optional scale factor for M ordinates.</summary>
    public double? ScaleM { get; init; }

    /// <summary>Translation offset for X ordinates.</summary>
    public double TranslateX { get; init; }

    /// <summary>Translation offset for Y ordinates.</summary>
    public double TranslateY { get; init; }

    /// <summary>Optional translation offset for Z ordinates.</summary>
    public double? TranslateZ { get; init; }

    /// <summary>Optional translation offset for M ordinates.</summary>
    public double? TranslateM { get; init; }
}
