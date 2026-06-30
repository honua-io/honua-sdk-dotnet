// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// Delegating handler that adds authentication headers to Console Share requests.
/// </summary>
internal sealed class HonuaConsoleShareAuthHandler : HonuaAuthHandler<HonuaConsoleShareClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaConsoleShareAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The Console Share client options containing authentication credentials.</param>
    public HonuaConsoleShareAuthHandler(IOptions<HonuaConsoleShareClientOptions> options)
        : base(options.Value, "console-share", HonuaConsoleShareClientOptions.ValidateBaseAddress)
    {
    }
}
