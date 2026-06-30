// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Processes;

/// <summary>
/// Delegating handler that adds authentication headers to OGC API Processes requests.
/// </summary>
internal sealed class HonuaProcessesAuthHandler : HonuaAuthHandler<HonuaProcessesClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessesAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The OGC API Processes client options containing authentication credentials.</param>
    public HonuaProcessesAuthHandler(IOptions<HonuaProcessesClientOptions> options)
        : base(options.Value, "processes", HonuaProcessesClientOptions.ValidateBaseAddress)
    {
    }
}
