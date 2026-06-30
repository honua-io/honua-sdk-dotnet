// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Catalogs.Stac;

/// <summary>
/// Delegating handler that adds authentication headers to STAC requests.
/// </summary>
internal sealed class HonuaStacAuthHandler : HonuaAuthHandler<HonuaStacClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStacAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The STAC client options containing authentication credentials.</param>
    public HonuaStacAuthHandler(IOptions<HonuaStacClientOptions> options)
        : base(options.Value, "stac", HonuaStacClientOptions.ValidateBaseAddress)
    {
    }
}
