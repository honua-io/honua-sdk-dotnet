// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

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

        if (options.EnableRetry)
        {
            httpBuilder.AddStandardResilienceHandler(resilience =>
            {
                resilience.TotalRequestTimeout.Timeout = HonuaResilienceTimeouts.TotalRequestTimeout(options.Timeout);
                resilience.AttemptTimeout.Timeout = HonuaResilienceTimeouts.AttemptTimeout(options.Timeout);
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
        }

        // Normalize failures without an HTTP response so callers can use the documented
        // catch (HonuaException) boundary. This is outside the resilience pipeline, allowing
        // transient failures to be retried before normalization.
        httpBuilder.AddHttpMessageHandler(() => new HonuaTransportExceptionHandler());

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
                || exception.GetType().FullName == "Polly.Timeout.TimeoutRejectedException"
                || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
    }
}
