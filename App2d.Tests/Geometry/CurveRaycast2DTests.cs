using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class CurveRaycast2DTests
{
    [Fact]
    public void LineCurvePreservesRayParameterAndDirectedNormal()
    {
        var line = LineSegmentCurve2D.Builder.Create(new(2, -1), new(2, 1));
        Assert.True(CurveRaycast2D.TryIntersect(Vector2.Zero, new(2, 0), line, 1, out var hit));
        Assert.Equal(new Vector2(2, 0), hit.Point);
        Assert.Equal(-Vector2.UnitX, hit.Normal);
        Assert.Equal(1, hit.Distance);
        Assert.False(CurveRaycast2D.TryIntersect(Vector2.Zero, new(2, 0), line, .9f, out _));
        Assert.True(CurveRaycast2D.TryIntersect(new(2, 0), Vector2.UnitX, line, 0, out var onSurface));
        Assert.Equal(0, onSurface.Distance);
        Assert.False(CurveRaycast2D.TryIntersect(new(2, -2), Vector2.UnitY, line, 10, out _));
    }

    [Fact]
    public void QuadraticFindsBothCrossingsAndAStationaryTangency()
    {
        var curve = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 2), new(2, 0));
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, .75f), Vector2.UnitX, curve, 5, out var first));
        Near(new(.5f, .75f), first.Point);
        Near(Vector2.Normalize(new(-1, 1)), first.Normal);
        Assert.True(CurveRaycast2D.TryIntersect(new(.8f, .75f), Vector2.UnitX, curve, 5, out var second));
        Near(new(1.5f, .75f), second.Point);
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, 1), Vector2.UnitX, curve, 5, out var tangent));
        Near(new(1, 1), tangent.Point);
        Near(Vector2.UnitY, tangent.Normal);
        Assert.False(CurveRaycast2D.TryIntersect(new(-1, 1.00001f), Vector2.UnitX, curve, 5, out _));
    }

    [Theory]
    [InlineData(-1f, .2f)]
    [InlineData(.3f, .5f)]
    [InlineData(.6f, .8f)]
    public void CubicSelectsTheNearestForwardHitAcrossThreeRoots(float start, float expected)
    {
        var curve = CubicBezier2D.Builder.Create(new(0, -.08f), new(1f / 3, .14f), new(2f / 3, -.14f), new(1, .08f));
        Assert.True(CurveRaycast2D.TryIntersect(new(start, 0), Vector2.UnitX, curve, 10, out var hit));
        Near(new(expected, 0), hit.Point);
        Assert.Equal(expected - start, hit.Distance, 5);
    }

    [Fact]
    public void CubicDetectsATangentRootWithoutASignChange()
    {
        var curve = CubicBezier2D.Builder.Create(new(0, .25f), new(1f / 3, 0), new(2f / 3, -.25f), new(1, .5f));
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, 0), Vector2.UnitX, curve, 10, out var hit));
        Near(new(.5f, 0), hit.Point);
        Near(Vector2.UnitY, hit.Normal);
    }

    [Fact]
    public void CuspGetsAFiniteOneSidedNormalWhilePointAndCollinearCurvesMiss()
    {
        var cusp = QuadraticBezier2D.Builder.Create(Vector2.One, -Vector2.One, Vector2.One);
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, 0), Vector2.UnitX, cusp, 5, out var hit));
        Near(Vector2.Zero, hit.Point);
        Near(Vector2.Normalize(new(-1, 1)), hit.Normal);
        var point = QuadraticBezier2D.Builder.Create(Vector2.One, Vector2.One, Vector2.One);
        Assert.False(CurveRaycast2D.TryIntersect(new(0, 1), Vector2.UnitX, point, 5, out _));
        var collinear = CubicBezier2D.Builder.Create(Vector2.Zero, Vector2.UnitX, -Vector2.UnitX, new(2, 0));
        Assert.False(CurveRaycast2D.TryIntersect(new(-1, 0), Vector2.UnitX, collinear, 5, out _));
    }

    [Fact]
    public void ArcTestsTheFarCircleRootAndHonorsSweepOrientationAndWraparound()
    {
        var quarter = Arc2D.Builder.Create(Vector2.Zero, 1, 0, MathF.PI / 2);
        Assert.True(CurveRaycast2D.TryIntersect(new(-2, 0), Vector2.UnitX, quarter, 3, out var far));
        Near(Vector2.UnitX, far.Point);
        Near(-Vector2.UnitX, far.Normal);
        Assert.False(CurveRaycast2D.TryIntersect(new(-2, 0), Vector2.UnitX, quarter, 2.9f, out _));
        var clockwise = Arc2D.Builder.Create(Vector2.Zero, 1, 0, -MathF.PI / 2);
        Assert.True(CurveRaycast2D.TryIntersect(new(-2, -.5f), Vector2.UnitX, clockwise, 5, out var lower));
        Near(new(MathF.Sqrt(.75f), -.5f), lower.Point);
        Near(lower.Point, lower.Normal);
        var wrapped = Arc2D.Builder.Create(Vector2.Zero, 1, 7 * MathF.PI / 4, MathF.PI / 2);
        Assert.True(CurveRaycast2D.TryIntersect(new(2, 0), -Vector2.UnitX, wrapped, 5, out var wrap));
        Near(Vector2.UnitX, wrap.Point);
        Assert.False(CurveRaycast2D.TryIntersect(new(-2, -.5f), Vector2.UnitX, quarter, 5, out _));
        Assert.False(CurveRaycast2D.TryIntersect(new(-2, 0), Vector2.UnitX, Arc2D.Builder.Create(Vector2.Zero, 0, 0, 1), 5, out _));
    }

    [Fact]
    public void ArcTangencyAndARestrictedFullCircleRetainExactCircularGeometry()
    {
        var arc = Arc2D.Builder.Create(Vector2.Zero, 1, 0, MathF.PI);
        Assert.True(CurveRaycast2D.TryIntersect(new(-2, 1), Vector2.UnitX, arc, 5, out var tangent));
        Near(Vector2.UnitY, tangent.Point);
        Near(-Vector2.UnitY, tangent.Normal);
        var full = Arc2D.Builder.Create(Vector2.Zero, 1, 0, MathF.Tau);
        var slice = new CurveSlice2D<Arc2D>(full, 0, .25f);
        Assert.True(CurveRaycast2D.TryIntersect(new(-2, 0), Vector2.UnitX, slice, 5, out var hit));
        Near(Vector2.UnitX, hit.Point);
    }

    [Fact]
    public void PolylineUsesTheNearestSegmentRatherThanTheFirstInCurveOrder()
    {
        var curve = PolylineCurve2D.Builder.Create([new(4, 1), new(4, -1), new(2, -1), new(2, 1)]);
        Assert.True(CurveRaycast2D.TryIntersect(Vector2.Zero, Vector2.UnitX, curve, 5, out var hit));
        Near(new(2, 0), hit.Point);
        Near(-Vector2.UnitX, hit.Normal);
    }

    [Fact]
    public void RoundedPolylineIntersectsItsQuadraticFillet()
    {
        var curve = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(2, 0), new(2, 2)], 1);
        // The corner trims .96 from each edge; its quadratic midpoint is (1.76, .24).
        Assert.True(CurveRaycast2D.TryIntersect(new(1.76f, -1), Vector2.UnitY, curve, 5, out var hit));
        Near(new(1.76f, .24f), hit.Point);
        Near(Vector2.Normalize(new(-1, 1)), hit.Normal);
        var linear = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(2, 0), new(2, 2)], 0);
        Assert.True(CurveRaycast2D.TryIntersect(new(1.76f, -1), Vector2.UnitY, linear, 5, out var cornerless));
        Near(new(1.76f, 0), cornerless.Point);
    }

    [Fact]
    public void NestedSlicesRestrictTheParameterDomainBeforeChoosingTheNearestHit()
    {
        var curve = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 2), new(2, 0));
        var left = new CurveSlice2D<QuadraticBezier2D>(curve, 0, .5f);
        Assert.False(CurveRaycast2D.TryIntersect(new(1.1f, .75f), Vector2.UnitX, left, 5, out _));
        var right = new CurveSlice2D<QuadraticBezier2D>(curve, .5f, 1);
        var nested = new CurveSlice2D<CurveSlice2D<QuadraticBezier2D>>(right, .25f, 1);
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, .75f), Vector2.UnitX, nested, 5, out var hit));
        Near(new(1.5f, .75f), hit.Point);
        Assert.False(CurveRaycast2D.TryIntersect(new(-1, .75f), Vector2.UnitX, new CurveSlice2D<QuadraticBezier2D>(curve, .25f, .25f), 5, out _));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void HigherDegreeSplineSpansFindRootsWithoutFlattening(int degree)
    {
        // Degree-elevated polynomial (u-.25)*(u-.75); independently known crossings at .25 and .75.
        var points = Enumerable.Range(0, degree + 1).Select(i => new Vector2(i / (float)degree,
            (float)(i * (i - 1d) / (degree * (degree - 1d)) - i / (double)degree + .1875))).ToArray();
        var curve = BSpline2D.Builder.Create(points, degree);
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, 0), Vector2.UnitX, curve, 5, out var first));
        Near(new(.25f, 0), first.Point);
        Assert.True(CurveRaycast2D.TryIntersect(new(.5f, 0), Vector2.UnitX, curve, 5, out var second));
        Near(new(.75f, 0), second.Point);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void ExtractedSplineSpansAgreeWithDeBoorAcrossInternalKnots(int degree)
    {
        var points = Enumerable.Range(0, degree + 7).Select(i => new Vector2(i, MathF.Sin(i * 1.7f))).ToArray();
        var curve = BSpline2D.Builder.Create(points, degree);
        foreach (var u in new[] { .03f, .17f, .33333334f, .57f, .89f, .98f })
        {
            var expected = curve.Evaluate(u);
            Assert.True(CurveRaycast2D.TryIntersect(expected - new Vector2(0, 4), new(0, 2), curve, 3, out var hit));
            Near(expected, hit.Point, .0001f);
            Assert.Equal(2, hit.Distance, 4);
            var tangent = curve.EvaluateDerivative(u);
            Near(Vector2.Normalize(new(-tangent.Y, tangent.X)), hit.Normal, .0001f);
        }
    }

    [Fact]
    public void HigherDegreeTangenciesAreNotLostBySignOnlySearches()
    {
        const int degree = 8;
        // Alternating Bernstein controls represent (2u-1)^8 exactly, with an eightfold root at .5.
        var points = Enumerable.Range(0, degree + 1).Select(i => new Vector2(i / (float)degree, (i & 1) == 0 ? 1 : -1)).ToArray();
        var curve = BSpline2D.Builder.Create(points, degree);
        Assert.True(CurveRaycast2D.TryIntersect(new(-1, 0), Vector2.UnitX, curve, 5, out var hit));
        Near(new(.5f, 0), hit.Point);
        Near(Vector2.UnitY, hit.Normal);
    }

    [Fact]
    public void CommonEntryPointSupportsShapesAndCurvesAndRejectsProceduralQueriesExplicitly()
    {
        IGeometryValue2D circle = Circle2D.Builder.Create(1, new(3, 0));
        IGeometryValue2D line = LineSegmentCurve2D.Builder.Create(new(2, -1), new(2, 1));
        var ray = new Ray2D(Vector2.Zero, Vector2.UnitX);
        Assert.True(GeometryRaycast2D.TryIntersect(ray, circle, 5, out var shapeHit));
        Assert.True(GeometryRaycast2D.TryIntersect(ray, line, 5, out var curveHit));
        Near(shapeHit.Point, curveHit.Point);
        var procedural = new NormalOffsetCurve2D((ICurve2D)line, _ => throw new Exception("Must not sample arbitrary code."));
        Assert.Throws<NotSupportedException>(() => GeometryRaycast2D.TryIntersect(ray, procedural, 5, out _));
        Assert.ThrowsAny<ArgumentException>(() => CurveRaycast2D.TryIntersect(default, (ICurve2D)line, 5, out _));
        Assert.ThrowsAny<ArgumentException>(() => CurveRaycast2D.TryIntersect(Vector2.Zero, Vector2.Zero, (ICurve2D)line, 5, out _));
        Assert.ThrowsAny<ArgumentException>(() => CurveRaycast2D.TryIntersect(Vector2.Zero, Vector2.UnitX, (ICurve2D)line, -1, out _));
    }

    [Fact]
    public void AffineQueriesPreserveWorldDistanceAndTransformCurveNormalsByTheInverseTranspose()
    {
        var curve = QuadraticBezier2D.Builder.Create(Vector2.Zero, new(1, 2), new(2, 0));
        var pose = new Affine2D { Position = new(3, -2), ScaleX = 2, ScaleY = .6f, ShearX = .2f, Rotation = .4f };
        Matrix3x2.Invert(pose.Matrix, out var inverse);
        var normal = Vector2.Normalize(inverse.TransposeTransformDirection(Vector2.UnitY));
        var point = pose.TransformPoint(new(1, 1));
        var ray = new Ray2D(point + normal * 5, -normal);
        Assert.True(GeometryRaycast2D.TryIntersect(ray, curve, pose.Matrix, 6, out var hit));
        Near(point, hit.Point);
        Near(normal, hit.Normal);
        Assert.Equal(5, hit.Distance, 4);
        Assert.False(GeometryRaycast2D.TryIntersect(ray, curve, pose.Matrix, 4.9f, out _));
        Assert.ThrowsAny<ArgumentException>(() => GeometryRaycast2D.TryIntersect(ray, curve, Matrix3x2.CreateScale(0), 6, out _));
    }

    [Fact]
    public void MirroringKeepsTheAuthoredNormalSideThroughTheInverseTranspose()
    {
        var line = LineSegmentCurve2D.Builder.Create(Vector2.Zero, Vector2.UnitX);
        var transform = Matrix3x2.CreateScale(-2, 3);
        Assert.True(GeometryRaycast2D.TryIntersect(new Ray2D(new(-1, 2), -Vector2.UnitY), line, transform, 5, out var hit));
        Near(new(-1, 0), hit.Point);
        Near(Vector2.UnitY, hit.Normal);
        Assert.Equal(2, hit.Distance);
    }

    [Fact]
    public void SlicedSplineAndRoundedCurveQueriesDoNotSearchOutsideTheirRestrictedIntervals()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(1, 2), new(2, -1), new(3, 1), new(4, 0)]);
        var sliced = new CurveSlice2D<BSpline2D>(spline, 0, .4f);
        var right = spline.Evaluate(.8f);
        Assert.False(CurveRaycast2D.TryIntersect(right - new Vector2(0, 5), Vector2.UnitY, sliced, 10, out _));
        var rounded = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(0, 0), new(2, 0), new(2, 2)]);
        Assert.True(CurveRaycast2D.TryIntersect(new(1.76f, -1), Vector2.UnitY, rounded, 5, out var hit));
        Near(new(1.76f, .24f), hit.Point);
        var early = new CurveSlice2D<RoundedPolylineCurve2D>(rounded, 0, .1f);
        Assert.False(CurveRaycast2D.TryIntersect(new(1.76f, -1), Vector2.UnitY, early, 5, out _));
    }

    private static void Near(Vector2 expected, Vector2 actual, float tolerance = .00002f) =>
        Assert.True(Vector2.Distance(expected, actual) <= tolerance, $"Expected {expected}, got {actual}");
}
