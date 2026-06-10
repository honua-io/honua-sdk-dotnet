namespace Honua.Sdk.Conformance.Tests;

/// <summary>
/// A fact in the live conformance tier. Skips with a clear reason when no
/// Testcontainers/external server is configured. When <paramref name="knownGap"/>
/// is supplied, the fact is marked <em>known-expected-failing</em> against the
/// referenced server issue: it is skipped with an explicit reference (never
/// silently, never via blanket continue-on-error) so the job stays green and the
/// harness is in place while the gap is tracked. New/untracked drift in other
/// facts still fails the gate. Remove the <paramref name="knownGap"/> argument to
/// flip the fact to a required check once the server gap lands.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LiveConformanceFactAttribute : FactAttribute
{
    public LiveConformanceFactAttribute(string? knownGap = null, string surface = "")
    {
        Surface = surface;
        var skip = LiveConformanceOptions.GetSkipReason();
        if (skip is not null)
        {
            Skip = skip;
            return;
        }

        if (!string.IsNullOrEmpty(knownGap))
        {
            KnownGap = knownGap;
            Skip = ConformanceKnownGaps.SkipReason(knownGap, surface);
        }
    }

    /// <summary>The tracking server issue when this fact is a known gap, else null.</summary>
    public string? KnownGap { get; }

    /// <summary>Short description of the conformance surface this fact exercises.</summary>
    public string Surface { get; }
}
