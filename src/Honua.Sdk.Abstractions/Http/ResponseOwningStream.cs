// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Http;

/// <summary>
/// Stream wrapper that disposes the underlying <see cref="HttpResponseMessage"/> when
/// the stream is disposed. Used with <see cref="HttpCompletionOption.ResponseHeadersRead"/>
/// to ensure the HTTP response lifecycle is tied to the returned stream.
/// </summary>
public sealed class ResponseOwningStream : Stream
{
    private readonly Stream _inner;
    private readonly HttpResponseMessage _response;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseOwningStream"/> class.
    /// </summary>
    /// <param name="inner">The response content stream to read from.</param>
    /// <param name="response">The HTTP response whose lifetime is bound to this stream.</param>
    public ResponseOwningStream(Stream inner, HttpResponseMessage response)
    {
        _inner = inner;
        _response = response;
    }

    /// <inheritdoc />
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc />
    public override bool CanSeek => _inner.CanSeek;

    /// <inheritdoc />
    public override bool CanWrite => false;

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
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    /// <inheritdoc />
    public override void SetLength(long value) => _inner.SetLength(value);

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
