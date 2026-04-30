// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;

namespace Honua.Sdk.Geometry.Tests;

public class HonuaPlanarGeometryAnalyzerTests
{
    private static readonly GeometryFactory ProjectedFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 3857);

    private static readonly GeometryFactory Wgs84Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [Fact]
    public void MeasureDistance_UsesNtsPlanarDistance()
    {
        var first = ProjectedFactory.CreatePoint(new Coordinate(0, 0));
        var second = ProjectedFactory.CreatePoint(new Coordinate(3, 4));

        var distance = HonuaPlanarGeometryAnalyzer.MeasureDistance(first, second);

        Assert.Equal(5, distance);
    }

    [Fact]
    public void MeasureArea_RejectsWgs84WithoutProjection()
    {
        var polygon = Wgs84Factory.CreatePolygon(
        [
            new Coordinate(-158.0, 21.0),
            new Coordinate(-157.0, 21.0),
            new Coordinate(-157.0, 22.0),
            new Coordinate(-158.0, 22.0),
            new Coordinate(-158.0, 21.0)
        ]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => HonuaPlanarGeometryAnalyzer.MeasureArea(polygon));

        Assert.Contains("EPSG:4326", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasureLength_ProjectsBeforePlanarAnalysis()
    {
        var line = Wgs84Factory.CreateLineString(
        [
            new Coordinate(-157.8583, 21.3069),
            new Coordinate(-157.8583, 21.3169)
        ]);

        var length = HonuaPlanarGeometryAnalyzer.MeasureLength(line, new PlanarGeometryAnalysisOptions
        {
            AnalysisSpatialReference = HonuaSpatialReference.WebMercator
        });

        Assert.InRange(length, 1_100, 1_250);
    }

    [Fact]
    public void Buffer_Simplify_AndCentroid_ReturnProjectedAnalysisGeometry()
    {
        var line = ProjectedFactory.CreateLineString(
        [
            new Coordinate(0, 0),
            new Coordinate(5, 0),
            new Coordinate(10, 0)
        ]);

        var buffer = HonuaPlanarGeometryAnalyzer.Buffer(line, 1);
        var simplified = HonuaPlanarGeometryAnalyzer.Simplify(line, 2);
        var centroid = HonuaPlanarGeometryAnalyzer.GetCentroid(buffer);

        Assert.True(buffer.Area > 0);
        Assert.Equal(2, simplified.NumPoints);
        Assert.InRange(centroid.X, 4.9, 5.1);
    }

    [Fact]
    public void TopologyOperations_UseNtsPredicatesAndOverlay()
    {
        var container = ProjectedFactory.CreatePolygon(
        [
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0)
        ]);
        var overlapping = ProjectedFactory.CreatePolygon(
        [
            new Coordinate(5, 5),
            new Coordinate(15, 5),
            new Coordinate(15, 15),
            new Coordinate(5, 15),
            new Coordinate(5, 5)
        ]);
        var inside = ProjectedFactory.CreatePoint(new Coordinate(2, 2));

        var intersection = HonuaPlanarGeometryAnalyzer.Intersect(container, overlapping);

        Assert.True(HonuaPlanarGeometryAnalyzer.Contains(container, inside));
        Assert.True(HonuaPlanarGeometryAnalyzer.Covers(container, inside));
        Assert.True(HonuaPlanarGeometryAnalyzer.Intersects(container, overlapping));
        Assert.True(HonuaPlanarGeometryAnalyzer.Overlaps(container, overlapping));
        Assert.Equal(25, intersection.Area);
    }

    [Fact]
    public void EnvelopeOperations_ReturnEnvelopeAndGeometry()
    {
        var geometry = ProjectedFactory.CreateMultiPointFromCoords(
        [
            new Coordinate(1, 2),
            new Coordinate(5, 8)
        ]);

        var envelope = HonuaPlanarGeometryAnalyzer.GetEnvelope(geometry);
        var envelopeGeometry = HonuaPlanarGeometryAnalyzer.GetEnvelopeGeometry(geometry);

        Assert.Equal(1, envelope.MinX);
        Assert.Equal(8, envelope.MaxY);
        Assert.Equal("Polygon", envelopeGeometry.GeometryType);
    }

    [Fact]
    public void NearestOperations_ReturnNearestPointPairAndVertex()
    {
        var line = ProjectedFactory.CreateLineString(
        [
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10)
        ]);
        var target = ProjectedFactory.CreatePoint(new Coordinate(7, 2));

        var nearestPoints = HonuaPlanarGeometryAnalyzer.FindNearestPoints(line, target);
        var nearestVertex = HonuaPlanarGeometryAnalyzer.FindNearestVertex(line, target);

        Assert.Equal(7, nearestPoints.FirstPoint.X);
        Assert.Equal(0, nearestPoints.FirstPoint.Y);
        Assert.Equal(2, nearestPoints.Distance);
        Assert.Equal(1, nearestVertex.CoordinateIndex);
        Assert.Equal(10, nearestVertex.Vertex.X);
        Assert.InRange(nearestVertex.Distance, 3.60, 3.61);
    }

    [Fact]
    public void BinaryAnalysis_RejectsDifferentKnownSridsWithoutAnalysisReference()
    {
        var first = ProjectedFactory.CreatePoint(new Coordinate(0, 0));
        var second = Wgs84Factory.CreatePoint(new Coordinate(0, 0));

        var exception = Assert.Throws<InvalidOperationException>(
            () => HonuaPlanarGeometryAnalyzer.Intersects(first, second));

        Assert.Contains("SRIDs differ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BinaryAnalysis_RejectsDifferentKnownSridsEvenWhenSourceReferenceIsProvided()
    {
        var first = ProjectedFactory.CreatePoint(new Coordinate(0, 0));
        var second = Wgs84Factory.CreatePoint(new Coordinate(0, 0));

        var exception = Assert.Throws<InvalidOperationException>(
            () => HonuaPlanarGeometryAnalyzer.Intersects(first, second, new PlanarGeometryAnalysisOptions
            {
                SourceSpatialReference = HonuaSpatialReference.WebMercator
            }));

        Assert.Contains("SRIDs differ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnalysisProjection_DistinguishesDifferentWktSpatialReferences()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory();
        var line = factory.CreateLineString(
        [
            new Coordinate(-157.8583, 21.3069),
            new Coordinate(-157.8583, 21.3169)
        ]);

        var length = HonuaPlanarGeometryAnalyzer.MeasureLength(line, new PlanarGeometryAnalysisOptions
        {
            SourceSpatialReference = HonuaSpatialReference.FromWkt(GeographicCoordinateSystem.WGS84.WKT),
            AnalysisSpatialReference = HonuaSpatialReference.FromWkt(ProjectedCoordinateSystem.WebMercator.WKT)
        });

        Assert.InRange(length, 1_100, 1_250);
    }
}
