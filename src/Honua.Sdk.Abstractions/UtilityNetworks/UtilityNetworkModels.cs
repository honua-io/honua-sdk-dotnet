// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.UtilityNetworks;

/// <summary>
/// Utility-network trace workflow kind.
/// </summary>
public enum UtilityNetworkTraceType
{
    /// <summary>Trace type is not specified or is provider-defined.</summary>
    Unspecified = 0,

    /// <summary>Connected trace workflow.</summary>
    Connected = 1,

    /// <summary>Upstream trace workflow.</summary>
    Upstream = 2,

    /// <summary>Downstream trace workflow.</summary>
    Downstream = 3,

    /// <summary>Subnetwork trace workflow.</summary>
    Subnetwork = 4,

    /// <summary>Provider-specific trace workflow not covered by the canonical values.</summary>
    Custom = 5
}

/// <summary>
/// Utility-network element category.
/// </summary>
public enum UtilityNetworkElementKind
{
    /// <summary>Element kind is unknown.</summary>
    Unknown = 0,

    /// <summary>Junction or point-like network element.</summary>
    Junction = 1,

    /// <summary>Edge or line-like network element.</summary>
    Edge = 2,

    /// <summary>Device element.</summary>
    Device = 3,

    /// <summary>Line element.</summary>
    Line = 4,

    /// <summary>Assembly element.</summary>
    Assembly = 5,

    /// <summary>Container element.</summary>
    Container = 6,

    /// <summary>Structure junction element.</summary>
    StructureJunction = 7,

    /// <summary>Structure line element.</summary>
    StructureLine = 8,

    /// <summary>Subnetwork controller or subnetwork summary element.</summary>
    Subnetwork = 9
}

/// <summary>
/// Utility-network association category.
/// </summary>
public enum UtilityNetworkAssociationKind
{
    /// <summary>Association kind is unknown.</summary>
    Unknown = 0,

    /// <summary>Connectivity association between two elements.</summary>
    Connectivity = 1,

    /// <summary>Containment association between a container and content.</summary>
    Containment = 2,

    /// <summary>Structural attachment association.</summary>
    StructuralAttachment = 3
}

/// <summary>
/// Utility-network trace barrier category.
/// </summary>
public enum UtilityNetworkTraceBarrierKind
{
    /// <summary>Barrier kind is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Barrier stops traversal at a specific element.</summary>
    Traversability = 1,

    /// <summary>Barrier filters results without necessarily stopping traversal.</summary>
    Filter = 2,

    /// <summary>Barrier is created from a provider-side condition or function.</summary>
    Function = 3
}

/// <summary>
/// Utility-network trace condition operator.
/// </summary>
public enum UtilityNetworkTraceConditionOperator
{
    /// <summary>Operator is not specified.</summary>
    Unspecified = 0,

    /// <summary>Left value equals the right value.</summary>
    Equal = 1,

    /// <summary>Left value does not equal the right value.</summary>
    NotEqual = 2,

    /// <summary>Left value is greater than the right value.</summary>
    GreaterThan = 3,

    /// <summary>Left value is greater than or equal to the right value.</summary>
    GreaterThanOrEqual = 4,

    /// <summary>Left value is less than the right value.</summary>
    LessThan = 5,

    /// <summary>Left value is less than or equal to the right value.</summary>
    LessThanOrEqual = 6,

    /// <summary>Left value is included in a provider-defined set.</summary>
    IncludesAny = 7,

    /// <summary>Left value excludes a provider-defined set.</summary>
    ExcludesAll = 8
}

/// <summary>
/// Utility-network trace message severity.
/// </summary>
public enum UtilityNetworkTraceMessageSeverity
{
    /// <summary>Informational trace message.</summary>
    Information = 0,

    /// <summary>Trace warning that does not block the result.</summary>
    Warning = 1,

    /// <summary>Trace error reported by the provider.</summary>
    Error = 2
}

/// <summary>
/// Utility-network source identifiers used by trace requests.
/// </summary>
public sealed record UtilityNetworkSource
{
    /// <summary>Honua service identifier or provider network service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Provider network identifier.</summary>
    public string? NetworkId { get; init; }

    /// <summary>Provider network display or lookup name.</summary>
    public string? NetworkName { get; init; }

    /// <summary>Optional branch or version name used by version-aware providers.</summary>
    public string? VersionName { get; init; }

    /// <summary>Additional provider-neutral feature source identifiers.</summary>
    public FeatureSource? FeatureSource { get; init; }
}

/// <summary>
/// Provider utility-network trace capabilities.
/// </summary>
public sealed record UtilityNetworkTraceCapabilities
{
    /// <summary>Whether connected traces are supported.</summary>
    public bool SupportsConnectedTrace { get; init; }

