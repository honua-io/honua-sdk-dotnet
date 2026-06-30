// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures.Wfs;

/// <summary>
/// Delegating handler that adds authentication headers to WFS requests.
/// </summary>
internal sealed class HonuaWfsAuthHandler : HonuaAuthHandler<HonuaWfsClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The WFS client options containing authentication credentials.</param>
    public HonuaWfsAuthHandler(IOptions<HonuaWfsClientOptions> options)
        : base(options.Value, "wfs", HonuaWfsClientOptions.ValidateBaseAddress)
    {
    }
}
