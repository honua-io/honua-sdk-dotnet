// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Catalogs.Records;

/// <summary>
/// Delegating handler that adds authentication headers to OGC API Records requests.
/// </summary>
internal sealed class HonuaOgcRecordsAuthHandler : HonuaAuthHandler<HonuaOgcRecordsClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The OGC API Records client options containing authentication credentials.</param>
    public HonuaOgcRecordsAuthHandler(IOptions<HonuaOgcRecordsClientOptions> options)
        : base(options.Value, "ogc-records", HonuaOgcRecordsClientOptions.ValidateBaseAddress)
    {
    }
}
