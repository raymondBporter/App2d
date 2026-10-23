using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Geometry.Serialization;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class RoundedPolylineCurve2DTests
{
    [Theory]
    [InlineData(.01f)]
    [InlineData(.5f)]
    [InlineData(1f)]
    public void FilletHasNoReverseTurnAndKeepsEndpointTangents(float softness)
    {
        var curve = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1)], softness);
        Assert.Equal(Vector2.Zero, curve.Evaluate(0));
        Assert.Equal(Vector2.One, curve.Evaluate(1));
        Assert.Equal(Vector2.UnitX, Curve2D.Tangent(curve, 0));
        Assert.Equal(Vector2.UnitY, Curve2D.Tangent(curve, 1));
        var previous = 0f;
        foreach (var t in curve.SampleParameters(1000))
        {
            var tangent = Curve2D.Tangent(curve, t);
            var angle = MathF.Atan2(tangent.Y, tangent.X);
            Assert.InRange(angle, previous - 1e-5f, MathF.PI / 2 + 1e-5f);
            previous = angle;
            var p = curve.Evaluate(t);
            Assert.InRange(p.X, 0, 1);
            Assert.InRange(p.Y, 0, 1);
        }
        var enter = (1 - .48f * softness) / 2;
        var leave = (1 + .48f * softness) / 2;
        Assert.InRange(Vector2.Distance(curve.EvaluateDerivative(enter - 1e-6f), curve.EvaluateDerivative(enter + 1e-6f)), 0, .002f);
        Assert.InRange(Vector2.Distance(curve.EvaluateDerivative(leave - 1e-6f), curve.EvaluateDerivative(leave + 1e-6f)), 0, .002f);
    }

    [Fact]
    public void BoundariesAreAlwaysSampledAndDepthUsesTheSameWeights()
    {
        var curve = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1)]);
        var samples = curve.SampleParameters(3);
        Assert.Contains(.26f, samples);
        Assert.Contains(.5f, samples);
        Assert.Contains(.74f, samples);
        foreach (var t in samples)
        {
            var p = curve.Evaluate(t);
            Assert.Equal(p.X + 2 * p.Y, curve.Interpolate([0, 1, 3], t), 5);
        }
    }

    [Fact]
    public void LongerTailCanIntentionallyTurnBothWaysAndRoundTripThroughGeometryJson()
    {
        Vector2[] points = [new(0, 0), new(1, 0), new(1, 1), new(2, 1), new(2, 2)];
        var original = RoundedPolylineCurve2D.Builder.Create(points, .8f);
        var restored = Assert.IsType<RoundedPolylineCurve2D>(GeometryJson2D.CurveFromJson(original.ToJson()));
        var curve = restored.Validated();
        Assert.Equal(points[0], curve.Evaluate(0));
        Assert.Equal(points[^1], curve.Evaluate(1));
        Assert.Equal(.8f, restored.Softness);
        var turns = Enumerable.Range(1, 999).Select(i =>
        {
            var a = curve.EvaluateDerivative((i - 1) / 1000f);
            var b = curve.EvaluateDerivative(i / 1000f);
            return a.X * b.Y - a.Y * b.X;
        }).ToArray();
        Assert.Contains(turns, t => t > .01f);
        Assert.Contains(turns, t => t < -.01f);
    }

    [Fact]
    public void CollapsedSpansStraightLinesAndFoldbacksStayFinite()
    {
        foreach (var points in new Vector2[][]
        {
            [new(0, 0), new(0, 0), new(0, 0)], [new(0, 0), new(0, 0), new(1, 0)],
            [new(0, 0), new(1, 0), new(0, 0)], [new(0, 0), new(1, 0), new(2, 0)]
        })
        {
            foreach (var softness in new[] { 0f, 1f })
            {
                var curve = RoundedPolylineCurve2D.Builder.Create(points, softness);
                foreach (var t in curve.SampleParameters(20))
                {
                    var p = curve.Evaluate(t);
                    var d = curve.EvaluateDerivative(t);
                    Assert.True(float.IsFinite(p.X + p.Y + d.X + d.Y));
                }
                Assert.Equal(points[0], curve.Evaluate(0));
                Assert.Equal(points[^1], curve.Evaluate(1));
            }
        }
    }
}
