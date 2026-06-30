// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Delegating handler that adds authentication headers to GeoServices API requests.
/// </summary>
internal sealed class HonuaGeoServicesAuthHandler : HonuaAuthHandler<HonuaGeoServicesClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeoServicesAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The GeoServices client options containing authentication credentials.</param>
    public HonuaGeoServicesAuthHandler(IOptions<HonuaGeoServicesClientOptions> options)
        : base(options.Value, "geoservices", HonuaGeoServicesClientOptions.ValidateBaseAddress)
    {
    }
}