    /// <summary>Whether upstream traces are supported.</summary>
    public bool SupportsUpstreamTrace { get; init; }

    /// <summary>Whether downstream traces are supported.</summary>
    public bool SupportsDownstreamTrace { get; init; }

    /// <summary>Whether subnetwork traces are supported.</summary>
    public bool SupportsSubnetworkTrace { get; init; }

    /// <summary>Whether named trace configurations can be discovered or referenced.</summary>
    public bool SupportsNamedTraceConfigurations { get; init; }

    /// <summary>Whether terminal-specific starting points and barriers are supported.</summary>
    public bool SupportsTerminals { get; init; }

    /// <summary>Whether association details can be returned.</summary>
    public bool SupportsAssociations { get; init; }

    /// <summary>Whether barriers can be sent with trace requests.</summary>
    public bool SupportsBarriers { get; init; }

    /// <summary>Whether provider result payloads can include geometry data.</summary>
    public bool SupportsResultGeometry { get; init; }

    /// <summary>Native provider surface backing the implementation.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason the capability set is unavailable, when applicable.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Lightweight reference to a utility-network element.
/// </summary>
public sealed record UtilityNetworkElementReference
{
    /// <summary>Stable provider or SDK element identifier.</summary>
    public required string ElementId { get; init; }

    /// <summary>Provider network source identifier.</summary>
    public string? NetworkSourceId { get; init; }

    /// <summary>Provider network source name.</summary>
    public string? NetworkSourceName { get; init; }

    /// <summary>Feature object identifier when the element maps to a feature row.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Feature global identifier when the element maps to a feature row.</summary>
    public string? GlobalId { get; init; }

    /// <summary>Terminal identifier used for terminal-specific traces.</summary>
    public string? TerminalId { get; init; }

    /// <summary>Provider-neutral feature source identifiers for the referenced element.</summary>
    public FeatureSource? FeatureSource { get; init; }
}

/// <summary>
/// Utility-network element returned by a trace result.
/// </summary>
public sealed record UtilityNetworkElement
{
    /// <summary>Stable provider or SDK element identifier.</summary>
    public required string ElementId { get; init; }

    /// <summary>Element category.</summary>
    public UtilityNetworkElementKind Kind { get; init; } = UtilityNetworkElementKind.Unknown;

    /// <summary>Provider network source identifier.</summary>
    public string? NetworkSourceId { get; init; }

    /// <summary>Provider network source name.</summary>
    public string? NetworkSourceName { get; init; }

    /// <summary>Feature object identifier when the element maps to a feature row.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Feature global identifier when the element maps to a feature row.</summary>
    public string? GlobalId { get; init; }

    /// <summary>Asset group code or name advertised by the provider.</summary>
    public string? AssetGroup { get; init; }

    /// <summary>Asset type code or name advertised by the provider.</summary>
    public string? AssetType { get; init; }

    /// <summary>Terminal identifier used when the trace result is terminal-specific.</summary>
    public string? TerminalId { get; init; }

    /// <summary>Terminal display or lookup name used when the trace result is terminal-specific.</summary>
    public string? TerminalName { get; init; }

    /// <summary>Provider-neutral feature source identifiers for this element.</summary>
    public FeatureSource? FeatureSource { get; init; }

    /// <summary>Optional geometry data returned by the provider. Rendering is owned by downstream viewers.</summary>
    public JsonElement? Geometry { get; init; }

    /// <summary>Provider or SDK attributes associated with the element.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Terminal metadata for utility-network elements.
/// </summary>
public sealed record UtilityNetworkTerminal
{
    /// <summary>Stable terminal identifier.</summary>
    public required string TerminalId { get; init; }

    /// <summary>Terminal display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Element that owns the terminal, when known.</summary>
    public UtilityNetworkElementReference? Element { get; init; }

    /// <summary>Whether this terminal is the provider default for the owning element.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Provider or SDK attributes associated with the terminal.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Association between two utility-network elements.
/// </summary>
public sealed record UtilityNetworkAssociation
{
    /// <summary>Stable association identifier.</summary>
    public required string AssociationId { get; init; }

    /// <summary>Association category.</summary>
    public UtilityNetworkAssociationKind Kind { get; init; } = UtilityNetworkAssociationKind.Unknown;

    /// <summary>First endpoint of the association.</summary>
    public required UtilityNetworkElementReference FromElement { get; init; }

    /// <summary>Second endpoint of the association.</summary>
    public required UtilityNetworkElementReference ToElement { get; init; }

    /// <summary>Provider or SDK attributes associated with the association.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Utility-network trace condition used by configuration and barrier rules.
/// </summary>
public sealed record UtilityNetworkTraceCondition
{
    /// <summary>Provider network attribute, category, or function name.</summary>
    public required string Name { get; init; }

