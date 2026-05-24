// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Alert zone create or update request.
/// </summary>
public sealed record AlertZoneRequest
{
    /// <summary>
    /// Service identifier owning the zone.
    /// </summary>
    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; init; }

    /// <summary>
    /// Zone name.
    /// </summary>
    [JsonPropertyName("zoneName")]
    public required string ZoneName { get; init; }

    /// <summary>
    /// Zone geometry in WKT.
    /// </summary>
    [JsonPropertyName("wkt")]
    public string? Wkt { get; init; }

    /// <summary>
    /// Geometry SRID.
    /// </summary>
    [JsonPropertyName("srid")]
    public int? Srid { get; init; }

    /// <summary>
    /// Optional non-secret metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string?>? Metadata { get; init; }

    /// <summary>
    /// Whether the zone is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Alert zone response.
/// </summary>
public sealed class AlertZoneResponse
{
    /// <summary>
    /// Zone identifier.
    /// </summary>
    [JsonPropertyName("zoneId")]
    public long ZoneId { get; init; }

    /// <summary>
    /// Service identifier owning the zone.
    /// </summary>
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>
    /// Zone name.
    /// </summary>
    [JsonPropertyName("zoneName")]
    public string ZoneName { get; init; } = string.Empty;

    /// <summary>
    /// Zone geometry in WKT.
    /// </summary>
    [JsonPropertyName("wkt")]
    public string? Wkt { get; init; }

    /// <summary>
    /// Geometry SRID.
    /// </summary>
    [JsonPropertyName("srid")]
    public int? Srid { get; init; }

    /// <summary>
    /// Zone metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string?> Metadata { get; init; } = [];

    /// <summary>
    /// Whether the zone is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}

/// <summary>
/// Alert rule create or update request.
/// </summary>
public sealed record AlertRuleRequest
{
    /// <summary>
    /// Service identifier targeted by the rule.
    /// </summary>
    [JsonPropertyName("serviceId")]
    public required string ServiceId { get; init; }

    /// <summary>
    /// Layer identifier targeted by the rule.
    /// </summary>
    [JsonPropertyName("layerId")]
    public int LayerId { get; init; }

    /// <summary>
    /// Optional zone identifier.
    /// </summary>
    [JsonPropertyName("zoneId")]
    public long? ZoneId { get; init; }

    /// <summary>
    /// Rule name.
    /// </summary>
    [JsonPropertyName("ruleName")]
    public required string RuleName { get; init; }

    /// <summary>
    /// Trigger type: enter, exit, dwell, or threshold.
    /// </summary>
    [JsonPropertyName("triggerType")]
    public required string TriggerType { get; init; }

    /// <summary>
    /// Rule conditions serialized as JSON.
    /// </summary>
    [JsonPropertyName("conditionsJson")]
    public string ConditionsJson { get; init; } = "{}";

    /// <summary>
    /// Cooldown in seconds.
    /// </summary>
    [JsonPropertyName("cooldownSeconds")]
    public int CooldownSeconds { get; init; }

    /// <summary>
    /// Severity: info, warning, or critical.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = "warning";

    /// <summary>
    /// Required edition: pro or enterprise.
    /// </summary>
    [JsonPropertyName("editionRequired")]
    public string EditionRequired { get; init; } = "pro";

    /// <summary>
    /// Delivery channels.
    /// </summary>
    [JsonPropertyName("channels")]
    public IReadOnlyList<string>? Channels { get; init; }

    /// <summary>
    /// Whether the rule is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// Alert rule response.
/// </summary>
public sealed class AlertRuleResponse
{
    /// <summary>
    /// Rule identifier.
    /// </summary>
    [JsonPropertyName("ruleId")]
    public long RuleId { get; init; }

    /// <summary>
    /// Service identifier targeted by the rule.
    /// </summary>
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>
    /// Layer identifier targeted by the rule.
    /// </summary>
    [JsonPropertyName("layerId")]
    public int LayerId { get; init; }

    /// <summary>
    /// Optional zone identifier.
    /// </summary>
    [JsonPropertyName("zoneId")]
    public long? ZoneId { get; init; }

    /// <summary>
    /// Rule name.
    /// </summary>
    [JsonPropertyName("ruleName")]
    public string RuleName { get; init; } = string.Empty;

    /// <summary>
    /// Trigger type.
    /// </summary>
    [JsonPropertyName("triggerType")]
    public string TriggerType { get; init; } = string.Empty;

    /// <summary>
    /// Rule conditions serialized as JSON.
    /// </summary>
    [JsonPropertyName("conditionsJson")]
    public string ConditionsJson { get; init; } = "{}";

    /// <summary>
    /// Cooldown in seconds.
    /// </summary>
    [JsonPropertyName("cooldownSeconds")]
    public int CooldownSeconds { get; init; }

    /// <summary>
    /// Severity.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    /// <summary>
    /// Required edition.
    /// </summary>
    [JsonPropertyName("editionRequired")]
    public string EditionRequired { get; init; } = string.Empty;

    /// <summary>
    /// Delivery channels.
    /// </summary>
    [JsonPropertyName("channels")]
    public IReadOnlyList<string> Channels { get; init; } = [];

    /// <summary>
    /// Whether the rule is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
