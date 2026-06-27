// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Shared base for Honua SDK client options classes. Centralizes the common
/// transport configuration surface — <see cref="BaseAddress"/>,
/// <see cref="Timeout"/>, <see cref="EnableRetry"/>, <see cref="MaxRetryAttempts"/>
/// and <see cref="PrimaryHttpMessageHandlerFactory"/> — together with its
/// defaults and the <see cref="MaxRetryAttempts"/> range check, so the protocol
/// packages no longer re-declare this surface in lockstep. Shared base-address
/// and timeout validation lives in <see cref="HonuaClientOptionsValidation"/>.
/// </summary>
public abstract class HonuaClientOptionsBase : IHonuaClientOptions
{
    private int _maxRetryAttempts = 3;

    /// <summary>
    /// Base address of the Honua server. Required; the SDK does not assume a
    /// localhost default so that missing configuration surfaces loudly at
    /// registration time via the owning package's <c>ValidateBaseAddress</c> check.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Optional primary HTTP message handler factory for certificate, mTLS, or
    /// enterprise transport configuration.
    /// </summary>
    public Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }

    /// <summary>
    /// Whether to enable automatic retry on transient failures (default: true).
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Overall timeout for each request, including retry attempts (default: 100 seconds).
    /// Must be greater than 10 milliseconds and less than 24 hours.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Maximum number of retry attempts (default: 3). Must be in the inclusive
    /// range [2, 5]; the setter throws <see cref="ArgumentOutOfRangeException"/>
    /// for values outside that range. Only applies when <see cref="EnableRetry"/> is <c>true</c>.
    /// </summary>
    public int MaxRetryAttempts
    {
        get => _maxRetryAttempts;
        set
        {
            if (value is < 2 or > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value,
                    "MaxRetryAttempts must be in the inclusive range [2, 5].");
            }
            _maxRetryAttempts = value;
        }
    }
}
