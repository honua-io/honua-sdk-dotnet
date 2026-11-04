// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>A GeoServices REST service root URL split into root (with any path prefix), services directory, service id and service type.</summary>
public sealed record GeoServicesServiceRoot
{
    private const string RestSegment = "rest";
    private const string ServicesSegment = "services";

    /// <summary>Absolute root including any path prefix, always ending with <c>/</c>.</summary>
    public required Uri RootAddress { get; init; }

    /// <summary>Services directory path relative to <see cref="RootAddress"/>, for example <c>rest/services</c>; empty when the URL has none.</summary>
    public required string ServicesPath { get; init; }

    /// <summary>Unescaped service id, including folders (for example <c>Utilities/Water</c>).</summary>
    public required string ServiceId { get; init; }

    /// <summary>The service type named by the final URL segment.</summary>
    public required GeoServicesServiceType ServiceType { get; init; }

    /// <summary>Parses an absolute URL ending at a FeatureServer or MapServer root, ignoring query and fragment; throws <see cref="ArgumentException"/> otherwise.</summary>
    public static GeoServicesServiceRoot Parse(Uri serviceRootUrl)
    {
        ArgumentNullException.ThrowIfNull(serviceRootUrl);
        return TryParse(serviceRootUrl, out var root)
            ? root
            : throw new ArgumentException(
                "A GeoServices REST service root URL must be absolute and end with a service name followed by FeatureServer or MapServer.",
                nameof(serviceRootUrl));
    }

    /// <summary>Tries to parse an absolute URL ending at a FeatureServer or MapServer root, ignoring query and fragment.</summary>
    public static bool TryParse(Uri? serviceRootUrl, [NotNullWhen(true)] out GeoServicesServiceRoot? root)
    {
        root = null;
        if (serviceRootUrl is null || !serviceRootUrl.IsAbsoluteUri)
        {
            return false;
        }

        var segments = serviceRootUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !TryParseServiceType(segments[^1], out var serviceType))
        {
            return false;
        }

        // The services directory is the last "rest/services" pair before the type segment; everything
        // before it is the deployment prefix (hosted GIS organization org, web adaptor name) and everything after
        // it is the (possibly foldered) service id. Without that pair only the service name is known.
        var servicesIndex = -1;
        for (var i = segments.Length - 2; i >= 1; i--)
        {
            if (segments[i - 1].Equals(RestSegment, StringComparison.OrdinalIgnoreCase) &&
                segments[i].Equals(ServicesSegment, StringComparison.OrdinalIgnoreCase))
            {
                servicesIndex = i;
                break;
            }
        }

        var prefixLength = servicesIndex >= 0 ? servicesIndex - 1 : segments.Length - 2;
        var serviceIdStart = servicesIndex >= 0 ? servicesIndex + 1 : segments.Length - 2;
        var servicesPath = servicesIndex >= 0 ? $"{segments[servicesIndex - 1]}/{segments[servicesIndex]}" : string.Empty;
        var serviceIdSegments = segments[serviceIdStart..^1].Select(Uri.UnescapeDataString).ToArray();
        if (serviceIdSegments.Length == 0 || !HonuaFeatureServerClient.AreValidServiceIdSegments(serviceIdSegments))
        {
            return false;
        }

        var prefix = string.Concat(segments[..prefixLength].Select(segment => segment + "/"));
        root = new GeoServicesServiceRoot
        {
            RootAddress = new Uri(serviceRootUrl.GetLeftPart(UriPartial.Authority) + "/" + prefix, UriKind.Absolute),
            ServicesPath = servicesPath,
            ServiceId = string.Join('/', serviceIdSegments),
            ServiceType = serviceType,
        };
        return true;
    }

    /// <summary>Creates client options that address this service root; pass <see cref="ServiceId"/> as the service id on each call.</summary>
    public HonuaFeatureServerClientOptions ToClientOptions(long maxResponseBytes = HonuaFeatureServerClientOptions.DefaultMaxResponseBytes) => new()
    {
        RootAddress = RootAddress,
        ServicesPath = ServicesPath,
        ServiceType = ServiceType,
        MaxResponseBytes = maxResponseBytes,
    };

    private static bool TryParseServiceType(string segment, out GeoServicesServiceType serviceType)
    {
        if (segment.Equals(nameof(GeoServicesServiceType.FeatureServer), StringComparison.OrdinalIgnoreCase))
        {
            serviceType = GeoServicesServiceType.FeatureServer;
            return true;
        }

        if (segment.Equals(nameof(GeoServicesServiceType.MapServer), StringComparison.OrdinalIgnoreCase))
        {
            serviceType = GeoServicesServiceType.MapServer;
            return true;
        }

        serviceType = default;
        return false;
    }
}
