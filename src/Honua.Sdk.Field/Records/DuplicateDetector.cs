// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Records;

/// <summary>
/// Detects potential duplicate field records by distance and matching field values.
/// </summary>
public sealed class DuplicateDetector
{
    private readonly IFieldRecordDistanceCalculator _distanceCalculator;

    /// <summary>
    /// Initializes a duplicate detector.
    /// </summary>
    /// <param name="distanceCalculator">Optional distance calculator. Defaults to a spherical WGS84 calculator.</param>
    public DuplicateDetector(IFieldRecordDistanceCalculator? distanceCalculator = null)
    {
        _distanceCalculator = distanceCalculator ?? SphericalFieldDistanceCalculator.Instance;
    }

    /// <summary>
    /// Finds existing records that may duplicate a candidate record.
    /// </summary>
    /// <param name="existing">Existing records.</param>
    /// <param name="candidate">Candidate record.</param>
    /// <param name="options">Detection options.</param>
    /// <returns>Potential duplicates.</returns>
    public IReadOnlyList<PotentialDuplicate> FindPotentialDuplicates(
        IEnumerable<FieldRecord> existing,
        FieldRecord candidate,
        DuplicateDetectionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(candidate);

        var resolvedOptions = options ?? new DuplicateDetectionOptions();
        var matches = new List<PotentialDuplicate>();

        foreach (var record in existing)
        {
            if (string.Equals(record.RecordId, candidate.RecordId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var distance = _distanceCalculator.CalculateDistanceMeters(record.Location, candidate.Location);
            if (distance is null || distance.Value > resolvedOptions.MaxDistanceMeters)
            {
                continue;
            }

            var matchedFields = resolvedOptions.MatchFieldIds
                .Where(fieldId =>
                    record.Values.TryGetValue(fieldId, out var existingValue) &&
                    candidate.Values.TryGetValue(fieldId, out var candidateValue) &&
                    string.Equals(existingValue?.ToString(), candidateValue?.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (resolvedOptions.MatchFieldIds.Count > 0 && matchedFields.Length == 0)
            {
                continue;
            }

            matches.Add(new PotentialDuplicate(record.RecordId, distance.Value, matchedFields));
        }

        return matches;
    }
}

/// <summary>
/// Calculates distance between two field record coordinates.
/// </summary>
public interface IFieldRecordDistanceCalculator
{
    /// <summary>
    /// Calculates distance in meters between two points.
    /// </summary>
    /// <param name="left">First point.</param>
    /// <param name="right">Second point.</param>
    /// <returns>Distance in meters, or <see langword="null"/> when either point is absent.</returns>
    double? CalculateDistanceMeters(FieldGeoPoint? left, FieldGeoPoint? right);
}

/// <summary>
/// Spherical WGS84 distance calculator for field duplicate detection.
/// </summary>
public sealed class SphericalFieldDistanceCalculator : IFieldRecordDistanceCalculator
{
    /// <summary>Shared calculator instance.</summary>
    public static SphericalFieldDistanceCalculator Instance { get; } = new();

    /// <summary>
    /// Initializes a spherical WGS84 distance calculator.
    /// </summary>
    public SphericalFieldDistanceCalculator()
    {
    }

    /// <inheritdoc />
    public double? CalculateDistanceMeters(FieldGeoPoint? left, FieldGeoPoint? right)
    {
        if (left is null || right is null)
        {
            return null;
        }

        const double radiusMeters = 6_371_000d;
        var dLat = DegreesToRadians(right.Latitude - left.Latitude);
        var dLon = DegreesToRadians(right.Longitude - left.Longitude);
        var lat1 = DegreesToRadians(left.Latitude);
        var lat2 = DegreesToRadians(right.Latitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return radiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}

/// <summary>
/// Options controlling duplicate detection.
/// </summary>
public sealed record DuplicateDetectionOptions
{
    /// <summary>Maximum distance in meters. Defaults to 15 meters.</summary>
    public double MaxDistanceMeters { get; init; } = 15;

    /// <summary>Field IDs that must match for a duplicate to be reported. Empty means distance only.</summary>
    public IReadOnlyList<string> MatchFieldIds { get; init; } = [];
}

/// <summary>
/// Record identified as a potential duplicate.
/// </summary>
/// <param name="RecordId">Existing record identifier.</param>
/// <param name="DistanceMeters">Distance to the candidate record.</param>
/// <param name="MatchedFieldIds">Field IDs that matched.</param>
public sealed record PotentialDuplicate(string RecordId, double DistanceMeters, IReadOnlyList<string> MatchedFieldIds);
