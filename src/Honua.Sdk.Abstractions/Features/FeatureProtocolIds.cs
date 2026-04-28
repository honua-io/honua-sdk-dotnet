// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Canonical feature source protocol identifiers and aliases shared across Honua SDKs.
/// </summary>
public static class FeatureProtocolIds
{
    /// <summary>.NET gRPC FeatureService client protocol identifier.</summary>
    public const string Grpc = "grpc";

    /// <summary>Canonical GeoServices Feature Service protocol identifier.</summary>
    public const string GeoServicesFeatureService = "geoservices-feature-service";

    /// <summary>Existing .NET GeoServices FeatureServer provider name alias.</summary>
    public const string GeoServicesFeatureServer = "geoservices-featureserver";

    /// <summary>OGC API Features protocol identifier.</summary>
    public const string OgcFeatures = "ogc-features";

    /// <summary>WFS protocol identifier.</summary>
    public const string Wfs = "wfs";

    private static readonly Dictionary<string, string> CanonicalIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Grpc] = Grpc,
            [GeoServicesFeatureService] = GeoServicesFeatureService,
            [GeoServicesFeatureServer] = GeoServicesFeatureService,
            ["featureserver"] = GeoServicesFeatureService,
            ["feature-server"] = GeoServicesFeatureService,
            ["feature-service"] = GeoServicesFeatureService,
            ["geoservices-feature-server"] = GeoServicesFeatureService,
            [OgcFeatures] = OgcFeatures,
            ["ogcapi-features"] = OgcFeatures,
            ["ogc-api-features"] = OgcFeatures,
            ["OgcFeatures"] = OgcFeatures,
            [Wfs] = Wfs
        };

    private static readonly Dictionary<string, IReadOnlyList<string>> AliasMap =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [Grpc] = [Grpc],
            [GeoServicesFeatureService] =
            [
                GeoServicesFeatureService,
                GeoServicesFeatureServer,
                "featureserver",
                "feature-server",
                "feature-service",
                "FeatureServer",
                "geoservices-feature-server"
            ],
            [OgcFeatures] =
            [
                OgcFeatures,
                "ogcapi-features",
                "ogc-api-features",
                "OgcFeatures"
            ],
            [Wfs] = [Wfs]
        };

    /// <summary>Canonical protocol identifiers currently backed by .NET feature clients.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Grpc,
        GeoServicesFeatureService,
        OgcFeatures,
        Wfs
    ];

    /// <summary>
    /// Normalizes a protocol identifier or provider name alias to its canonical protocol identifier.
    /// </summary>
    /// <param name="protocolId">Protocol identifier or alias.</param>
    /// <returns>The canonical protocol identifier.</returns>
    public static string Normalize(string protocolId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protocolId);

        return CanonicalIds.TryGetValue(protocolId, out var canonical)
            ? canonical
            : protocolId;
    }

    /// <summary>
    /// Returns whether two protocol identifiers refer to the same canonical protocol.
    /// </summary>
    /// <param name="left">First protocol identifier or alias.</param>
    /// <param name="right">Second protocol identifier or alias.</param>
    /// <returns><see langword="true"/> when both identifiers normalize to the same protocol.</returns>
    public static bool Matches(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);

    /// <summary>
    /// Returns the known aliases for a canonical protocol identifier.
    /// </summary>
    /// <param name="protocolId">Canonical protocol identifier or alias.</param>
    /// <returns>Known aliases, including the canonical identifier.</returns>
    public static IReadOnlyList<string> AliasesFor(string protocolId)
    {
        var canonical = Normalize(protocolId);
        return AliasMap.TryGetValue(canonical, out var aliases)
            ? aliases
            : [canonical];
    }
}
