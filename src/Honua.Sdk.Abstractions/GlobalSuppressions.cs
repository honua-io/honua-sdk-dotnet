// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "ApiDesign",
    "RS0041:Public members should not use oblivious types",
    Justification = "System.Text.Json source generation emits oblivious metadata members. " +
                    "Those generated members remain explicitly tracked by RS0016/RS0017 snapshots.",
    Scope = "type",
    Target = "~T:Honua.Sdk.Abstractions.Serialization.HonuaAbstractionsJsonContext")]
