// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Studio;

/// <summary>
/// Delegating handler that adds authentication headers to Console Studio requests.
/// </summary>
internal sealed class HonuaStudioAuthHandler : HonuaAuthHandler<HonuaStudioClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStudioAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The Console Studio client options containing authentication credentials.</param>
    public HonuaStudioAuthHandler(IOptions<HonuaStudioClientOptions> options)
        : base(options.Value, "studio", HonuaStudioClientOptions.ValidateBaseAddress)
    {
    }
}