    /// <summary>Condition comparison operator.</summary>
    public UtilityNetworkTraceConditionOperator Operator { get; init; } = UtilityNetworkTraceConditionOperator.Unspecified;

    /// <summary>Comparison value, when the provider condition requires one.</summary>
    public JsonElement? Value { get; init; }

    /// <summary>Whether the condition should be negated.</summary>
    public bool Negate { get; init; }
}

/// <summary>
/// Inline trace configuration shared by all utility-network trace workflows.
/// </summary>
public sealed record UtilityNetworkTraceConfiguration
{
    /// <summary>Trace workflow kind represented by the configuration.</summary>
    public UtilityNetworkTraceType TraceType { get; init; } = UtilityNetworkTraceType.Unspecified;

    /// <summary>Provider domain network name.</summary>
    public string? DomainNetwork { get; init; }

    /// <summary>Provider source tier name.</summary>
    public string? Tier { get; init; }

    /// <summary>Provider target tier name used by tier-aware traces.</summary>
    public string? TargetTier { get; init; }

    /// <summary>Subnetwork name used by subnetwork traces.</summary>
    public string? SubnetworkName { get; init; }

    /// <summary>Traversal conditions applied by the provider.</summary>
    public IReadOnlyList<UtilityNetworkTraceCondition> TraversabilityConditions { get; init; } = [];

    /// <summary>Filter conditions applied to returned results.</summary>
    public IReadOnlyList<UtilityNetworkTraceCondition> FilterConditions { get; init; } = [];

    /// <summary>Network attribute names requested in the trace result.</summary>
    public IReadOnlyList<string> OutputNetworkAttributes { get; init; } = [];

    /// <summary>Whether container elements should be included when the provider supports them.</summary>
    public bool IncludeContainers { get; init; }

    /// <summary>Whether contained content should be included when the provider supports it.</summary>
    public bool IncludeContent { get; init; }

    /// <summary>Whether structure elements should be included when the provider supports them.</summary>
    public bool IncludeStructures { get; init; }

    /// <summary>Whether the provider should validate network topology consistency before tracing.</summary>
    public bool ValidateConsistency { get; init; }

    /// <summary>Additional provider parameters for server-specific trace extensions.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderParameters { get; init; }
}

/// <summary>
/// Named trace configuration advertised by a utility-network provider.
/// </summary>
public sealed record UtilityNetworkNamedTraceConfiguration
{
    /// <summary>Stable configuration identifier.</summary>
    public required string ConfigurationId { get; init; }

    /// <summary>Configuration display or lookup name.</summary>
    public required string Name { get; init; }

    /// <summary>Configuration description.</summary>
    public string? Description { get; init; }

    /// <summary>Trace workflow kind represented by this named configuration.</summary>
    public UtilityNetworkTraceType TraceType { get; init; } = UtilityNetworkTraceType.Unspecified;

    /// <summary>Inline configuration details, when the provider exposes them.</summary>
    public UtilityNetworkTraceConfiguration? Configuration { get; init; }

    /// <summary>Whether this is the provider default for its trace workflow or tier.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Provider or SDK metadata associated with the named configuration.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Metadata { get; init; }
}

/// <summary>
/// Query used to discover named utility-network trace configurations.
/// </summary>
public sealed record UtilityNetworkTraceConfigurationQuery
{
    /// <summary>Utility-network source identifiers.</summary>
    public UtilityNetworkSource Source { get; init; } = new();

    /// <summary>Optional trace workflow kind filter.</summary>
    public UtilityNetworkTraceType? TraceType { get; init; }

    /// <summary>Optional provider domain network name filter.</summary>
    public string? DomainNetwork { get; init; }

    /// <summary>Optional provider tier name filter.</summary>
    public string? Tier { get; init; }

    /// <summary>Whether inactive or hidden provider configurations should be included.</summary>
    public bool IncludeInactive { get; init; }
}

/// <summary>
/// Starting point for a utility-network trace.
/// </summary>
public sealed record UtilityNetworkTraceStartingPoint
{
    /// <summary>Trace starting element.</summary>
    public required UtilityNetworkElementReference Element { get; init; }

    /// <summary>Optional terminal identifier overriding the element reference terminal.</summary>
    public string? TerminalId { get; init; }

    /// <summary>Optional percentage along an edge element.</summary>
    public double? PercentAlong { get; init; }

    /// <summary>Provider or SDK attributes associated with the starting point.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Barrier used by a utility-network trace.
/// </summary>
public sealed record UtilityNetworkTraceBarrier
{
    /// <summary>Barrier element.</summary>
    public required UtilityNetworkElementReference Element { get; init; }

    /// <summary>Barrier category.</summary>
    public UtilityNetworkTraceBarrierKind Kind { get; init; } = UtilityNetworkTraceBarrierKind.Traversability;

