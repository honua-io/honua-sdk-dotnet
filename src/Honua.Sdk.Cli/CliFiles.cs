// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Cli;

internal static class CliFiles
{
    internal const int MaxWorkbookBytes = 8 * 1024 * 1024;

    internal static async Task<byte[]> ReadBoundedAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using (stream.ConfigureAwait(false))
            return await ReadBoundedAsync(stream, maxBytes, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using MemoryStream buffer = new();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new IOException($"The content exceeds {maxBytes / (1024 * 1024)} MiB.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return buffer.ToArray();
    }

    internal static string WriteAtomically(string path, ReadOnlySpan<byte> content)
    {
        string destination = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(destination)
            ?? throw new IOException("The output directory could not be resolved.");
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, overwrite: true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    internal static string DefaultWorkbookName(string formId)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        string safe = new(formId.Select(c => invalid.Contains(c) || c is '.' or '\\' ? '_' : c).ToArray());
        return (safe.Trim('_').Length == 0 ? "form" : safe) + ".xlsx";
    }
}
