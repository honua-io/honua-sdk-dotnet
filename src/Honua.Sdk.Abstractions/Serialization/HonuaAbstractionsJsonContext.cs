// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Console;
using Honua.Sdk.Abstractions.Environments;

namespace Honua.Sdk.Abstractions.Serialization;

/// <summary>
/// Source-generated JSON context for shared SDK contracts.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HonuaEnvironmentProfileSet))]
[JsonSerializable(typeof(HonuaEnvironmentProfile))]
[JsonSerializable(typeof(HonuaEnvironmentProfile[]))]
[JsonSerializable(typeof(HonuaTenantScope))]
[JsonSerializable(typeof(HonuaTransportCapabilities))]
[JsonSerializable(typeof(HonuaTrustProfile))]
[JsonSerializable(typeof(HonuaClientCertificateReference))]
[JsonSerializable(typeof(HonuaEnvironmentTrustState))]
[JsonSerializable(typeof(HonuaConsoleShellDescriptor))]
[JsonSerializable(typeof(HonuaConsolePrincipal))]
[JsonSerializable(typeof(HonuaConsoleNavigationItem))]
[JsonSerializable(typeof(HonuaConsoleNavigationItem[]))]
[JsonSerializable(typeof(HonuaConsoleRouteGuard))]
[JsonSerializable(typeof(HonuaConsoleRouteGuard[]))]
[JsonSerializable(typeof(HonuaConsoleRouteGuardDecision))]
[JsonSerializable(typeof(HonuaConsolePermissionGrant))]
[JsonSerializable(typeof(HonuaConsolePermissionGrant[]))]
public sealed partial class HonuaAbstractionsJsonContext : JsonSerializerContext
{
}