    /// <summary>Optional terminal identifier overriding the element reference terminal.</summary>
    public string? TerminalId { get; init; }

    /// <summary>Optional percentage along an edge element.</summary>
    public double? PercentAlong { get; init; }

    /// <summary>Provider or SDK attributes associated with the barrier.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Utility-network trace request shared by connected, upstream, downstream, and subnetwork traces.
/// </summary>
public sealed record UtilityNetworkTraceRequest
{
    /// <summary>Utility-network source identifiers.</summary>
    public UtilityNetworkSource Source { get; init; } = new();

    /// <summary>Trace starting points.</summary>
    public required IReadOnlyList<UtilityNetworkTraceStartingPoint> StartingPoints { get; init; }

    /// <summary>Named provider trace configuration identifier.</summary>
    public string? NamedConfigurationId { get; init; }

    /// <summary>Inline trace configuration for providers that support caller-supplied settings.</summary>
    public UtilityNetworkTraceConfiguration? Configuration { get; init; }

    /// <summary>Trace barriers.</summary>
    public IReadOnlyList<UtilityNetworkTraceBarrier> Barriers { get; init; } = [];

    /// <summary>Whether trace result elements should be returned.</summary>
    public bool ReturnElements { get; init; } = true;

    /// <summary>Whether trace result associations should be returned.</summary>
    public bool ReturnAssociations { get; init; } = true;

    /// <summary>Whether trace result terminals should be returned.</summary>
    public bool ReturnTerminals { get; init; } = true;

    /// <summary>Whether provider geometry data should be returned for trace result elements.</summary>
    public bool ReturnGeometry { get; init; }

    /// <summary>Additional provider parameters for server-specific trace extensions.</summary>
    public IReadOnlyDictionary<string, JsonElement>? ProviderParameters { get; init; }
}

/// <summary>
/// Subnetwork summary returned by a utility-network trace.
/// </summary>
public sealed record UtilityNetworkSubnetworkResult
{
    /// <summary>Subnetwork display or lookup name.</summary>
    public required string SubnetworkName { get; init; }

    /// <summary>Provider domain network name.</summary>
    public string? DomainNetwork { get; init; }

    /// <summary>Provider tier name.</summary>
    public string? Tier { get; init; }

    /// <summary>Subnetwork controller elements returned by the provider.</summary>
    public IReadOnlyList<UtilityNetworkElementReference> Controllers { get; init; } = [];

    /// <summary>Provider or SDK attributes associated with the subnetwork.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Provider message returned with a utility-network trace result.
/// </summary>
public sealed record UtilityNetworkTraceMessage
{
    /// <summary>Message severity.</summary>
    public UtilityNetworkTraceMessageSeverity Severity { get; init; } = UtilityNetworkTraceMessageSeverity.Information;

    /// <summary>Provider or SDK message code.</summary>
    public string? Code { get; init; }

    /// <summary>Human-readable message text.</summary>
    public required string Message { get; init; }

    /// <summary>Element associated with the message, when applicable.</summary>
    public UtilityNetworkElementReference? Element { get; init; }
}

/// <summary>
/// Utility-network trace result data. Display and map rendering are downstream responsibilities.
/// </summary>
public sealed record UtilityNetworkTraceResult
{
    /// <summary>Trace workflow kind that produced this result.</summary>
    public UtilityNetworkTraceType TraceType { get; init; } = UtilityNetworkTraceType.Unspecified;

    /// <summary>Provider trace identifier.</summary>
    public string? TraceId { get; init; }

    /// <summary>Named configuration identifier used by the trace, when applicable.</summary>
    public string? ConfigurationId { get; init; }

    /// <summary>Whether the provider completed the trace successfully.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Elements returned by the trace.</summary>
    public IReadOnlyList<UtilityNetworkElement> Elements { get; init; } = [];

    /// <summary>Associations returned by the trace.</summary>
    public IReadOnlyList<UtilityNetworkAssociation> Associations { get; init; } = [];

    /// <summary>Terminals returned by the trace.</summary>
    public IReadOnlyList<UtilityNetworkTerminal> Terminals { get; init; } = [];

    /// <summary>Subnetwork summaries returned by the trace.</summary>
    public IReadOnlyList<UtilityNetworkSubnetworkResult> Subnetworks { get; init; } = [];

    /// <summary>Provider messages returned with the trace.</summary>
    public IReadOnlyList<UtilityNetworkTraceMessage> Messages { get; init; } = [];

    /// <summary>Provider or SDK scalar outputs associated with the trace.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Outputs { get; init; }

    /// <summary>Raw provider response, when the implementation exposes one.</summary>
    public JsonElement? RawResponse { get; init; }
}
