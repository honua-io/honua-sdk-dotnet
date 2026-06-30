// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Delegating handler that adds authentication headers to OGC API Features requests.
/// </summary>
internal sealed class HonuaOgcFeaturesAuthHandler : HonuaAuthHandler<HonuaOgcFeaturesClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcFeaturesAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The OGC API Features client options containing authentication credentials.</param>
    public HonuaOgcFeaturesAuthHandler(IOptions<HonuaOgcFeaturesClientOptions> options)
        : base(options.Value, "ogc-features", HonuaOgcFeaturesClientOptions.ValidateBaseAddress)
    {
    }
}
