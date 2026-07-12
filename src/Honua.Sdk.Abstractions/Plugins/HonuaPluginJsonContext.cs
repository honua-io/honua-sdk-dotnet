// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Plugins;

[JsonSerializable(typeof(HonuaPluginManifest))]
internal sealed partial class HonuaPluginJsonContext : JsonSerializerContext
{
}
