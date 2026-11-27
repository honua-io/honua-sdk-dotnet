// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;

namespace Honua.Sdk.Abstractions.Http;

internal sealed record GeoServicesEnvelope(int Code, bool? Retryable, TimeSpan? RetryAfter);

internal static class HonuaGeoServicesEnvelopeInspector
{
    private const int PrefixProbeBytes = 128;
    private const int MaxEnvelopeBytes = 64 * 1024;
    private static readonly byte[] ErrorPropertyBytes = "\"error\""u8.ToArray();

    public static bool IsJsonResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }

    public static async ValueTask<GeoServicesEnvelope?> TryInspectAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return null;
        }

        if (response.Content.Headers.ContentLength is { } declared && declared > MaxEnvelopeBytes)
        {
            return null;
        }

        var originalContent = response.Content;
        var stream = await originalContent.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var probe = await ReadUpToAsync(stream, PrefixProbeBytes, cancellationToken).ConfigureAwait(false);
        if (!LooksLikeErrorEnvelopePrefix(probe.Bytes))
        {
            response.Content = CreateReplayableContent(originalContent, probe, stream);
            return null;
        }

        return await ReadAndParseEnvelopeAsync(
            response,
            new StreamProbe(probe, stream),
            cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<GeoServicesEnvelope?> ReadAndParseEnvelopeAsync(
        HttpResponseMessage response,
        StreamProbe probe,
        CancellationToken cancellationToken)
    {
        var originalContent = response.Content;
        if (probe.Chunk.Status == StreamReadStatus.EndOfStream)
        {
            await probe.Remainder.DisposeAsync().ConfigureAwait(false);
            response.Content = CreateBufferedContent(originalContent, probe.Chunk.Bytes);
            return ParseEnvelope(probe.Chunk.Bytes);
        }

        var remaining = await ReadUpToAsync(
            probe.Remainder,
            MaxEnvelopeBytes - probe.Chunk.Bytes.Length + 1,
            cancellationToken).ConfigureAwait(false);
        var combined = Combine(probe.Chunk.Bytes, remaining.Bytes);
        if (remaining.Status != StreamReadStatus.EndOfStream || combined.Length > MaxEnvelopeBytes)
        {
            var chunk = new StreamReadChunk(combined, StreamReadStatus.Incomplete);
            response.Content = CreateReplayableContent(originalContent, chunk, probe.Remainder);
            return null;
        }

        await probe.Remainder.DisposeAsync().ConfigureAwait(false);
        response.Content = CreateBufferedContent(originalContent, combined);
        return ParseEnvelope(combined);
    }

    private static async ValueTask<StreamReadChunk> ReadUpToAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes];
        var total = 0;
        while (total < maxBytes)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, maxBytes - total), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                return new StreamReadChunk(buffer[..total], StreamReadStatus.EndOfStream);
            }

            total += read;
        }

        return new StreamReadChunk(buffer, StreamReadStatus.Incomplete);
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var combined = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, combined, 0, first.Length);
        Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
        return combined;
    }

    private static bool LooksLikeErrorEnvelopePrefix(ReadOnlySpan<byte> bytes)
    {
        var index = SkipBomAndWhitespace(bytes, 0);
        if (index >= bytes.Length || bytes[index] != (byte)'{')
        {
            return false;
        }

        index = SkipWhitespace(bytes, index + 1);
        var remaining = bytes[index..];
        return remaining.Length >= ErrorPropertyBytes.Length
            && remaining[..ErrorPropertyBytes.Length].SequenceEqual(ErrorPropertyBytes);
    }

    private static int SkipBomAndWhitespace(ReadOnlySpan<byte> bytes, int index)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            index = 3;
        }

        return SkipWhitespace(bytes, index);
    }

    private static int SkipWhitespace(ReadOnlySpan<byte> bytes, int index)
    {
        while (index < bytes.Length && bytes[index] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            index++;
        }

        return index;
    }

    private static GeoServicesEnvelope? ParseEnvelope(ReadOnlyMemory<byte> utf8Bytes)
    {
        if (utf8Bytes.IsEmpty)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(utf8Bytes);
            var error = TryGetErrorObject(document.RootElement);
            if (error is null)
            {
                return null;
            }

            var code = TryGetInt32(error.Value, "code");
            if (code is null)
            {
                return null;
            }

            return new GeoServicesEnvelope(
                code.Value,
                TryGetBoolean(error.Value, "retryable"),
                TryGetRetryAfterSeconds(error.Value));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonElement? TryGetErrorObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return error;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || !prop.TryGetInt32(out var parsed))
        {
            return null;
        }

        return parsed;
    }

    private static bool? TryGetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static TimeSpan? TryGetRetryAfterSeconds(JsonElement error)
    {
        if (!error.TryGetProperty("retryAfterSeconds", out var prop) ||
            prop.ValueKind != JsonValueKind.Number ||
            !prop.TryGetDouble(out var seconds) ||
            !double.IsFinite(seconds) ||
            seconds < 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static HttpContent CreateReplayableContent(
        HttpContent original,
        StreamReadChunk chunk,
        Stream remainder)
    {
        if (chunk.Status == StreamReadStatus.EndOfStream)
        {
            remainder.Dispose();
            return CreateBufferedContent(original, chunk.Bytes);
        }

        var replacement = new StreamContent(new PrefixedStream(chunk.Bytes, remainder, original));
        CopyHeaders(original, replacement);
        return replacement;
    }

    private static ByteArrayContent CreateBufferedContent(HttpContent original, byte[] bytes)
    {
        var replacement = new ByteArrayContent(bytes);
        CopyHeaders(original, replacement);
        original.Dispose();
        return replacement;
    }

    private static void CopyHeaders(HttpContent source, HttpContent destination)
    {
        foreach (var header in source.Headers)
        {
            destination.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private enum StreamReadStatus
    {
        Incomplete,
        EndOfStream
    }

    private readonly record struct StreamReadChunk(byte[] Bytes, StreamReadStatus Status);
    private readonly record struct StreamProbe(StreamReadChunk Chunk, Stream Remainder);

    private sealed class PrefixedStream(byte[] prefix, Stream remainder, HttpContent owner) : Stream
    {
        private int _prefixOffset;
        private bool _closed;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => remainder.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_prefixOffset < prefix.Length)
            {
                var toCopy = Math.Min(count, prefix.Length - _prefixOffset);
                Buffer.BlockCopy(prefix, _prefixOffset, buffer, offset, toCopy);
                _prefixOffset += toCopy;
                return toCopy;
            }

            return remainder.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_prefixOffset < prefix.Length)
            {
                var toCopy = Math.Min(buffer.Length, prefix.Length - _prefixOffset);
                prefix.AsSpan(_prefixOffset, toCopy).CopyTo(buffer.Span);
                _prefixOffset += toCopy;
                return ValueTask.FromResult(toCopy);
            }

            return remainder.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Close()
        {
            if (!_closed)
            {
                _closed = true;
                remainder.Dispose();
                owner.Dispose();
            }

            base.Close();
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_closed)
            {
                _closed = true;
                await remainder.DisposeAsync().ConfigureAwait(false);
                owner.Dispose();
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
