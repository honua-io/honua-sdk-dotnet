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
/// <c>SamplingDuration</c> (default 30 s) is at least <em>double</em> the
/// per-attempt timeout. With the default option <see cref="IHonuaClientOptions.Timeout"/>
/// of 100 s, naively setting <c>AttemptTimeout = Timeout</c> fails that check and
/// throws <c>OptionsValidationException</c> the first time any client is resolved.
/// </para>
/// <para>
/// This helper treats <see cref="IHonuaClientOptions.Timeout"/> as the
/// <em>overall</em> request budget (matching its documented meaning, "including
/// retry attempts") and derives a per-attempt timeout that (a) stays strictly
/// below half the circuit-breaker sampling window and (b) leaves room for retries
/// when the overall budget allows it.
/// </para>
/// </remarks>
public static class HonuaResilienceTimeouts
{
    /// <summary>
    /// The standard resilience handler's default circuit-breaker sampling
    /// duration. The handler requires <c>SamplingDuration &gt;= 2 * AttemptTimeout</c>.
    /// </summary>
    public static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(30);

    // A small margin below SamplingDuration/2 (15 s) keeps the validator happy
    // even accounting for any internal rounding, and is a sane per-attempt ceiling.
    private static readonly TimeSpan MaxAttemptTimeout = TimeSpan.FromSeconds(14);

    /// <summary>
    /// Computes the per-attempt timeout for the standard resilience handler from
    /// the caller's overall <paramref name="totalTimeout"/> budget. The result is
    /// always strictly less than half of <see cref="CircuitBreakerSamplingDuration"/>
    /// (so handler validation passes) and never exceeds the overall budget. When
    /// the overall budget is large, the per-attempt timeout is capped so multiple
    /// attempts fit inside the budget.
    /// </summary>
    /// <param name="totalTimeout">The overall request budget (the option <c>Timeout</c>).</param>
    /// <returns>A per-attempt timeout suitable for <c>AttemptTimeout.Timeout</c>.</returns>
    public static TimeSpan AttemptTimeout(TimeSpan totalTimeout)
    {
        if (totalTimeout <= MaxAttemptTimeout)
        {
            // Budget already fits within a single valid attempt; leave a little
            // headroom for at least a second attempt when the budget is not tiny.
            return totalTimeout;
        }

        return MaxAttemptTimeout;
    }

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
