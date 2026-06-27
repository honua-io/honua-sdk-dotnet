// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Http;

/// <summary>
/// Stream wrapper that leaves the underlying stream open when disposed. Use when handing a
/// caller-owned stream to an <see cref="HttpContent"/> that would otherwise dispose it.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Usage",
    "CA2213:Disposable fields should be disposed",
    Justification = "The wrapped stream is caller-owned and must remain open after upload.")]
public sealed class NonDisposingStream : Stream
{
    private readonly Stream _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="NonDisposingStream"/> class.
    /// </summary>
    /// <param name="inner">The caller-owned stream to wrap; it is not disposed by this wrapper.</param>
    public NonDisposingStream(Stream inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _inner.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => _inner.CanWrite;

    /// <inheritdoc />
    public override long Length => _inner.Length;

    /// <inheritdoc />
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    /// <inheritdoc />
    public override void Flush() => _inner.Flush();

    /// <inheritdoc />
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _inner.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    /// <inheritdoc />
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.WriteAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.WriteAsync(buffer, cancellationToken);
}
