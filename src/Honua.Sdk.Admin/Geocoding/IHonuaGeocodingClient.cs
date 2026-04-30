// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin.Geocoding;

/// <summary>
/// Client interface for the Honua Geocoding REST API (GeoServices-compatible).
/// </summary>
public interface IHonuaGeocodingClient
{
    /// <summary>
    /// Geocodes an address string into one or more candidate locations.
    /// </summary>
    /// <param name="address">The address or place name to geocode.</param>
    /// <param name="options">Optional parameters to control the geocoding request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of geocode results ordered by score descending.</returns>
    Task<IReadOnlyList<GeocodeResult>> ForwardGeocodeAsync(
        string address,
        ForwardGeocodeOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Reverse geocodes a latitude/longitude pair into a street address.
    /// </summary>
    /// <param name="latitude">The latitude (Y coordinate) of the location.</param>
    /// <param name="longitude">The longitude (X coordinate) of the location.</param>
    /// <param name="options">Optional parameters to control the reverse geocoding request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A reverse geocode result, or <c>null</c> if no address was found.</returns>
    Task<ReverseGeocodeResult?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        ReverseGeocodeOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns autocomplete suggestions for a partial address or place name.
    /// </summary>
    /// <param name="text">The partial text to get suggestions for.</param>
    /// <param name="options">Optional parameters to control the suggest request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of suggestions.</returns>
    Task<IReadOnlyList<GeocodeSuggestion>> SuggestAsync(
        string text,
        SuggestOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Geocodes multiple addresses in a single request.
    /// </summary>
    /// <param name="addresses">The list of addresses to geocode.</param>
    /// <param name="options">Optional parameters to control the batch geocoding request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matched geocode results.</returns>
    Task<IReadOnlyList<GeocodeResult>> BatchGeocodeAsync(
        IReadOnlyList<string> addresses,
        BatchGeocodeOptions? options = null,
        CancellationToken ct = default);
}

/// <summary>
/// Extended batch geocoding client that preserves per-input partial-failure details.
/// </summary>
public interface IHonuaBatchGeocodingClient : IHonuaGeocodingClient
{
    /// <summary>
    /// Geocodes multiple addresses and returns one result envelope for each input address.
    /// </summary>
    /// <param name="addresses">The list of addresses to geocode.</param>
    /// <param name="options">Optional parameters to control the batch geocoding request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Batch geocode results keyed to the original input order.</returns>
    Task<IReadOnlyList<BatchGeocodeResult>> BatchGeocodeDetailedAsync(
        IReadOnlyList<string> addresses,
        BatchGeocodeOptions? options = null,
        CancellationToken ct = default);
}
