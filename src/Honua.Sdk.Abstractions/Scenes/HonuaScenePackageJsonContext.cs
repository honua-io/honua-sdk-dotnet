// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Scenes;

[JsonSerializable(typeof(HonuaScenePackageManifest))]
internal sealed partial class HonuaScenePackageJsonContext : JsonSerializerContext
{
}
