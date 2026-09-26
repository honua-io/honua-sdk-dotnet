// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// An ArcGIS service root URL (<c>.../FeatureServer</c> or <c>.../MapServer</c>) split into the parts a
/// <see cref="HonuaFeatureServerClient"/> addresses: the root (with any path prefix such as
/// <c>/&lt;org&gt;/arcgis/</c> or a web adaptor's <c>/arcgis/</c>), the services directory, the possibly
/// foldered service id, and the service type.
/// </summary>
public sealed record ArcGisServiceRoot
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
    public required ArcGisServiceType ServiceType { get; init; }

    /// <summary>
    /// Parses an absolute service root URL. The query string and fragment are ignored.
    /// </summary>
    /// <param name="serviceRootUrl">A URL whose last path segment is <c>FeatureServer</c> or <c>MapServer</c>.</param>
    /// <returns>The parsed service root.</returns>
    /// <exception cref="ArgumentException">The URL is relative or does not end at a FeatureServer or MapServer service root.</exception>
    public static ArcGisServiceRoot Parse(Uri serviceRootUrl)
    {
        ArgumentNullException.ThrowIfNull(serviceRootUrl);
        return TryParse(serviceRootUrl, out var root)
            ? root
            : throw new ArgumentException(
                "An ArcGIS service root URL must be absolute and end with a service name followed by FeatureServer or MapServer.",
                nameof(serviceRootUrl));
    }

    /// <summary>
    /// Attempts to parse an absolute service root URL. The query string and fragment are ignored.
    /// </summary>
    /// <param name="serviceRootUrl">A URL whose last path segment is <c>FeatureServer</c> or <c>MapServer</c>.</param>
    /// <param name="root">The parsed service root when this method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the URL is an absolute FeatureServer or MapServer service root.</returns>
    public static bool TryParse(Uri? serviceRootUrl, [NotNullWhen(true)] out ArcGisServiceRoot? root)
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
        // before it is the deployment prefix (ArcGIS Online org, web adaptor name) and everything after
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
        root = new ArcGisServiceRoot
        {
            RootAddress = new Uri(serviceRootUrl.GetLeftPart(UriPartial.Authority) + "/" + prefix, UriKind.Absolute),
            ServicesPath = servicesPath,
            ServiceId = string.Join('/', serviceIdSegments),
            ServiceType = serviceType,
        };
        return true;
    }

    /// <summary>
    /// Creates client options that address this service root.
    /// </summary>
    /// <param name="maxResponseBytes">Response body ceiling in bytes; defaults to <see cref="HonuaFeatureServerClientOptions.DefaultMaxResponseBytes"/>.</param>
    /// <returns>Options for <see cref="HonuaFeatureServerClient(HttpClient, HonuaFeatureServerClientOptions)"/>; pass <see cref="ServiceId"/> as the service id.</returns>
    public HonuaFeatureServerClientOptions ToClientOptions(long maxResponseBytes = HonuaFeatureServerClientOptions.DefaultMaxResponseBytes) => new()
    {
        RootAddress = RootAddress,
        ServicesPath = ServicesPath,
        ServiceType = ServiceType,
        MaxResponseBytes = maxResponseBytes,
    };

    private static bool TryParseServiceType(string segment, out ArcGisServiceType serviceType)
    {
        if (segment.Equals(nameof(ArcGisServiceType.FeatureServer), StringComparison.OrdinalIgnoreCase))
        {
            serviceType = ArcGisServiceType.FeatureServer;
            return true;
        }

        if (segment.Equals(nameof(ArcGisServiceType.MapServer), StringComparison.OrdinalIgnoreCase))
        {
            serviceType = ArcGisServiceType.MapServer;
            return true;
        }

        serviceType = default;
        return false;
    }
}
