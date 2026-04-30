// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class AdvancedEditingContractTests
{
    [Fact]
    public void SourceSchema_CarriesStructuredEditingRuleMetadata()
    {
        var domain = StatusDomain();
        var relationship = InspectionRelationship();

        var schema = new SourceSchema
        {
            Fields =
            [
                new SourceField
                {
                    Name = "status",
                    Alias = "Status",
                    Type = "esriFieldTypeString",
                    Editable = true,
                    Required = true,
                    DomainInfo = domain,
                },
            ],
            EditingRules = new FeatureEditingRulesMetadata
            {
                Source = ParksSource(),
                FieldDomains = [domain],
                ContingentValues =
                [
                    new FeatureContingentValueSet
                    {
                        ContingencyId = "status-condition",
                        FieldGroupName = "status-workflow",
                        Values =
                        [
                            new FeatureContingentFieldValue
                            {
                                FieldName = "status",
                                Value = JsonValue("\"open\""),
                            },
                            new FeatureContingentFieldValue
                            {
                                FieldName = "condition",
                                Value = JsonValue("\"good\""),
                            },
                        ],
                    },
                ],
                AttributeRules =
                [
                    new FeatureAttributeRule
                    {
                        RuleId = "status-required",
                        Name = "Status is required",
                        Type = FeatureAttributeRuleType.Constraint,
                        FieldName = "status",
                        Triggers = [FeatureAttributeRuleTrigger.Insert, FeatureAttributeRuleTrigger.Update],
                        ExpressionLanguage = "arcade",
                        ErrorMessage = "Status is required.",
                    },
                ],
                Relationships = [relationship],
                Versioning = new FeatureEditVersioningCapabilities
                {
                    SupportsVersionName = true,
                    SupportsBranchVersioning = true,
                    SupportsEditSessions = true,
                    DefaultVersionName = "sde.DEFAULT",
                },
            },
        };

        var editingRules = Assert.IsType<FeatureEditingRulesMetadata>(schema.EditingRules);
        var versioning = Assert.IsType<FeatureEditVersioningCapabilities>(editingRules.Versioning);

        Assert.Equal("status", schema.Fields[0].DomainInfo?.FieldName);
        Assert.Equal(FeatureFieldDomainType.CodedValue, editingRules.FieldDomains[0].Type);
        Assert.Equal("status-required", editingRules.AttributeRules[0].RuleId);
        Assert.Equal(FeatureRelationshipCardinality.OneToMany, editingRules.Relationships[0].Cardinality);
        Assert.True(versioning.SupportsEditSessions);
    }

    [Fact]
    public async Task Interface_ModelsValidationAndVersionedEditSessions()
    {
        var client = new FakeEditingRulesClient();
        var session = await client.StartEditSessionAsync(new FeatureEditSessionStartRequest
        {
            Source = ParksSource(),
            VersionName = "field-review",
            ParentVersionName = "sde.DEFAULT",
            AcquireLock = true,
        });
        var metadata = await client.GetEditingRulesAsync(new FeatureEditingRulesRequest
        {
            Source = ParksSource(),
            VersionName = session.VersionName,
        });
        var validation = await client.ValidateEditsAsync(new FeatureEditValidationRequest
        {
            Source = ParksSource(),
            Session = session,
            Mode = FeatureEditValidationMode.ClientAndServer,
            Adds =
            [
                new FeatureEditFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["status"] = JsonValue("\"retired\""),
                    },
                },
            ],
        });
        await client.RollbackEditSessionAsync(new FeatureEditSessionCompleteRequest
        {
            Session = session,
        });

        Assert.Equal("fake-edit-rules", client.ProviderName);
        Assert.True(client.EditCapabilities.SupportsEditingRuleMetadata);
        Assert.True(client.EditCapabilities.SupportsVersionedEditSessions);
        Assert.Equal("field-review", session.VersionName);
        Assert.Single(metadata.FieldDomains);
        Assert.False(validation.IsValid);
        var finding = Assert.Single(validation.Results);
        Assert.Equal("status", finding.FieldName);
        Assert.Equal("status-required", finding.RuleId);
        Assert.Equal(FeatureEditValidationSeverity.Blocking, finding.Severity);
        Assert.Equal("Choose an active status.", finding.SuggestedFix);
    }

    [Fact]
    public void EditResponse_TreatsBlockingValidationAsUnsuccessful()
    {
        var response = new FeatureEditResponse
        {
            ProviderName = "test",
            AddResults =
            [
                new FeatureEditResult
                {
                    Succeeded = true,
                },
            ],
            ValidationResults =
            [
                new FeatureEditValidationResult
                {
                    FieldName = "status",
                    RuleId = "status-required",
                    Severity = FeatureEditValidationSeverity.Blocking,
                    Message = "Status is required.",
                    SuggestedFix = "Choose an active status.",
                },
            ],
        };

        Assert.False(response.Succeeded);
        Assert.True(response.ValidationResults[0].BlocksApply);
    }

    private static FeatureFieldDomain StatusDomain()
        => new()
        {
            DomainId = "status-domain",
            Name = "Status",
            FieldName = "status",
            Type = FeatureFieldDomainType.CodedValue,
            CodedValues =
            [
                new FeatureFieldDomainCode
                {
                    Value = JsonValue("\"open\""),
                    Label = "Open",
                },
                new FeatureFieldDomainCode
                {
                    Value = JsonValue("\"closed\""),
                    Label = "Closed",
                },
            ],
        };

    private static FeatureRelationshipClassDescriptor InspectionRelationship()
        => new()
        {
            RelationshipId = "parks-inspections",
            Name = "Park inspections",
            OriginSource = ParksSource(),
            DestinationSource = new FeatureSource { ServiceId = "inspections", LayerId = 1 },
            Cardinality = FeatureRelationshipCardinality.OneToMany,
            Type = FeatureRelationshipType.Composite,
            Keys =
            [
                new FeatureRelationshipKey
                {
                    OriginField = "globalid",
                    DestinationField = "park_globalid",
                },
            ],
            SupportsQueryRelated = true,
            SupportsEditRelated = true,
        };

    private static FeatureSource ParksSource()
        => new()
        {
            ServiceId = "parks",
            LayerId = 0,
        };

    private static JsonElement JsonValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class FakeEditingRulesClient : IHonuaFeatureEditingRulesClient
    {
        public string ProviderName => "fake-edit-rules";

        public FeatureEditCapabilities EditCapabilities { get; } = new()
        {
            SupportsAdds = true,
            SupportsUpdates = true,
            SupportsRollbackOnFailure = true,
            SupportsEditingRuleMetadata = true,
            SupportsValidateOnly = true,
            SupportsContingentValues = true,
            SupportsAttributeRules = true,
            SupportsVersionedEditSessions = true,
            NativeSurface = "test",
        };

        public Task<FeatureEditingRulesMetadata> GetEditingRulesAsync(
            FeatureEditingRulesRequest request,
            CancellationToken ct = default)
            => Task.FromResult(new FeatureEditingRulesMetadata
            {
                Source = request.Source,
                FieldDomains = [StatusDomain()],
                Relationships = [InspectionRelationship()],
                Versioning = new FeatureEditVersioningCapabilities
                {
                    SupportsVersionName = true,
                    SupportsBranchVersioning = true,
                    SupportsEditSessions = true,
                    DefaultVersionName = "sde.DEFAULT",
                },
            });

        public Task<FeatureEditValidationResponse> ValidateEditsAsync(
            FeatureEditValidationRequest request,
            CancellationToken ct = default)
            => Task.FromResult(new FeatureEditValidationResponse
            {
                ProviderName = ProviderName,
                Results =
                [
                    new FeatureEditValidationResult
                    {
                        FieldName = "status",
                        RuleId = "status-required",
                        RuleName = "Status is required",
                        Severity = FeatureEditValidationSeverity.Blocking,
                        Message = "Status value is not active.",
                        SuggestedFix = "Choose an active status.",
                        Operation = "add",
                    },
                ],
            });

        public Task<FeatureEditSession> StartEditSessionAsync(
            FeatureEditSessionStartRequest request,
            CancellationToken ct = default)
            => Task.FromResult(new FeatureEditSession
            {
                SessionId = "session-1",
                VersionName = request.VersionName,
                ParentVersionName = request.ParentVersionName,
                StartedAt = new DateTimeOffset(2026, 4, 30, 0, 0, 0, TimeSpan.Zero),
                StateToken = "state-1",
            });

        public Task CommitEditSessionAsync(
            FeatureEditSessionCompleteRequest request,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RollbackEditSessionAsync(
            FeatureEditSessionCompleteRequest request,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
