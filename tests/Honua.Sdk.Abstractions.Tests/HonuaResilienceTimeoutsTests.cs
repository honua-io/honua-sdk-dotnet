// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Tests;

public sealed class HonuaResilienceTimeoutsTests
{
    public static IEnumerable<object[]> BudgetCases()
    {
        yield return [TimeSpan.FromSeconds(1)];
        yield return [TimeSpan.FromSeconds(15)];
        yield return [TimeSpan.FromSeconds(30)];
        yield return [TimeSpan.FromSeconds(100)];
        yield return [TimeSpan.FromMinutes(10)];
        yield return [TimeSpan.FromHours(1)];
        yield return [TimeSpan.FromHours(23)];
    }

    [Theory]
    [MemberData(nameof(BudgetCases))]
    public void DerivedTimeouts_SatisfyStandardResilienceValidatorConstraints(TimeSpan budget)
    {
        var attempt = HonuaResilienceTimeouts.AttemptTimeout(budget);
        var sampling = HonuaResilienceTimeouts.SamplingDuration(budget);
        var total = HonuaResilienceTimeouts.TotalRequestTimeout(budget);

        // The standard resilience handler validates:
        //   1. TotalRequestTimeout > AttemptTimeout
        //   2. CircuitBreaker.SamplingDuration >= 2 * AttemptTimeout
        //   3. TotalRequestTimeout > CircuitBreaker.SamplingDuration
        Assert.True(attempt > TimeSpan.Zero);
        Assert.True(total > attempt, $"total {total} must exceed attempt {attempt}");
        Assert.True(sampling >= attempt + attempt, $"sampling {sampling} must be >= 2x attempt {attempt}");
        Assert.True(total > sampling, $"total {total} must exceed sampling {sampling}");
    }

    [Fact]
    public void AttemptTimeout_ScalesWithBudget_NotPinnedAtFixedCeiling()
    {
        // Regression: a configured 100 s budget must permit a single attempt well
        // beyond the old hard-coded 14 s per-attempt ceiling, and the per-attempt
        // timeout must grow with the budget rather than saturating at a constant.
        var attempt100 = HonuaResilienceTimeouts.AttemptTimeout(TimeSpan.FromSeconds(100));
        var attempt30 = HonuaResilienceTimeouts.AttemptTimeout(TimeSpan.FromSeconds(30));

        Assert.True(attempt100 > TimeSpan.FromSeconds(14), $"attempt for 100 s budget was {attempt100}");
        Assert.True(attempt100 > attempt30, "per-attempt timeout must scale with the overall budget");

        var attemptDay = HonuaResilienceTimeouts.AttemptTimeout(TimeSpan.FromHours(12));
        Assert.True(attemptDay > attempt100, "per-attempt timeout must keep scaling for large budgets");
    }

    [Fact]
    public void TotalRequestTimeout_EqualsConfiguredBudget()
    {
        var budget = TimeSpan.FromSeconds(73);
        Assert.Equal(budget, HonuaResilienceTimeouts.TotalRequestTimeout(budget));
    }
}
