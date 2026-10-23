using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Geometry.Operations.HeightFields;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class HeightField2DTests
{
    [Fact]
    public void AnalyticSourceEvaluatesDirectlyWithoutSamplingOrSearchingForExtrema()
    {
        var calls = 0;
        var field = new FunctionHeightField2D(0f, 2f * MathF.PI, -2f,
            x => { calls++; return 2f + 3f * MathF.Sin(x); }, x => 3f * MathF.Cos(x), yMax: 5f);
        Assert.Equal(2, calls); // Constructor validates endpoints only, without a scan for minima/maxima.
        Assert.Equal(5f, field.HeightAt(MathF.PI / 2f), 5);
        Assert.Equal(3, calls);
        Assert.InRange(Vector2.Distance(Vector2.Normalize(new(-3, 1)), field.NormalAt(0f)), 0f, 1e-6f);
        Assert.Equal(3, calls); // An analytic derivative does not need any height samples either.
        Assert.Equal(-2f, field.Bounds().Min.Y);
        Assert.Equal(5f, field.Bounds().Max.Y);
    }

    [Fact]
    public void SourcesShareTheSameBoundedSolidAndTopNormalQueries()
    {
        Vector2[] points = [new(-10, -1), new(-9, -0.5f), new(-3, 2.5f), new(10, 9)];
        IHeightField2D[] fields =
        [
            new FunctionHeightField2D(-10f, 10f, -20f, x => 0.5f * x + 4f, _ => 0.5f, yMax: 9f),
            new PolylineHeightField2D(points, -20f),
            new BSplineHeightField2D(BSpline2D.Builder.Create(points), -20f)
        ];
        foreach (var field in fields)
        {
            Assert.Equal(new(-10f, -20f), field.Bounds().Min);
            Assert.Equal(new(10f, 9f), field.Bounds().Max);
            Assert.InRange(MathF.Abs(field.HeightAt(1f) - 4.5f), 0f, 0.00001f);
            Assert.InRange(Vector2.Distance(Vector2.Normalize(new(-0.5f, 1f)), field.NormalAt(1f)), 0f, 0.00001f);
            Assert.True(field.ContainsPoint(new(1f, field.HeightAt(1f))));
            Assert.True(field.ContainsPoint(new(-10f, -1f)));
            Assert.True(field.ContainsPoint(new(10f, 9f)));
            Assert.True(field.ContainsPoint(new(0f, -20f)));
            Assert.False(field.ContainsPoint(new(1f, 4.51f)));
            Assert.False(field.ContainsPoint(new(0f, -20.01f)));
            Assert.False(field.ContainsPoint(new(-10.01f, -10f)));
            Assert.False(field.ContainsPoint(new(10.01f, -10f)));
            Assert.False(field.ContainsPoint(new(float.NaN, 0f)));
            Assert.False(field.ContainsPoint(new(0f, float.NegativeInfinity)));
            Assert.ThrowsAny<ArgumentException>(() => field.HeightAt(-11f));
            Assert.ThrowsAny<ArgumentException>(() => field.HeightAt(float.NaN));
        }
    }

    [Fact]
    public void DefaultDerivativeUsesWorldCoordinatesAndOneSidedEndpointDifferences()
    {
        var field = new FunctionHeightField2D(-3f, 7f, -10f, x => x * x);
        Assert.InRange(field.SlopeAt(-3f), -6.01f, -5.99f);
        Assert.InRange(field.SlopeAt(2f), 3.99f, 4.01f);
        Assert.InRange(field.SlopeAt(7f), 13.99f, 14.01f);
        IHeightField2D custom = new MinimalHeightField();
        Assert.InRange(custom.SlopeAt(4f), 1.999f, 2.001f);
        Assert.Equal(float.PositiveInfinity, custom.YMax);
        Assert.Equal(new(-1f, -5f), custom.Bounds().Min);
        Assert.Equal(new(5f, float.PositiveInfinity), custom.Bounds().Max);
    }

    [Fact]
    public void PolylineUsesWorldXAndPreservesAuthoredCornersWhenBaked()
    {
        Vector2[] points = [new(0, 2), new(3, 8), new(40, -2), new(100, 4)];
        var field = new PolylineHeightField2D(points, -10f);
        points[1] = new(3, 99); // The curve owns its input storage.
        Assert.Equal(5f, field.HeightAt(1.5f));
        Assert.Equal(8f, field.HeightAt(3f));
        Assert.Equal(-2f, field.HeightAt(40f));
        Assert.Equal(2f, field.SlopeAt(0f));
        Assert.Equal(-10f / 37f, field.SlopeAt(3f));
        Assert.Equal(0.1f, field.SlopeAt(100f));
        var sampled = field.ToPolyline(5f, 0.01f);
        foreach (var point in field.Curve.Points) Assert.Contains(point, sampled.Points.ToArray());
        Assert.Equal(8f, field.YMax);
    }

    [Fact]
    public void BSplineInvertsItsXCoordinateAndUsesDyOverDx()
    {
        var curve = BSpline2D.Builder.Create([new(0, 0), new(1, 100), new(3, -40), new(100, 20)]);
        var field = new BSplineHeightField2D(curve, -100f);
        for (var index = 0; index <= 100; index++)
        {
            var parameter = index / 100f;
            var point = curve.Evaluate(parameter);
            var derivative = curve.EvaluateDerivative(parameter);
            Assert.InRange(MathF.Abs(field.HeightAt(point.X) - point.Y), 0f, 0.0001f);
            Assert.InRange(MathF.Abs(field.SlopeAt(point.X) - derivative.Y / derivative.X), 0f, 0.0001f);
        }
        Assert.True(MathF.Abs(field.HeightAt(50f) - curve.Evaluate(0.5f).Y) > 1f);
        Assert.Equal(100f, field.YMax); // Safe control-hull bound, not the actual maximum of the curve.
        Assert.Equal(curve.Evaluate(0f), field.ToPolyline().Points[0]);
        Assert.Equal(curve.Evaluate(1f), field.ToPolyline().Points[^1]);
    }

    [Fact]
    public void InfiniteBottomSupportsQueriesAndRequiresExplicitClippingForPolygonCollision()
    {
        var field = new FunctionHeightField2D(-20f, 20f, float.NegativeInfinity, x => 2f + MathF.Sin(x), yMax: 3f);
        Assert.Equal(new(-20f, float.NegativeInfinity), field.Bounds().Min);
        Assert.True(field.ContainsPoint(new(0f, -1e20f)));
        Assert.False(field.ContainsPoint(new(-21f, -1e20f)));
        Assert.Throws<InvalidOperationException>(() => field.ToPolygon());
        var clipped = field.ToClippedPolygon(-100f, maximumSegmentLength: 1f, maximumDeviation: 0.001f);
        Assert.True(clipped.ContainsPoint(new(0f, -100f)));
        Assert.True(clipped.ContainsPoint(new(-20f, -50f)));
        Assert.False(clipped.ContainsPoint(new(0f, -101f)));
        Assert.True(field.ContainsPoint(new(0f, -101f))); // Source remains infinite.
        Assert.ThrowsAny<ArgumentException>(() => field.ToClippedPolygon(float.NegativeInfinity));
    }

    [Fact]
    public void FunctionCanBakeASolidWithMatchingTopSidesAndBottom()
    {
        var field = new FunctionHeightField2D(0f, 100f, -20f,
            x => 10f * MathF.Sin(x * 0.1f), x => MathF.Cos(x * 0.1f), yMax: 10f);
        var solid = field.ToPolygon(maximumSegmentLength: 2f, maximumDeviation: 0.005f);
        for (var x = 0.5f; x < 100f; x += 0.5f)
        {
            Assert.True(solid.ContainsPoint(new(x, field.HeightAt(x) - 0.02f)));
            Assert.False(solid.ContainsPoint(new(x, field.HeightAt(x) + 0.02f)));
        }
        Assert.True(solid.ContainsPoint(new(0f, -10f)));
        Assert.True(solid.ContainsPoint(new(100f, -10f)));
        Assert.True(solid.ContainsPoint(new(50f, -20f)));
        Assert.False(solid.ContainsPoint(new(-0.01f, -10f)));
        Assert.False(solid.ContainsPoint(new(100.01f, -10f)));
        Assert.False(solid.ContainsPoint(new(50f, -20.01f)));
        Assert.ThrowsAny<ArgumentException>(() => field.ToClippedPolygon(-21f));
    }

    [Theory]
    [InlineData(1f, 1f, -10f, 10f)]
    [InlineData(2f, 1f, -10f, 10f)]
    [InlineData(float.NegativeInfinity, 1f, -10f, 10f)]
    [InlineData(0f, float.PositiveInfinity, -10f, 10f)]
    [InlineData(0f, 1f, float.PositiveInfinity, float.PositiveInfinity)]
    [InlineData(0f, 1f, float.NaN, 10f)]
    [InlineData(0f, 1f, -10f, float.NaN)]
    [InlineData(0f, 1f, -10f, float.NegativeInfinity)]
    [InlineData(0f, 1f, -10f, -11f)]
    public void RejectsInvalidDomains(float xMin, float xMax, float yMin, float yMax) =>
        Assert.ThrowsAny<ArgumentException>(() => new FunctionHeightField2D(xMin, xMax, yMin, _ => 0f, yMax: yMax));

    [Fact]
    public void RejectsBacktrackingAndProfilesThatViolateTheirSuppliedBounds()
    {
        Assert.Throws<ArgumentException>(() => new PolylineHeightField2D([new(0, 0), new(0, 1)], -10f));
        Assert.Throws<ArgumentException>(() => new PolylineHeightField2D([new(0, 0), new(2, 1), new(1, 0)], -10f));
        Assert.Throws<ArgumentException>(() => new PolylineHeightField2D([new(0, 0), new(1, -11)], -10f));
        Assert.Throws<ArgumentException>(() => new BSplineHeightField2D(
            BSpline2D.Builder.Create([new(0, 0), new(2, 0), new(1, 0), new(3, 0)]), -10f));
        var belowBottom = new FunctionHeightField2D(0f, 1f, 0f, x => x == 0.5f ? -1f : 1f);
        Assert.Throws<ArgumentException>(() => belowBottom.HeightAt(0.5f));
        Assert.Throws<ArgumentException>(() => belowBottom.ToPolygon());
        var aboveBound = new FunctionHeightField2D(0f, 1f, -10f, x => x == 0.5f ? 11f : 1f, yMax: 10f);
        Assert.Throws<ArgumentException>(() => aboveBound.HeightAt(0.5f));
        Assert.ThrowsAny<ArgumentException>(() => new FunctionHeightField2D(0f, 1f, -10f, _ => float.NaN));
        Assert.Throws<ArgumentNullException>(() => new FunctionHeightField2D(0f, 1f, -10f, null!));
    }

    private sealed class MinimalHeightField : IHeightField2D
    {
        public float XMin => -1f;
        public float XMax => 5f;
        public float YMin => -5f;
        public float HeightAt(float x) => 2f * x;
    }
}
