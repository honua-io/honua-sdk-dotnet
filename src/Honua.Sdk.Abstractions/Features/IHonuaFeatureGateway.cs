// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Unified feature accessor that lets a geoprocessing (GP) tool read and write
/// feature attachments and run temporal / grouped-statistics queries without
/// caring which transport actually backs them.
/// </summary>
/// <remarks>
/// <para>
/// The workhorse gRPC <c>FeatureService</c> exposes only
/// <c>QueryFeatures</c>/<c>QueryFeaturesStream</c>/<c>ApplyEdits</c>: it has no
/// attachment RPCs and no provider-neutral time-filter or grouped-statistics
/// <c>having</c> contract. A GP tool that reads features over gRPC therefore used
/// to hit <see cref="NotSupportedException"/> the moment it touched media or a
/// time-aware/summary query.
/// </para>
/// <para>
/// The gateway closes that gap by composing every registered feature provider and
/// routing each operation to the first provider whose
/// <see cref="FeatureAttachmentCapabilities"/> / <see cref="FeatureQueryCapabilities"/>
/// supports it. Attachments resolve over the GeoServices FeatureServer client even
/// when features stream over gRPC; a temporal or <c>having</c> query transparently
/// falls back from gRPC to a time/having-capable provider. Both the gateway and the
/// underlying clients expose capability flags so a tool can also select a provider
/// up front instead of relying on routing.
/// </para>
/// </remarks>
public interface IHonuaFeatureGateway : IHonuaFeatureQueryClient, IHonuaFeatureAttachmentClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    new string ProviderName { get; }
}
