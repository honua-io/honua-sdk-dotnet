// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly.Timeout;

namespace Honua.Sdk.Internal.Http;

/// <summary>
/// Shared registration helper that applies the primary HTTP message handler and the
/// standard resilience pipeline that every Honua REST typed-client uses. Compiled as
/// linked source into each REST package (it depends on
/// <c>Microsoft.Extensions.Http.Resilience</c>, which the zero-dependency
/// <c>Honua.Sdk.Abstractions</c> contracts package deliberately does not reference),
/// so the security-relevant no-redirect default and the resilience/timeout math live
/// in exactly one place rather than being copy-pasted across the REST
/// <c>ServiceCollectionExtensions</c>.
/// </summary>
internal static class HonuaRestHttpClientRegistration
{
    /// <summary>
    /// Configures the shared Honua REST transport on <paramref name="httpBuilder"/>:
    /// the primary HTTP message handler (the caller-supplied
    /// <see cref="IHonuaClientOptions.PrimaryHttpMessageHandlerFactory"/> when present,
    /// otherwise the credential-safe no-redirect default) and, when
    /// <see cref="IHonuaClientOptions.EnableRetry"/> is set, the standard resilience
    /// pipeline with the shared timeout budget from <see cref="HonuaResilienceTimeouts"/>.
    /// That per-attempt budget also covers the response body, including streaming reads.
    /// </summary>
    /// <param name="httpBuilder">The typed-client builder to configure.</param>
    /// <param name="options">The captured client options snapshot.</param>
    /// <param name="configureRetry">
    /// Optional hook to customize the retry strategy. When <see langword="null"/> the
    /// default policy retries transient failures on safe HTTP methods only (via
    /// <c>DisableForUnsafeHttpMethods</c>). Clients with idempotent non-safe requests
    /// (for example the GeoServices <c>/query</c> POST fallback) supply their own
    /// predicate instead.
    /// </param>
    /// <returns>The same <paramref name="httpBuilder"/> for chaining.</returns>
    public static IHttpClientBuilder ConfigureHonuaRestHttpClient(
        this IHttpClientBuilder httpBuilder,
        IHonuaClientOptions options,
        Action<HttpRetryStrategyOptions>? configureRetry = null)
    {
        ArgumentNullException.ThrowIfNull(httpBuilder);
        ArgumentNullException.ThrowIfNull(options);

        if (options.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
        else
        {
            // Disable auto-redirect by default so the custom X-API-Key header is
            // never forwarded to an attacker-controlled 30x redirect target.
            httpBuilder.ConfigurePrimaryHttpMessageHandler(
                HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler);
        }

        // Normalize failures without an HTTP response so callers can use the documented
        // catch (HonuaException) boundary. Register this before the resilience pipeline
        // so it is the outer handler: transient failures remain visible to Polly for
        // retries, and the terminal failure is normalized for callers.
        httpBuilder.AddHttpMessageHandler(() => new HonuaTransportExceptionHandler());

        if (options.EnableRetry)
        {
            // 45% of Timeout by default: 45 seconds when Timeout is the default 100 seconds.
            // Polly's attempt timeout ends when headers arrive, and HttpClient.Timeout is
            // infinite while this pipeline is registered, so the same budget is applied to
            // the response content below.
            var attemptTimeout = HonuaResilienceTimeouts.AttemptTimeout(options.Timeout);
            httpBuilder.AddStandardResilienceHandler(resilience =>
            {
                resilience.TotalRequestTimeout.Timeout = HonuaResilienceTimeouts.TotalRequestTimeout(options.Timeout);
                resilience.AttemptTimeout.Timeout = attemptTimeout;
                resilience.CircuitBreaker.SamplingDuration = HonuaResilienceTimeouts.SamplingDuration(options.Timeout);
                // The SDK option counts the initial send, while Polly counts only
                // retries after that send. Convert the public total-attempt contract
                // to Polly's retry-count contract so REST and gRPC behave alike.
                resilience.Retry.MaxRetryAttempts = options.MaxRetryAttempts - 1;
                resilience.Retry.UseJitter = true;

                if (configureRetry is null)
                {
                    resilience.Retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                    resilience.Retry.DisableForUnsafeHttpMethods();
                }
                else
                {
                    configureRetry(resilience.Retry);
                }
            });

            // Registered after the resilience handler so it runs inside each attempt,
            // closest to the transport. The deadline starts with the attempt and stays
            // with the response until the body is read or the response is disposed.
            httpBuilder.AddHttpMessageHandler(() => new HonuaAttemptResponseTimeoutHandler(attemptTimeout));
        }

        return httpBuilder;
    }

    private sealed class HonuaTransportExceptionHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HonuaException)
            {
                throw;
            }
            catch (Exception ex) when (IsTransportFailure(ex, cancellationToken))
            {
                var status = ex is HttpRequestException { StatusCode: { } code } ? (int?)code : null;
                throw new HonuaTransportException(
                    $"REST request failed before receiving a response: {ex.Message}", ex, status);
            }
        }

        private static bool IsTransportFailure(Exception exception, CancellationToken cancellationToken)
            => exception is HttpRequestException
                || exception is TimeoutRejectedException
                || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Keeps <see cref="HonuaResilienceTimeouts.AttemptTimeout"/> in force for the
    /// response body. The standard resilience attempt timeout completes when the
    /// inner handler returns headers, and <see cref="HttpClient.Timeout"/> is
    /// infinite while retry is enabled, so neither of those budgets covers
    /// <c>LoadIntoBufferAsync</c> or a later streaming read.
    /// </summary>
    private sealed class HonuaAttemptResponseTimeoutHandler : DelegatingHandler
    {
        private readonly TimeSpan _attemptTimeout;

        public HonuaAttemptResponseTimeoutHandler(TimeSpan attemptTimeout)
            => _attemptTimeout = attemptTimeout;

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!ShouldEnforce(_attemptTimeout))
            {
                return base.Send(request, cancellationToken);
            }

            CancellationTokenSource? deadline = new CancellationTokenSource(_attemptTimeout);
            HttpResponseMessage? response = null;
            try
            {
                response = base.Send(request, cancellationToken);
                response.Content = new AttemptDeadlineContent(response.Content, deadline);
                deadline = null;
                return response;
            }
            finally
            {
                if (deadline is not null)
                {
                    response?.Dispose();
                }

                deadline?.Dispose();
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!ShouldEnforce(_attemptTimeout))
            {
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            CancellationTokenSource? deadline = new CancellationTokenSource(_attemptTimeout);
            HttpResponseMessage? response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.Content = new AttemptDeadlineContent(response.Content, deadline);
                deadline = null;
                return response;
            }
            finally
            {
                if (deadline is not null)
                {
                    response?.Dispose();
                }

                deadline?.Dispose();
            }
        }

        private static bool ShouldEnforce(TimeSpan attemptTimeout)
            => attemptTimeout > TimeSpan.Zero && attemptTimeout != Timeout.InfiniteTimeSpan;
    }

    /// <summary>
    /// Response content whose reads observe the attempt deadline that started
    /// when the attempt's send began, including reads that happen after the
    /// resilience handler has already returned.
    /// </summary>
    private sealed class AttemptDeadlineContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly CancellationTokenSource _deadline;
        private bool _disposed;

        public AttemptDeadlineContent(HttpContent inner, CancellationTokenSource deadline)
        {
            _inner = inner;
            _deadline = deadline;
            foreach (var header in inner.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context,
            CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
            await _inner.CopyToAsync(stream, context, linked.Token).ConfigureAwait(false);
        }

        protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
            _inner.CopyTo(stream, context, linked.Token);
        }

        protected override bool TryComputeLength(out long length)
        {
            if (_inner.Headers.ContentLength is long contentLength)
            {
                length = contentLength;
                return true;
            }

            length = 0;
            return false;
        }

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
        {
            var innerStream = await _inner.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return new AttemptDeadlineStream(innerStream, _deadline.Token);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                base.Dispose(disposing);
                _inner.Dispose();
                _deadline.Dispose();
            }
        }
    }

    /// <summary>
    /// Stream wrapper that cancels reads when the attempt deadline elapses,
    /// even if the caller passes <see cref="CancellationToken.None"/>.
    /// </summary>
    private sealed class AttemptDeadlineStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationToken _deadline;
        private bool _disposed;

        public AttemptDeadlineStream(Stream inner, CancellationToken deadline)
        {
            _inner = inner;
            _deadline = deadline;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline);
            return await _inner.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                await _inner.DisposeAsync().ConfigureAwait(false);
            }

            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
