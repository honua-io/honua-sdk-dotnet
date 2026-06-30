// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Admin;

/// <summary>
/// Delegating handler that adds authentication headers to admin API requests.
/// </summary>
internal sealed class HonuaAdminAuthHandler : HonuaAuthHandler<HonuaAdminClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaAdminAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The admin client options containing authentication credentials.</param>
    public HonuaAdminAuthHandler(IOptions<HonuaAdminClientOptions> options)
        : base(options.Value, "admin", HonuaAdminClientOptions.ValidateBaseAddress)
    {
    }
}
