using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Distance;
using App2d.Core.Geometry.Intersection.Raycast;
using App2d.Core.Mathematics.Curves;

namespace App2d.Tests.Curves;

/// <summary>Curves made of polynomial pieces hand those pieces out, so distance and raycast are written once.</summary>
public sealed class PolynomialSpanTests
{
    public static TheoryData<ICurve2D, int, int> PolynomialCurves => new()
    {
        { LineSegmentCurve2D.Create(new(1, 2), new(4, 6)), 1, 1 },
        { QuadraticBezier2D.Builder.Create(new(-1, 1), new(0, -1), new(1, 1)), 1, 2 },
        { CubicBezier2D.Builder.Create(new(1, 2), new(3, 8), new(7, 5), new(9, -3)), 1, 3 },
        { PolylineCurve2D.Builder.Create([new(0, 0), new(2, 5), new(4, 1), new(8, 3)]), 3, 1 },
        { BSpline2D.Builder.Create([new(0, 0), new(1, 2), new(2, -1), new(3, 1), new(4, 0)]), 2, 3 },
        { RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(10, 0), new(10, 10)]), 3, 2 }
    };

    [Theory]
    [MemberData(nameof(PolynomialCurves))]
    public void SpansAreContiguousAndReproduceTheCurve(ICurve2D curve, int count, int maximumDegree)
    {
        var spans = Assert.IsType<IPolynomialCurve2D>(curve, exactMatch: false).GetPolynomialSpans();
        Span<Vector2d> controls = stackalloc Vector2d[spans.MaximumDegree + 1];
        var previousEnd = 0d;

        Assert.Equal(count, spans.Count);
        Assert.Equal(maximumDegree, spans.MaximumDegree);
        for (var i = 0; i < spans.Count; i++)
        {
            var range = spans.Get(i, controls);
            Assert.Equal(previousEnd, range.From, 6);
            Assert.InRange(range.ControlCount, 2, maximumDegree + 1);
            for (var step = 0; step <= 4; step++)
            {
                var local = step / 4d;
                var onSpan = BezierMath.EvaluateControls<Vector2d, double>(controls[..range.ControlCount], local);
                var onCurve = curve.Evaluate((float)(range.From + (range.To - range.From) * local)).ToVector2d();
                Assert.InRange((onSpan - onCurve).Length(), 0, 1e-5);
            }
            previousEnd = range.To;
        }
        Assert.Equal(1d, previousEnd, 6);
    }

    [Fact]
    public void QueriesAgainstSinglePieceCurvesDoNotAllocate()
    {
        ICurve2D line = LineSegmentCurve2D.Create(new(0, 0), new(4, 0));
        CubicBezier2D cubic = CubicBezier2D.Builder.Create(new(1, 2), new(3, 8), new(7, 5), new(9, -3));
        var query = new Vector2f(2, 3);
        var sum = 0f;
        var allocated = long.MaxValue;
        for (var pass = 0; pass < 4; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 2_000; i++)
            {
                if (CurveRaycast2D.TryIntersect(new(1, 3), -Vector2f.UnitY, line, 10, out var hit)) sum += hit.Distance;
                if (CurveRaycast2D.TryIntersect(new(5, 20), -Vector2f.UnitY, cubic, 40, out hit)) sum += hit.Distance;
                sum += cubic.Distance(query) + CurveDistance2D.Distance(query, cubic, .2f, .7f) + CurveDistance2D.Distance(query, line, .2f, .7f);
            }
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Under one byte per query on the quietest pass: nothing is allocated per call.
        Assert.InRange(allocated, 0, 9_999);
        Assert.True(float.IsFinite(sum));
    }

    [Fact]
    public void AnArcIsNotPolynomial()
    {
        ICurve2D arc = Arc2D.Builder.Create(Vector2f.Zero, 1, 0, 1);

        Assert.False(arc is IPolynomialCurve2D);
    }

    [Fact]
    public void ARayOntoASharedPolylineVertexReportsOneHitThere()
    {
        var polyline = PolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(2, 0)]);

        Assert.True(CurveRaycast2D.TryIntersect(new(1, 3), -Vector2f.UnitY, polyline, 10, out var hit));

        Assert.Equal(new Vector2f(1, 0), hit.Point);
        Assert.Equal(3f, hit.Distance);
    }

    [Fact]
    public void IntervalQueriesWorkOnAnyPolynomialCurve()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(1, 2), new(2, -1), new(3, 1), new(4, 0)]);
        var query = new Vector2f(4, 0);

        var whole = spline.Distance(query);
        var firstHalf = CurveDistance2D.Distance(query, spline, 0f, .5f);

        Assert.InRange(whole, 0, 1e-4f);
        Assert.Equal(Vector2f.Distance(query, spline.Evaluate(.5f)), firstHalf, 3);
    }
}
