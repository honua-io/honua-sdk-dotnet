// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// Entry point for authoring Honua geoprocessing processes and analysis plans in C#.
/// </summary>
public static class HonuaProcessAuthoring
{
    /// <summary>
    /// Begins authoring a process definition.
    /// </summary>
    /// <param name="processId">Stable process identifier, for example <c>geometry.buffer</c>.</param>
    /// <returns>A fluent <see cref="HonuaProcessBuilder"/>.</returns>
    public static HonuaProcessBuilder DefineProcess(string processId) => new(processId);

    /// <summary>
    /// Begins authoring an analysis plan for the canonical <c>honua-geoprocessing</c> process.
    /// </summary>
    /// <param name="planId">Plan identifier.</param>
    /// <returns>A fluent <see cref="HonuaAnalysisPlanBuilder"/>.</returns>
    public static HonuaAnalysisPlanBuilder DefinePlan(string planId) => new(planId);
}
