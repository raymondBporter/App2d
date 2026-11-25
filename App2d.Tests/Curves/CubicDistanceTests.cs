using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Distance;

namespace App2d.Tests.Curves;

/// <summary>Cubic pieces measure distance from the roots of the stationary polynomial, not from sampled chords.</summary>
public sealed class CubicDistanceTests
{
    private static readonly CubicBezier2D[] Cubics =
    [
        CubicBezier2D.Builder.Create(new(0, 0), new(10, 10), new(-5, 10), new(5, 0)),
        CubicBezier2D.Builder.Create(new(1, 2), new(3, 8), new(7, 5), new(9, -3)),
        CubicBezier2D.Builder.Create(new(-4, 0), new(-4, 9), new(4, -9), new(4, 0))
    ];

    [Fact]
    public void DistanceMatchesADenseReferenceEverywhere()
    {
        var random = new Random(1234);
        foreach (var cubic in Cubics)
        {
            for (var i = 0; i < 200; i++)
            {
                var query = new Vector2f(random.NextSingle() * 24 - 8, random.NextSingle() * 24 - 8);
                Assert.Equal(Reference(cubic, query, 0, 1), cubic.Distance(query), 4);
            }
        }
    }

    [Fact]
    public void APointOnTheCurveBetweenSamplePositionsIsAtDistanceZero()
    {
        // Parameter 64.5 / 128 sits in the middle of a chord of the old 128-chord sampling, at the tight turn.
        var cubic = Cubics[0];

        Assert.InRange(cubic.Distance(cubic.Evaluate(64.5f / 128f)), 0, 1e-5f);
    }

    [Fact]
    public void IntervalDistanceMatchesTheReference()
    {
        var random = new Random(99);
        for (var i = 0; i < 100; i++)
        {
            var query = new Vector2f(random.NextSingle() * 24 - 8, random.NextSingle() * 24 - 8);
            Assert.Equal(Reference(Cubics[0], query, .2, .7), CurveDistance2D.Distance(query, Cubics[0], .2f, .7f), 4);
        }
    }

    [Fact]
    public void TheNearestPointCanBeAnEndOfACurvedCubic()
    {
        var cubic = Cubics[1];
        var behindTheStart = cubic.Start - cubic.Derivative(0) * .5f;

        Assert.Equal(Vector2f.Distance(behindTheStart, cubic.Start), cubic.Distance(behindTheStart), 5);
        Assert.Equal(0f, cubic.Distance(cubic.Start));
        Assert.Equal(0f, cubic.Distance(cubic.End));
    }

    [Fact]
    public void AStraightCubicIsASegment()
    {
        var straight = CubicBezier2D.Builder.Create(new(0, 0), new(1, 0), new(3, 0), new(4, 0));

        Assert.Equal(3f, straight.Distance(new(2, 3)), 5);
        Assert.Equal(5f, straight.Distance(new(8, 3)), 5);
        Assert.Equal(1f, straight.Distance(new(-1, 0)), 5);
    }

    [Fact]
    public void ACubicThatIsOnePointMeasuresToThatPoint()
    {
        var point = CubicBezier2D.Builder.Create(new(2, 2), new(2, 2), new(2, 2), new(2, 2));

        Assert.Equal(5f, point.Distance(new(5, 6)), 5);
    }

    [Fact]
    public void HugeCoordinatesStayFinite()
    {
        var huge = CubicBezier2D.Builder.Create(new(1e20f, 0), Vector2f.Zero, Vector2f.Zero, Vector2f.Zero);

        Assert.Equal(3f, huge.Distance(new(0, 3)), 3);
        Assert.True(float.IsFinite(huge.Distance(new(1e19f, 5))));
    }

    [Fact]
    public void ACubicBSplineMatchesTheReference()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(1, 6), new(4, -5), new(7, 6), new(9, 0), new(12, 3)]);
        var random = new Random(7);
        for (var i = 0; i < 100; i++)
        {
            var query = new Vector2f(random.NextSingle() * 16 - 2, random.NextSingle() * 12 - 6);
            Assert.Equal(Reference(spline, query, 0, 1), spline.Distance(query), 4);
        }
    }

    // Distance to 20,000 chords, in double precision: the chords sit within about 1e-7 of these curves.
    private static float Reference<TCurve>(TCurve curve, Vector2f query, double from, double to) where TCurve : ICurve2D
    {
        const int Chords = 20_000;
        var point = query.ToVector2d();
        var previous = curve.Evaluate((float)from).ToVector2d();
        var nearest = double.PositiveInfinity;
        for (var i = 1; i <= Chords; i++)
        {
            var next = curve.Evaluate((float)(from + (to - from) * i / Chords)).ToVector2d();
            var edge = next - previous;
            var lengthSquared = edge.LengthSquared();
            var amount = lengthSquared == 0 ? 0 : Math.Clamp((point - previous).Dot(edge) / lengthSquared, 0, 1);
            nearest = Math.Min(nearest, (point - previous - edge * amount).LengthSquared());
            previous = next;
        }
        return (float)Math.Sqrt(nearest);
    }
}
