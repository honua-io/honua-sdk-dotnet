// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Common shape implemented by every Honua SDK client options class. Centralizes
/// the cross-package contract for <see cref="BaseAddress"/>, <see cref="Timeout"/>,
/// <see cref="EnableRetry"/>, <see cref="MaxRetryAttempts"/>, and the optional
/// <see cref="PrimaryHttpMessageHandlerFactory"/> hook so consumers (and code-review
/// tools) can rely on a single contract across all REST and gRPC packages.
/// </summary>
public interface IHonuaClientOptions
{
    /// <summary>
    /// Base address of the Honua server. Required: the SDK does not bake in a
    /// localhost default. Misconfiguration surfaces loudly at registration
    /// time via the owning package's options-class <c>ValidateBaseAddress</c>
    /// (REST) or <c>ParseAndValidateAddress</c> (gRPC) check.
    /// </summary>
    Uri? BaseAddress { get; set; }

    /// <summary>
    /// Overall request budget per call, including retry attempts.
    /// Must be in the range (10 ms, 24 h).
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    /// Enables the default transient-failure retry policy for the underlying
    /// transport (gRPC service config or HTTP standard resilience pipeline).
    /// </summary>
    bool EnableRetry { get; set; }

    /// <summary>
    /// Maximum number of retry attempts (including the original call). Must be
    /// in the inclusive range [2, 5]; the setter throws
    /// <see cref="ArgumentOutOfRangeException"/> for out-of-range values.
    /// Applied only when <see cref="EnableRetry"/> is <c>true</c>.
    /// </summary>
    int MaxRetryAttempts { get; set; }

    /// <summary>
    /// Optional primary HTTP message handler factory for certificate, mTLS,
    /// proxy, or enterprise transport configuration. Returning a fresh
    /// handler per call is the caller's responsibility.
    /// </summary>
    Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }
}
