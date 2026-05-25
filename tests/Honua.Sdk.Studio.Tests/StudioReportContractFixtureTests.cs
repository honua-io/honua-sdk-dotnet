// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Studio;

namespace Honua.Sdk.Studio.Tests;

/// <summary>
/// Drift checks that pin the analysis-report and result-package fixtures to the
/// source-generated contracts. Deserializing strictly through
/// <see cref="StudioJsonContext"/> also exercises the trimming/AOT-safe path:
/// the polymorphic section hierarchy resolves with no reflection fallback.
/// </summary>
public sealed class StudioReportContractFixtureTests
{
    private static readonly string[] ExpectedSectionDiscriminators =
    [
        HonuaAnalysisReportSectionKinds.Heading,
        HonuaAnalysisReportSectionKinds.Paragraph,
        HonuaAnalysisReportSectionKinds.KeyMetric,
        HonuaAnalysisReportSectionKinds.Table,
        HonuaAnalysisReportSectionKinds.Chart,
        HonuaAnalysisReportSectionKinds.MapEmbed,
        HonuaAnalysisReportSectionKinds.Narrative,
        HonuaAnalysisReportSectionKinds.ProvenanceFooter
    ];

    [Fact]
    public void AnalysisReportFixture_DeserializesEverySectionDiscriminator()
    {
        var report = JsonSerializer.Deserialize(
            ReadFixture("analysis-report.v1.json"),
            StudioJsonContext.Default.HonuaAnalysisReport)
            ?? throw new InvalidOperationException("Analysis report fixture was empty.");

        var modeledKinds = report.Sections
            .Select(section => section.Kind)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var discriminator in ExpectedSectionDiscriminators)
        {
            Assert.Contains(discriminator, modeledKinds);
        }
    }

    [Fact]
    public void AnalysisReportFixture_RoundTripsThroughSourceGenContext()
    {
        var report = JsonSerializer.Deserialize(
            ReadFixture("analysis-report.v1.json"),
            StudioJsonContext.Default.HonuaAnalysisReport)!;

        // Serialize back through the source-gen context (AOT-safe write path),
        // then re-read; the polymorphic sections must survive intact.
        var json = JsonSerializer.Serialize(report, StudioJsonContext.Default.HonuaAnalysisReport);
        var reparsed = JsonSerializer.Deserialize(json, StudioJsonContext.Default.HonuaAnalysisReport)!;

        Assert.Contains("\"kind\":\"key-metric\"", json, StringComparison.Ordinal);
        Assert.Equal(report.Sections.Count, reparsed.Sections.Count);
        Assert.Equal(
            report.Sections.Select(s => s.Kind),
            reparsed.Sections.Select(s => s.Kind));
        Assert.IsType<HonuaChartSection>(reparsed.Sections.Single(s => s is HonuaChartSection));
    }

    [Fact]
    public void ResultPackageFixture_MapsNumericEnumsToServerOrdinals()
    {
        var package = JsonSerializer.Deserialize(
            ReadFixture("analysis-result-package.v1.json"),
            StudioJsonContext.Default.HonuaAnalysisResultPackage)
            ?? throw new InvalidOperationException("Result package fixture was empty.");

        Assert.Equal(HonuaGeoprocessingWorkflowStatus.Completed, package.Status);
        Assert.Equal("map-7f3c", package.MapPackageId);
        Assert.Equal("app-7f3c", package.AppPackageId);

        Assert.Collection(
            package.Artifacts,
            scalar =>
            {
                Assert.Equal(HonuaArtifactKind.Scalar, scalar.Kind);
                Assert.Equal("people", scalar.Metadata["unit"]);
            },
            layer =>
            {
                Assert.Equal(HonuaArtifactKind.FeatureLayer, layer.Kind);
                Assert.Equal("honua://artifacts/affected-parcels", layer.Uri);
            });

        Assert.Equal(HonuaWorkspaceKind.ResultCollection, package.WorkspaceRefs.Single().Kind);
    }

    [Fact]
    public void GeoprocessingError_RoundTripsThroughSourceGenContext()
    {
        var error = new HonuaGeoprocessingError
        {
            Kind = HonuaGeoprocessingErrorKind.ValidationFailed,
            Message = "Buffer distance is required.",
            StepId = "buffer",
            Violations =
            [
                new HonuaGeoprocessingValidationFailure
                {
                    Code = "missing-input",
                    Message = "distance is required",
                    FieldPath = "inputs.distance"
                }
            ]
        };

        var json = JsonSerializer.Serialize(error, StudioJsonContext.Default.HonuaGeoprocessingError);
        var reparsed = JsonSerializer.Deserialize(json, StudioJsonContext.Default.HonuaGeoprocessingError)!;

        Assert.Equal(HonuaGeoprocessingErrorKind.ValidationFailed, reparsed.Kind);
        Assert.Equal("buffer", reparsed.StepId);
        Assert.Equal("inputs.distance", reparsed.Violations!.Single().FieldPath);
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Join(FindRepoRoot(), "contracts", "fixtures", "console", name));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Honua.Sdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }
}
