// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class MigrationToolkitTests
{
    [Fact]
    public async Task ScanMigrationSourceAsync_PostsRequestAndReadsRawInventoryArtifact()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/admin/import/scan", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return TestHelpers.CreateRawJsonResponse(CreateInventoryArtifact());
        });

        var result = await client.ScanMigrationSourceAsync(new MigrationInventoryScanRequest
        {
            SourceKind = "geoserver",
            SourceUrl = "https://example.com/geoserver/rest",
            Username = "admin",
            Password = "geoserver",
            IncludeStyleContent = true
        });

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("geoserver", sent.RootElement.GetProperty("sourceKind").GetString());
        Assert.Equal("https://example.com/geoserver/rest", sent.RootElement.GetProperty("sourceUrl").GetString());
        Assert.True(sent.RootElement.GetProperty("includeStyleContent").GetBoolean());

        Assert.Equal(MigrationSourceInventoryArtifact.CurrentArtifactKind, result.ArtifactKind);
        Assert.Equal(MigrationSourceInventoryArtifact.CurrentArtifactVersion, result.ArtifactVersion);
        Assert.Equal("geoserver-rest", result.SourceKind);
        Assert.Equal("GeoServer", result.Source.Product);
        Assert.Equal("complete", result.ScanCompleteness.Status);
        Assert.Equal("STATUS", Assert.Single(result.Resources).Fields.Single().Name);
        Assert.Equal(4326, result.Resources.Single().SpatialReferences.Single().Srid);
    }

    [Fact]
    public async Task ScanMigrationSourceAsync_WithExportJsonAddsQuery()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/scan?export=json", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(CreateInventoryArtifact()));
        });

        var result = await client.ScanMigrationSourceAsync(
            new MigrationInventoryScanRequest
            {
                SourceKind = "geoservices",
                SourceUrl = "https://example.com/arcgis/rest/services/Parcels/FeatureServer"
            },
            exportJson: true);

        Assert.Equal("complete", result.ScanCompleteness.Status);
    }

    [Fact]
    public async Task ScanMigrationSourceAsync_AllowsSuccessfulFailedCompletenessArtifact()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/scan", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(CreateInventoryArtifact(status: "failed")));
        });

        var result = await client.ScanMigrationSourceAsync(new MigrationInventoryScanRequest
        {
            SourceKind = "geoserver",
            SourceUrl = "https://example.com/geoserver/rest"
        });

        Assert.Equal("failed", result.ScanCompleteness.Status);
        Assert.Contains("source timed out", result.ScanCompleteness.Warnings);
        Assert.Equal("incompatible", result.OverallCompatibility.Level);
    }

    [Fact]
    public async Task ScanMigrationSourceAsync_ExtensionCallsConcreteAdminClient()
    {
        IHonuaAdminClient client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/scan", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(CreateInventoryArtifact()));
        });

        var result = await client.ScanMigrationSourceAsync(new MigrationInventoryScanRequest
        {
            SourceKind = "geoserver",
            SourceUrl = "https://example.com/geoserver/rest"
        });

        Assert.Equal(MigrationSourceInventoryArtifact.CurrentArtifactKind, result.ArtifactKind);
    }

    [Fact]
    public void MigrationManifestArtifact_PreservesKindVersionAndNestedPlanningFields()
    {
        var artifact = new MigrationManifestArtifact
        {
            SourceKind = "arcgis-geoservices-rest",
            Source = CreateSourceIdentity(),
            Summary = new MigrationManifestSummary
            {
                SourceResourceCount = 1,
                TargetResourceCount = 1,
                StyleActionCount = 1,
                ManualReviewCount = 1,
                UnsupportedCount = 1
            },
            TargetResources =
            [
                new MigrationManifestTargetResource
                {
                    SourceResourceId = "resource:parcels",
                    SourceKind = "feature-layer",
                    Action = "publish",
                    TargetServiceName = "city",
                    TargetResourceName = "parcels",
                    GeometryType = "Polygon",
                    Fields =
                    [
                        new MigrationInventoryField
                        {
                            Name = "STATUS",
                            Alias = "Status",
                            FieldType = "esriFieldTypeString",
                            Nullable = true,
                            DomainType = "codedValue",
                            DomainName = "StatusDomain",
                            DomainValues = [new MigrationInventoryCodedValue { Code = "A", Name = "Active" }]
                        }
                    ],
                    SpatialReferences = [new MigrationSpatialReferenceInfo { Role = "declared", Srid = 4326 }],
                    StyleIds = ["style:parcels"],
                    ExternalDependencyIds = ["dependency:attachments"],
                    Compatibility = Compatible()
                }
            ],
            StyleActions =
            [
                new MigrationManifestStyleAction
                {
                    SourceStyleId = "style:parcels",
                    Action = "manual-review",
                    Format = "arcgis-renderer",
                    ResourceIds = ["resource:parcels"],
                    Compatibility = Partial()
                }
            ],
            ManualReviewItems =
            [
                new MigrationManifestReviewItem
                {
                    SourceId = "style:parcels",
                    Kind = "renderer",
                    Code = "ARCGIS_UNSUPPORTED_RENDERER",
                    Severity = "manual-review",
                    Reason = "Renderer requires review.",
                    ManualSteps = ["Recreate renderer."]
                }
            ],
            UnsupportedItems =
            [
                new MigrationManifestReviewItem
                {
                    SourceId = "resource:unsupported",
                    Kind = "layer",
                    Code = "UNSUPPORTED",
                    Severity = "unsupported",
                    Reason = "Cannot translate."
                }
            ]
        };

        var json = JsonSerializer.Serialize(artifact, HonuaAdminJsonContext.Default.MigrationManifestArtifact);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(MigrationManifestArtifact.CurrentArtifactKind, doc.RootElement.GetProperty("artifactKind").GetString());
        Assert.Equal(MigrationManifestArtifact.CurrentArtifactVersion, doc.RootElement.GetProperty("artifactVersion").GetString());
        Assert.Equal(MigrationSourceInventoryArtifact.CurrentArtifactKind, doc.RootElement.GetProperty("sourceArtifactKind").GetString());
        Assert.Equal("STATUS", doc.RootElement.GetProperty("targetResources")[0].GetProperty("fields")[0].GetProperty("name").GetString());

        var roundTrip = JsonSerializer.Deserialize(json, HonuaAdminJsonContext.Default.MigrationManifestArtifact)!;
        Assert.Equal("manual-review", roundTrip.StyleActions.Single().Action);
        Assert.Equal("ARCGIS_UNSUPPORTED_RENDERER", roundTrip.ManualReviewItems.Single().Code);
        Assert.Equal("UNSUPPORTED", roundTrip.UnsupportedItems.Single().Code);
    }

    [Fact]
    public void MigrationParityEvidenceArtifact_PreservesStatesReadinessAndAttestationFields()
    {
        Assert.Equal("pass", MigrationEvidenceStates.Pass);
        Assert.Equal("fail", MigrationEvidenceStates.Fail);
        Assert.Equal("unknown", MigrationEvidenceStates.Unknown);
        Assert.Equal("not-applicable", MigrationEvidenceStates.NotApplicable);

        var artifact = new MigrationParityEvidenceArtifact
        {
            SourceKind = "geoserver-rest",
            Source = CreateSourceIdentity(),
            OverallState = MigrationEvidenceStates.Unknown,
            Summary = "Review is waiting on operator attestations.",
            ManifestAvailable = true,
            Sections =
            [
                new MigrationParityEvidenceSection
                {
                    Id = "data",
                    Title = "Data parity",
                    State = MigrationEvidenceStates.Pass,
                    Items =
                    [
                        new MigrationParityEvidenceItem
                        {
                            Id = "resource:parcels",
                            State = MigrationEvidenceStates.Pass,
                            Summary = "Feature count matches.",
                            Evidence = ["source=42", "target=42"],
                            RelatedIds = ["resource:parcels"]
                        }
                    ]
                }
            ],
            CutoverReadiness = new MigrationCutoverReadinessSummary
            {
                State = MigrationEvidenceStates.Unknown,
                Items =
                [
                    new MigrationCutoverReadinessItem
                    {
                        Id = "rollback-plan-documented",
                        Title = "Rollback plan documented",
                        State = MigrationEvidenceStates.Unknown,
                        Remediation = ["Attach rollback runbook."],
                        Owner = "ops"
                    }
                ]
            }
        };

        var json = JsonSerializer.Serialize(artifact, HonuaAdminJsonContext.Default.MigrationParityEvidenceArtifact);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(MigrationParityEvidenceArtifact.CurrentArtifactKind, doc.RootElement.GetProperty("artifactKind").GetString());
        Assert.Equal(MigrationParityEvidenceArtifact.CurrentArtifactVersion, doc.RootElement.GetProperty("artifactVersion").GetString());
        Assert.Equal("cutoverReadiness", doc.RootElement.EnumerateObject().Single(p => p.Name == "cutoverReadiness").Name);

        var roundTrip = JsonSerializer.Deserialize(json, HonuaAdminJsonContext.Default.MigrationParityEvidenceArtifact)!;
        Assert.Equal(MigrationEvidenceStates.Unknown, roundTrip.CutoverReadiness.State);
        Assert.Equal("rollback-plan-documented", roundTrip.CutoverReadiness.Items.Single().Id);
        Assert.Equal("source=42", roundTrip.Sections.Single().Items.Single().Evidence[0]);

        var attestation = new MigrationReadinessAttestation
        {
            Items =
            [
                new MigrationReadinessAttestationItem
                {
                    Id = "rollback-plan-documented",
                    State = MigrationEvidenceStates.Pass,
                    Evidence = ["https://runbooks.example/rollback"],
                    Owner = "ops"
                }
            ]
        };

        var attestationJson = JsonSerializer.Serialize(attestation, HonuaAdminJsonContext.Default.MigrationReadinessAttestation);
        var attestationRoundTrip = JsonSerializer.Deserialize(attestationJson, HonuaAdminJsonContext.Default.MigrationReadinessAttestation)!;
        Assert.Equal(MigrationEvidenceStates.Pass, attestationRoundTrip.Items.Single().State);
        Assert.Equal("ops", attestationRoundTrip.Items.Single().Owner);
    }

    private static MigrationSourceInventoryArtifact CreateInventoryArtifact(string status = "complete")
        => new()
        {
            SourceKind = "geoserver-rest",
            Source = CreateSourceIdentity(),
            AuthPosture = new MigrationInventoryAuthPosture
            {
                Mode = "basic",
                CredentialsSupplied = true,
                AccessConfirmed = status != "failed",
                Notes = status == "failed" ? ["source timed out"] : []
            },
            ScanCompleteness = new MigrationInventoryCompleteness
            {
                Status = status,
                Warnings = status == "failed" ? ["source timed out"] : [],
                MissingArtifacts = status == "failed" ? ["resources"] : []
            },
            Summary = new MigrationInventorySummary
            {
                ContainerCount = 1,
                ResourceCount = 1,
                StyleCount = 1,
                ExternalDependencyCount = 1,
                CompatibleCount = status == "failed" ? 0 : 1,
                PartiallyCompatibleCount = status == "failed" ? 0 : 1,
                IncompatibleCount = status == "failed" ? 1 : 0
            },
            OverallCompatibility = status == "failed"
                ? new MigrationCompatibilityAssessment
                {
                    Level = "incompatible",
                    Code = "SOURCE_SCAN_FAILED",
                    Reason = "Source scan failed.",
                    ManualSteps = ["Retry the scan."]
                }
                : Partial(),
            Containers =
            [
                new MigrationInventoryContainer
                {
                    Id = "workspace:city",
                    Kind = "workspace",
                    Name = "city",
                    IsDefault = true,
                    Compatibility = Compatible()
                }
            ],
            Resources =
            [
                new MigrationInventoryResource
                {
                    Id = "resource:parcels",
                    ContainerId = "workspace:city",
                    Kind = "layer",
                    Name = "parcels",
                    GeometryType = "Polygon",
                    FeatureCount = 42,
                    HasAttachments = true,
                    Capabilities = ["query", "attachments"],
                    SpatialReferences = [new MigrationSpatialReferenceInfo { Role = "declared", Srid = 4326, CrsUri = "EPSG:4326" }],
                    Fields =
                    [
                        new MigrationInventoryField
                        {
                            Name = "STATUS",
                            Alias = "Status",
                            FieldType = "esriFieldTypeString",
                            Nullable = true,
                            DomainType = "codedValue",
                            DomainName = "StatusDomain",
                            DomainValues = [new MigrationInventoryCodedValue { Code = "A", Name = "Active" }]
                        }
                    ],
                    StyleIds = ["style:parcels"],
                    ExternalDependencyIds = ["dependency:attachments"],
                    Compatibility = Compatible()
                }
            ],
            Styles =
            [
                new MigrationInventoryStyle
                {
                    Id = "style:parcels",
                    ContainerId = "workspace:city",
                    Kind = "style",
                    Name = "parcels",
                    Format = "sld",
                    ResourceIds = ["resource:parcels"],
                    Metadata = new Dictionary<string, string> { ["rules"] = "2" },
                    Compatibility = Partial()
                }
            ],
            ExternalDependencies =
            [
                new MigrationExternalDependency
                {
                    Id = "dependency:attachments",
                    ContainerId = "workspace:city",
                    ResourceId = "resource:parcels",
                    Kind = "attachments",
                    Name = "parcel-attachments",
                    Compatibility = Partial()
                }
            ]
        };

    private static MigrationSourceIdentity CreateSourceIdentity()
        => new()
        {
            DisplayName = "City GIS",
            BaseUrl = "https://example.com/geoserver/rest",
            Product = "GeoServer",
            Version = "2.25",
            Build = "2026.05",
            ServiceType = "REST"
        };

    private static MigrationCompatibilityAssessment Compatible()
        => new()
        {
            Level = "compatible",
            Code = "COMPATIBLE",
            Reason = "Supported."
        };

    private static MigrationCompatibilityAssessment Partial()
        => new()
        {
            Level = "partial",
            Code = "MANUAL_REVIEW",
            Reason = "Requires operator review.",
            Warnings = ["manual review required"],
            ManualSteps = ["Review before cutover."]
        };
}
