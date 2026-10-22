using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Operations.MassProperties;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class HeightFieldShape2DTests
{
    [Fact]
    public void SlopeScanKeepsHillsTogetherAndSplitsAtIncreasingSlopes()
    {
        var hill = HeightFieldShape2D.Builder.Create([new(-10, 0), new(-5, 4), new(0, 6), new(5, 4), new(10, 0)], -10f);
        Assert.Equal(1, hill.ConvexPieces.Parts.Length);
        Assert.Equal(7, Assert.IsType<ConvexPolygon2D>(hill.ConvexPieces.Parts[0]).Vertices.Length);

        var valley = HeightFieldShape2D.Builder.Create([new(-10, 4), new(0, 0), new(10, 4)], -10f);
        Assert.Equal(2, valley.ConvexPieces.Parts.Length);
        Assert.False(valley.ConvexPieces.ContainsPoint(new(0, 3)));
        Assert.True(valley.ConvexPieces.ContainsPoint(new(0, -3)));

        var mixed = HeightFieldShape2D.Builder.Create([new(0, 0), new(2, 4), new(5, 5), new(7, 1), new(10, 4), new(14, 3)], -10f);
        Assert.Equal(2, mixed.ConvexPieces.Parts.Length);
        Assert.Equal(7f, ShapeBounds2D.Calculate(mixed.ConvexPieces.Parts[0]).Max.X);
        Assert.Equal(7f, ShapeBounds2D.Calculate(mixed.ConvexPieces.Parts[1]).Min.X);
    }

    [Fact]
    public void CollinearSamplesReduceToOneFourVertexCollisionPieceWithoutChangingTheAuthoredSurface()
    {
        Vector2[] surface = [.. Enumerable.Range(0, 1001).Select(index => new Vector2(index, index * 0.5f))];
        var field = new PolylineHeightField2D(surface, -10f);
        var shape = field.ToShape();
        Assert.Equal(surface, shape.Surface.ToArray());
        Assert.Equal(1, shape.ConvexPieces.Parts.Length);
        Assert.Equal(4, Assert.IsType<ConvexPolygon2D>(shape.ConvexPieces.Parts[0]).Vertices.Length);
    }

    [Fact]
    public void ConvexRunsHaveDisjointInteriorsAndExactlyCoverAnIrregularHeightfield()
    {
        var random = new Random(719);
        var points = Enumerable.Range(0, 80).Select(index => new Vector2(index * 2f, random.Next(-8, 12))).ToArray();
        var shape = HeightFieldShape2D.Builder.Create(points, -20f);
        var pieces = shape.ConvexPieces;
        Assert.InRange(MathF.Abs(shape.Area - pieces.Area), 0f, 0.002f);
        for (var index = 1; index < pieces.Parts.Length; index++)
            Assert.Equal(ShapeBounds2D.Calculate(pieces.Parts[index - 1]).Max.X, ShapeBounds2D.Calculate(pieces.Parts[index]).Min.X);
        for (var sample = 0; sample < 2000; sample++)
        {
            var point = new Vector2((float)random.NextDouble() * 180f - 10f, (float)random.NextDouble() * 50f - 30f);
            Assert.Equal(shape.ContainsPoint(point), pieces.ContainsPoint(point));
        }
    }

    [Fact]
    public void ShapeSupportsBoundsMassDistanceRaysOutlinesAndTriangulation()
    {
        var shape = HeightFieldShape2D.Builder.Create([new(0, 4), new(5, 0), new(10, 4)], -10f);
        var polygon = SimplePolygon2D.Builder.BelowPolyline(shape.Surface, shape.YMin);
        var placed = new SpatialObject2D(shape);
        Assert.Equal(new(0f, -10f), placed.LocalBounds.Min);
        Assert.Equal(new(10f, 4f), placed.LocalBounds.Max);
        Assert.Equal(120f, shape.Area);
        Assert.Equal(polygon.Diameter, ShapeDiameter2D.Calculate(shape));
        Assert.Equal(ShapeMassProperties2D.Calculate(polygon, 2d), ShapeMassProperties2D.Calculate(shape, 2d));
        foreach (var point in new Vector2[] { new(5, -2), new(5, 3), new(-2, -2), new(12, -12) })
            Assert.Equal(ShapeDistance2D.SignedDistance(point, polygon), ShapeDistance2D.SignedDistance(point, shape));
        Assert.True(ShapeRaycast2D.TryIntersect(new(5, 10), -Vector2.UnitY, shape, 20f, out var hit));
        Assert.Equal(10f, hit.Distance);
        Assert.True(ShapeRaycast2D.TryIntersect(new(5, -5), -Vector2.UnitY, shape, 20f, out var bottom));
        Assert.Equal(5f, bottom.Distance);
        Assert.True(ShapeRaycast2D.TryIntersect(new(5, -5), Vector2.UnitX, shape, 20f, out var end));
        Assert.Equal(5f, end.Distance);
        Span<Vector2> buffer = stackalloc Vector2[shape.GetOutlineVertCount()];
        Assert.Equal(polygon.Vertices.ToArray(), shape.GetOutlineVerts(buffer).ToArray());
        Assert.Equal(0, shape.GetVertCount()); // A concave field has no single convex support map.
        var area = 0f;
        foreach (var triangle in shape.Triangles(Span<Vector2>.Empty))
            area += MathF.Abs((triangle.B.X - triangle.A.X) * (triangle.C.Y - triangle.A.Y)
                - (triangle.B.Y - triangle.A.Y) * (triangle.C.X - triangle.A.X)) * 0.5f;
        Assert.Equal(shape.Area, area);
    }

    [Fact]
    public void SplineAndFunctionShapesFreezeOneSampledBoundary()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(1, 40), new(60, -20), new(100, 10)]);
        var splineShape = new BSplineHeightField2D(spline, -100f).ToShape(2f, 0.01f);
        for (var index = 0; index <= 300; index++)
            Assert.InRange(splineShape.Profile.Curve.Distance(spline.Evaluate(index / 300f)), 0f, 0.011f);
        var calls = 0;
        var source = new FunctionHeightField2D(0f, 100f, -20f, x => { calls++; return 5f * MathF.Sin(x * 0.1f); });
        var functionShape = source.ToShape(2f, 0.01f);
        var callsAfterBaking = calls;
        _ = functionShape.HeightAt(50f);
        _ = functionShape.ContainsPoint(new(50f, -5f));
        _ = ShapeDistance2D.Distance(new Vector2(50f, 10f), functionShape);
        Assert.Equal(callsAfterBaking, calls);
        var restored = Assert.IsType<HeightFieldShape2D>(GeometryJson2D.ShapeFromJson(functionShape.ToJson()).Validated());
        Assert.Equal(functionShape.Surface.ToArray(), restored.Surface.ToArray());
        Assert.Equal(functionShape.YMin, restored.YMin);
        Assert.Equal(functionShape.ToJson(), restored.ToJson());
    }

    [Fact]
    public void InfiniteSourcesNeedAnExplicitCutoffAndRawInvalidDataWaitsForValidation()
    {
        var infinite = new FunctionHeightField2D(0f, 100f, float.NegativeInfinity, _ => 0f);
        Assert.Throws<InvalidOperationException>(() => infinite.ToShape());
        var shape = infinite.ToClippedShape(-100f);
        Assert.True(shape.ContainsPoint(new(50, -100)));
        Assert.False(shape.ContainsPoint(new(50, -101)));
        Assert.True(infinite.ContainsPoint(new(50, -101)));
        var invalid = GeometryJson2D.ShapeFromJson("{\"kind\":\"height-field\",\"surface\":[{\"x\":1,\"y\":1},{\"x\":0,\"y\":1}],\"yMin\":-10}");
        Assert.Throws<ArgumentException>(invalid.Validate);
    }
}
