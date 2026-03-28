// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Features.FeatureServer.Models;

/// <summary>
/// Parameters for FeatureServer statistics queries.
/// </summary>
public sealed record FeatureServerStatisticsParams
{
    /// <summary>SQL WHERE clause to filter features before computing statistics.</summary>
    public string? Where { get; init; }

    /// <summary>
    /// Statistics definitions as JSON array.
    /// Each element: <c>{ "statisticType": "count|sum|min|max|avg|stddev|var", "onStatisticField": "FIELD", "outStatisticFieldName": "ALIAS" }</c>.
    /// </summary>
    public string? OutStatistics { get; init; }

    /// <summary>Comma-separated list of fields to group by.</summary>
    public string? GroupByFieldsForStatistics { get; init; }

    /// <summary>SQL HAVING clause for filtering grouped results.</summary>
    public string? Having { get; init; }

    /// <summary>Fields to order results by.</summary>
    public string? OrderByFields { get; init; }

    /// <summary>Number of records to skip.</summary>
    public int? ResultOffset { get; init; }

    /// <summary>Maximum number of records to return.</summary>
    public int? ResultRecordCount { get; init; }
}
