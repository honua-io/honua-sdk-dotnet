// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Security.Cryptography;

namespace Honua.Sdk.Studio.Content;

/// <summary>Recomputes Studio content hashes from the canonical bytes supplied by the server.</summary>
public static class StudioContentHash
{
    /// <summary>Computes the lower-case hexadecimal SHA-256 of canonical hash-input bytes.</summary>
    public static string Compute(ReadOnlySpan<byte> input)
        => Convert.ToHexStringLower(SHA256.HashData(input));

    /// <summary>Verifies that <paramref name="contentHash"/> is the lower-case hexadecimal SHA-256 of the base64-decoded <paramref name="contentHashInput"/>.</summary>
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
