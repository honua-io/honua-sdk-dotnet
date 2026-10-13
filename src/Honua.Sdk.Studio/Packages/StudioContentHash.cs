// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Security.Cryptography;

namespace Honua.Sdk.Studio.Packages;

/// <summary>
/// Client-side recomputation of a Studio content hash from its canonical input
/// (honua-server#5449). The server returns the exact bytes it hashed as base64
/// <c>contentHashInput</c>; <c>contentHash</c> is the lower-case hexadecimal SHA-256 of those
/// bytes. Recomputing from the input avoids reproducing the server's envelope serialization.
/// </summary>
public static class StudioContentHash
{
    /// <summary>Computes the lower-case hexadecimal SHA-256 of canonical hash-input bytes.</summary>
    /// <param name="input">The decoded canonical hash input.</param>
    /// <returns>The 64-character lower-case hexadecimal digest.</returns>
    public static string Compute(ReadOnlySpan<byte> input)
        => Convert.ToHexStringLower(SHA256.HashData(input));

    /// <summary>
    /// Verifies that <paramref name="contentHash"/> is the lower-case hexadecimal SHA-256 of the
    /// base64-decoded <paramref name="contentHashInput"/>.
    /// </summary>
    /// <param name="contentHashInput">Base64 canonical hash input, or <see langword="null"/> when the server supplied none.</param>
    /// <param name="contentHash">The server-reported lower-case hexadecimal SHA-256 digest.</param>
    /// <returns>
    /// <see langword="null"/> when <paramref name="contentHashInput"/> is absent or empty;
    /// <see langword="true"/> when the recomputed digest equals <paramref name="contentHash"/>
    /// (ordinal comparison); <see langword="false"/> when it differs, the digest is absent, or the
    /// input is not valid base64.
    /// </returns>
    public static bool? Verify(string? contentHashInput, string? contentHash)
    {
        if (string.IsNullOrEmpty(contentHashInput))
        {
            return null;
        }

        if (string.IsNullOrEmpty(contentHash))
        {
            return false;
        }

        byte[] input;
        try
        {
            input = Convert.FromBase64String(contentHashInput);
        }
        catch (FormatException)
        {
            return false;
        }

        return string.Equals(Compute(input), contentHash, StringComparison.Ordinal);
    }
}
