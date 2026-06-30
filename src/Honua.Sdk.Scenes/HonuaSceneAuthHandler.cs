// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Scenes;

/// <summary>
/// Delegating handler that adds authentication headers to scene metadata requests.
/// </summary>
internal sealed class HonuaSceneAuthHandler : HonuaAuthHandler<HonuaSceneClientOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The scene client options containing authentication credentials.</param>
    public HonuaSceneAuthHandler(IOptions<HonuaSceneClientOptions> options)
        : base(options.Value, "scenes", HonuaSceneClientOptions.ValidateBaseAddress)
    {
    }
}
