// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Spec;

/// <summary>
/// Delegating handler that adds authentication headers to spec workspace API requests.
/// </summary>
internal sealed class HonuaSpecAuthHandler : HonuaAuthHandler<HonuaSpecClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The spec client options containing authentication credentials.</param>
    public HonuaSpecAuthHandler(IOptions<HonuaSpecClientOptions> options)
        : base(options.Value, "spec", HonuaSpecClientOptions.ValidateBaseAddress)
    {
    }
}
