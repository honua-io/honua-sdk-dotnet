// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Honua.Sdk.OgcFeatures.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.OgcFeatures.Tests;

/// <summary>
/// Regression tests for issue #414 (DN-001). With the default resilience pipeline
/// enabled, <see cref="HttpClient.Timeout"/> is infinite and the standard attempt
/// timeout ends when response headers arrive. A stalled body — buffered by
/// <see cref="HttpCompletionOption.ResponseContentRead"/> or streamed after
/// <see cref="HttpCompletionOption.ResponseHeadersRead"/> — must still be cancelled
/// by the per-attempt budget (<c>HonuaResilienceTimeouts.AttemptTimeout</c>, 45% of
/// <c>Timeout</c>; 45 seconds when <c>Timeout</c> keeps its 100 second default).
/// </summary>
public sealed class ResponseBodyTimeoutTests
{
    // One second overall budget => ~450 ms per attempt. The hang bound is several
    // times the overall budget, so a body with no deadline fails the test instead
    // of waiting forever.
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HangBound = TimeSpan.FromSeconds(3);

    [Fact]
    public async Task DefaultResilience_ReadsShortResponseBody()
    {
        using var provider = BuildProvider(new ShortBodyHandler("{}", "application/json"));
        var http = GetHttpClient(provider);

        using var response = await http.GetAsync(new Uri("https://example.test/collections"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DefaultResilience_StalledBufferedBody_CompletesWithinAttemptTimeout()
    {
        using var provider = BuildProvider(new StalledBodyHandler());
        var http = GetHttpClient(provider);
        var started = Stopwatch.StartNew();

        var send = http.GetAsync(new Uri("https://example.test/collections"));
        var completed = await Task.WhenAny(send, Task.Delay(HangBound));

        Assert.Same(send, completed);
        var exception = await Record.ExceptionAsync(() => send);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.InRange(started.Elapsed, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DefaultResilience_StalledStreamingBody_CompletesWithinAttemptTimeout()
    {
        using var provider = BuildProvider(new StalledBodyHandler());
        var http = GetHttpClient(provider);
        var started = Stopwatch.StartNew();

        using var response = await http.GetAsync(
            new Uri("https://example.test/collections"),
            HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        var read = stream.ReadAsync(new byte[16]).AsTask();
        var completed = await Task.WhenAny(read, Task.Delay(HangBound));

        Assert.Same(read, completed);
        var exception = await Record.ExceptionAsync(() => read);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.InRange(started.Elapsed, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DefaultResilience_StalledStreamCreation_CompletesWithinAttemptTimeout()
    {
        using var provider = BuildProvider(new StalledStreamCreationHandler());
        var http = GetHttpClient(provider);
        var started = Stopwatch.StartNew();

        using var response = await http.GetAsync(
            new Uri("https://example.test/collections"),
            HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var open = response.Content.ReadAsStreamAsync();
        var completed = await Task.WhenAny(open, Task.Delay(HangBound));

        Assert.Same(open, completed);
        var exception = await Record.ExceptionAsync(() => open);
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.InRange(started.Elapsed, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2));
    }

    private static ServiceProvider BuildProvider(HttpMessageHandler handler)
    {
        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = new Uri("https://example.test");
            // EnableRetry and the resilience pipeline stay at their defaults.
            // Timeout is shortened so a missing body deadline fails in-process.
            options.Timeout = Budget;
            options.PrimaryHttpMessageHandlerFactory = () => handler;
        });
        return services.BuildServiceProvider();
    }

    private static HttpClient GetHttpClient(ServiceProvider provider)
    {
        var client = provider.GetRequiredService<HonuaOgcFeaturesClient>();
        var field = typeof(HonuaOgcFeaturesClient).GetField(
            "_http",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<HttpClient>(field.GetValue(client));
    }

    private sealed class ShortBodyHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly string _mediaType;

        public ShortBodyHandler(string body, string mediaType)
        {
            _body = body;
            _mediaType = mediaType;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = new StringContent(_body);
            content.Headers.ContentType = new MediaTypeHeaderValue(_mediaType);
            return Task.FromResult(Ok(content));
        }
    }

    /// <summary>
    /// Returns headers immediately and a body that completes only when the read
    /// token is cancelled. Matches a socket whose response headers arrived and
    /// whose body then stalled.
    /// </summary>
    private sealed class StalledBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Ok(new StalledBodyContent()));
    }

    /// <summary>
    /// Returns headers immediately. Opening the body blocks in
    /// <see cref="HttpContent.CreateContentReadStreamAsync(CancellationToken)"/>
    /// until that token is cancelled, which is the gap before the returned
    /// stream's reads are wrapped.
    /// </summary>
    private sealed class StalledStreamCreationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Ok(new StalledStreamCreationContent()));
    }

    /// <summary>
    /// Handler responses are owned by <see cref="HttpClient"/>. Returning the
    /// new message from this method is what keeps that transfer visible.
    /// </summary>
    private static HttpResponseMessage Ok(HttpContent content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class StalledStreamCreationContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => Task.FromException(new InvalidOperationException("Stream creation must not buffer the body."));

        protected override Task SerializeToStreamAsync(
            Stream stream, TransportContext? context, CancellationToken cancellationToken)
            => Task.FromException(new InvalidOperationException("Stream creation must not buffer the body."));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return Stream.Null;
        }
    }

    private sealed class StalledBodyContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            // The token overload is what HttpClient buffering and CopyToAsync call.
            // The parameterless overload drops the token, which is the bug's shape
            // when the caller does not supply a deadline of its own.
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
            => Task.FromResult<Stream>(new StalledBodyStream());
    }

    private sealed class StalledBodyStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
