// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Centralizes the timeout math shared by every package's
/// <c>AddStandardResilienceHandler</c> registration so the standard resilience
/// pipeline validates successfully and leaves real budget for retries.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddStandardResilienceHandler</c> validates that the circuit-breaker
/// <c>SamplingDuration</c> is at least <em>double</em> the per-attempt timeout,
/// and that the total request timeout is greater than both. With the default
/// option <see cref="IHonuaClientOptions.Timeout"/> of 100 s, naively setting
/// <c>AttemptTimeout = Timeout</c> fails those checks and throws
/// <c>OptionsValidationException</c> the first time any client is resolved.
/// </para>
/// <para>
/// This helper treats <see cref="IHonuaClientOptions.Timeout"/> as the
/// <em>overall</em> request budget (matching its documented meaning, "including
/// retry attempts") and derives <em>both</em> the per-attempt timeout
/// (<see cref="AttemptTimeout(TimeSpan)"/>) and the circuit-breaker sampling
/// window (<see cref="SamplingDuration(TimeSpan)"/>) <em>from that budget</em>.
/// Deriving the sampling window from the budget — instead of pinning the
/// per-attempt timeout under a fixed 30 s window — means a configured 100 s budget
/// genuinely permits a single attempt of up to ~45 s (and a 24 h budget scales up
/// accordingly), rather than aborting every attempt at a hard-coded 14 s ceiling.
/// </para>
/// </remarks>
public static class HonuaResilienceTimeouts
{
    // Per-attempt timeout as a fraction of the overall budget. At 0.45 the
    // budget always covers at least two attempts, and the derived sampling
    // window (2 x attempt + slack) stays strictly below the total budget, so
    // the standard resilience handler's validator is satisfied for any budget:
    //   total > sampling (0.95) >= 2 x attempt (0.90)  and  total > attempt (0.45).
    private const double AttemptFraction = 0.45;
    private const double SamplingFraction = 0.95;

    /// <summary>
    /// The standard resilience handler's default circuit-breaker sampling
    /// duration. Retained for backwards compatibility; the effective sampling
    /// window is now derived from the configured budget via
    /// <see cref="SamplingDuration(TimeSpan)"/> so that the per-attempt timeout is
    /// not pinned under a fixed 30 s window.
    /// </summary>
    public static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Computes the per-attempt timeout for the standard resilience handler from
    /// the caller's overall <paramref name="totalTimeout"/> budget. The result
    /// scales with the budget (≈45% of it) so a long single call is allowed to
    /// run for a meaningful fraction of the configured budget instead of being
    /// aborted at a fixed ceiling, while still leaving room for a retry. Pair this
    /// with <see cref="SamplingDuration(TimeSpan)"/> when configuring the handler
    /// so that <c>SamplingDuration &gt;= 2 * AttemptTimeout</c> always holds.
    /// </summary>
    /// <param name="totalTimeout">The overall request budget (the option <c>Timeout</c>).</param>
    /// <returns>A per-attempt timeout suitable for <c>AttemptTimeout.Timeout</c>.</returns>
    public static TimeSpan AttemptTimeout(TimeSpan totalTimeout)
        => totalTimeout <= TimeSpan.Zero
            ? totalTimeout
            : TimeSpan.FromTicks((long)(totalTimeout.Ticks * AttemptFraction));

    /// <summary>
    /// Computes the circuit-breaker sampling window for the standard resilience
    /// handler from the caller's overall <paramref name="totalTimeout"/> budget.
    /// The result (≈95% of the budget) is guaranteed to be at least double the
    /// per-attempt timeout returned by <see cref="AttemptTimeout(TimeSpan)"/> for
    /// the same budget, and strictly less than the total budget, so the handler's
    /// validation passes for any configured <c>Timeout</c>.
    /// </summary>
    /// <param name="totalTimeout">The overall request budget (the option <c>Timeout</c>).</param>
    /// <returns>A sampling duration suitable for <c>CircuitBreaker.SamplingDuration</c>.</returns>
    public static TimeSpan SamplingDuration(TimeSpan totalTimeout)
        => totalTimeout <= TimeSpan.Zero
            ? totalTimeout
            : TimeSpan.FromTicks((long)(totalTimeout.Ticks * SamplingFraction));

    /// <summary>
    /// Computes the overall request timeout for the standard resilience handler's
    /// <c>TotalRequestTimeout</c>. This is the caller's overall budget, which is
    /// guaranteed to be at least the per-attempt timeout.
    /// </summary>
    /// <param name="totalTimeout">The overall request budget (the option <c>Timeout</c>).</param>
    /// <returns>A total request timeout suitable for <c>TotalRequestTimeout.Timeout</c>.</returns>
    public static TimeSpan TotalRequestTimeout(TimeSpan totalTimeout) => totalTimeout;

    /// <summary>
    /// Computes the <see cref="System.Net.Http.HttpClient.Timeout"/> to apply to the typed
    /// client. When the resilience pipeline is enabled it owns all timing, so the
    /// outer <c>HttpClient.Timeout</c> must not pre-empt the total budget (otherwise
    /// a 100 s budget is silently capped at the same single value used for one
    /// attempt). When retry is disabled, the option <paramref name="totalTimeout"/>
    /// is honored directly.
    /// </summary>
    /// <param name="totalTimeout">The overall request budget (the option <c>Timeout</c>).</param>
    /// <param name="resilienceEnabled">Whether the standard resilience pipeline is registered.</param>
    /// <returns>The value to assign to <c>HttpClient.Timeout</c>.</returns>
    public static TimeSpan HttpClientTimeout(TimeSpan totalTimeout, bool resilienceEnabled)
        => resilienceEnabled ? System.Threading.Timeout.InfiniteTimeSpan : totalTimeout;
}
