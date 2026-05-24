// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Alert zone and rule management client.
/// </summary>
public interface IHonuaAdminAlertsClient
{
    /// <summary>
    /// Lists alert zones.
    /// </summary>
    Task<IReadOnlyList<AlertZoneResponse>> ListAlertZonesAsync(string? serviceId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an alert zone.
    /// </summary>
    Task<AlertZoneResponse> CreateAlertZoneAsync(AlertZoneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an alert zone.
    /// </summary>
    Task<AlertZoneResponse> UpdateAlertZoneAsync(long zoneId, AlertZoneRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an alert zone.
    /// </summary>
    Task DeleteAlertZoneAsync(long zoneId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists alert rules.
    /// </summary>
    Task<IReadOnlyList<AlertRuleResponse>> ListAlertRulesAsync(string? serviceId = null, int? layerId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an alert rule.
    /// </summary>
    Task<AlertRuleResponse> CreateAlertRuleAsync(AlertRuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an alert rule.
    /// </summary>
    Task<AlertRuleResponse> UpdateAlertRuleAsync(long ruleId, AlertRuleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an alert rule.
    /// </summary>
    Task DeleteAlertRuleAsync(long ruleId, CancellationToken cancellationToken = default);
}
