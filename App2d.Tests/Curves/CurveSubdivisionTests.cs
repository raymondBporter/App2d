using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Curves;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class CurveSubdivisionTests
{
    private static ICurve2D[] Curves =>
    [
        new LineSegmentCurve2D(new(1, 2), new(7, -3)),
        new QuadraticBezier2D(new(1, 2), new(-2, 8), new(7, -3)),
        new CubicBezier2D(new(1, 2), new(-2, 8), new(5, -6), new(7, -3)),
        new Arc2D(new(1, 2), 3, .2f, -4f),
        new PolylineCurve2D([new(0, 0), new(2, 5), new(4, 1), new(8, 3)])
    ];

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(.13f, .81f)]
    [InlineData(.5f, .5f)]
    public void GenericSlicePreservesEvaluationAndScalesDerivatives(float from, float to)
    {
        foreach (var curve in Curves)
        {
            var piece = curve.Slice(from, to);
            for (var i = 0; i <= 20; i++)
            {
                var u = i / 20f;
                Near(curve.Evaluate(from + (to - from) * u), piece.Evaluate(u));
                Near(curve.EvaluateDerivative(from + (to - from) * u) * (to - from), piece.EvaluateDerivative(u));
            }
            Near(curve.Evaluate(from), piece.Evaluate(-1));
            Near(curve.Evaluate(to), piece.Evaluate(2));
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(.37f)]
    [InlineData(1f)]
    public void GenericSplitReturnsBothPiecesIncludingEndpointSplits(float at)
    {
        foreach (var curve in Curves)
        {
            var (left, right) = curve.Split(at);
            for (var i = 0; i <= 20; i++)
            {
                var u = i / 20f;
                Near(curve.Evaluate(at * u), left.Evaluate(u));
                Near(curve.Evaluate(at + (1 - at) * u), right.Evaluate(u));
            }
        }
    }

    [Fact]
    public void NestedSlicesComposeAndDistanceUsesOnlyTheRestrictedGeometry()
    {
        var line = new LineSegmentCurve2D(Vector2.Zero, new(10, 0));
        var piece = line.Slice(.4f, .6f);
        Assert.Equal(4f, piece.Distance(Vector2.Zero), 5);
        Assert.Equal(2f, line.Slice(.5f, .5f).Distance(new(5, 2)), 5);
        var nested = piece.Slice(.25f, .75f);
        Near(new(4.5f, 0), nested.Evaluate(0));
        Near(new(5.5f, 0), nested.Evaluate(1));
        Near(new(1, 0), nested.EvaluateDerivative(.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => line.Split(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => line.Slice(.7f, .2f));
    }

    [Fact]
    public void HermiteConstructionPreservesEndpointValuesAndTangents()
    {
        var start = new Vector2(2, -3);
        var end = new Vector2(-1, 4);
        var first = new Vector2(6, 9);
        var last = new Vector2(-3, 6);
        var curve = CubicBezier2D.FromHermite(start, end, first, last);
        Near(start, curve.Evaluate(0));
        Near(end, curve.Evaluate(1));
        Near(first, curve.EvaluateDerivative(0));
        Near(last, curve.EvaluateDerivative(1));
        for (var i = 0; i <= 20; i++)
        {
            var u = i / 20f;
            var square = u * u;
            var cube = square * u;
            var expected = (2 * cube - 3 * square + 1) * start + (cube - 2 * square + u) * first
                + (-2 * cube + 3 * square) * end + (cube - square) * last;
            Near(expected, curve.Evaluate(u));
        }
    }

    private static void Near(Vector2 expected, Vector2 actual) => Assert.InRange(Vector2.Distance(expected, actual), 0, 2e-5f);
}
