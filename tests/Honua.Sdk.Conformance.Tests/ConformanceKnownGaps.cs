namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// Registry of already-tracked <c>honua-server:nightly</c> gaps that the live
/// conformance tier marks <em>known-expected-failing</em>. Each entry references
/// the owning server issue. The live tier skips the affected assertion with the
/// reference rather than letting the job go red, so the harness stays in place
/// and the job stays green — while any <em>new/untracked</em> drift still fails.
/// When a gap lands server-side, remove its entry here and the corresponding
/// assertion becomes a required check.
/// </summary>
public static class ConformanceKnownGaps
{
    /// <summary>FeatureServer / OGC API Features JSONB attribute projection shape (honua-server#1238).</summary>
    public const string FeatureServerOgcJsonbProjection = "honua-server#1238";

    /// <summary>Temporal field query / round-trip (honua-server#1166).</summary>
    public const string Temporal = "honua-server#1166";

    /// <summary>Replica / offline sync surface (honua-server#1167).</summary>
    public const string Replica = "honua-server#1167";

    /// <summary>Analysis list / estimate (honua-server#1237).</summary>
    public const string AnalysisListEstimate = "honua-server#1237";

    /// <summary>
    /// All registered known gaps, keyed by the conformance surface they affect.
    /// Used for the documented summary and to keep the references discoverable.
    /// </summary>
    public static IReadOnlyDictionary<string, string> All { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["featureserver-ogc-jsonb-projection"] = FeatureServerOgcJsonbProjection,
        ["temporal"] = Temporal,
        ["replica"] = Replica,
        ["analysis-list-estimate"] = AnalysisListEstimate,
    };

    /// <summary>
    /// Builds the explicit skip reason recorded against a known-expected-failing
    /// assertion. The reason always names the tracking issue so the skip is never
    /// silent and can be flipped to a required check when the gap lands.
    /// </summary>
    /// <param name="serverIssue">The tracking <c>honua-server#NNNN</c> reference.</param>
    /// <param name="surface">Short description of the conformance surface.</param>
    /// <returns>A human-readable skip reason.</returns>
    public static string SkipReason(string serverIssue, string surface)
        => $"KNOWN-EXPECTED-FAILING ({serverIssue}): {surface}. " +
           "Tracked nightly server gap; flip to a required check when it lands. " +
           "New/untracked drift in other surfaces still fails the conformance gate.";
}
