using App2d.Core.Curves;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class Arc2DTests
{
    [Fact]
    public void ArcDistanceUsesTheCircleInteriorAndFiniteSweepEndpoints()
    {
        Arc2D quarter = new Arc2D(Vector2.Zero, 2, 0, MathF.PI / 2);
        Assert.Equal(1, quarter.Distance(new(3 / MathF.Sqrt(2), 3 / MathF.Sqrt(2))), 5);
        Assert.Equal(MathF.Sqrt(5), quarter.Distance(new(-1, 0)), 5);

        Arc2D clockwise = new Arc2D(Vector2.Zero, 2, 0, -MathF.PI / 2);
        Assert.Equal(1, clockwise.Distance(new(0, -3)), 5);
        Assert.Equal(MathF.Sqrt(13), clockwise.Distance(new(0, 3)), 5);

        Arc2D full = new Arc2D(Vector2.Zero, 2, 0, MathF.Tau);
        Assert.Equal(1, full.Distance(new(-3, 0)), 5);
        Assert.Equal(2, full.Distance(Vector2.Zero), 5);

        Arc2D wrapped = new Arc2D(Vector2.Zero, 2, 3 * MathF.PI / 4, MathF.PI);
        Assert.Equal(1, wrapped.Distance(new(-3, 0)), 5);
        Assert.Equal(1, wrapped.Distance(new(0, -3)), 5);

        Assert.Equal(3, new Arc2D(Vector2.Zero, 0, 0, 1).Distance(new(0, 3)), 5);
        Assert.Equal(3, new Arc2D(Vector2.Zero, 2, 0, 0).Distance(new(5, 0)), 5);
    }

    [Fact]
    public void CircularArcSamplesEndpointsMidpointAndDerivative()
    {
        var arc = new Arc2D(new(2, 3), 4, 0, MathF.PI / 2);
        Assert.Equal(new Vector2(6, 3), arc.Start);
        Assert.InRange(Vector2.Distance(new(2 + 2 * MathF.Sqrt(2), 3 + 2 * MathF.Sqrt(2)), arc.Evaluate(.5f)), 0, 1e-6f);
        Assert.InRange(Vector2.Distance(new(2, 7), arc.End), 0, 1e-6f);
        Assert.InRange(Vector2.Distance(new(0, 2 * MathF.PI), arc.EvaluateDerivative(0)), 0, 1e-6f);
        Assert.Equal(2 * Math.PI, arc.Length, 1e-6);

        var samples = Curve2D.Sample(arc, 4);
        Assert.Equal(5, samples.Length);
        Assert.Equal(arc.Start, samples[0]);
        Assert.Equal(arc.End, samples[^1]);
    }

    [Fact]
    public void ClockwiseAndFullCircleSweepsKeepTheirDirection()
    {
        var clockwise = new Arc2D(Vector2.Zero, 1, 0, -MathF.PI / 2);
        Assert.True(clockwise.Evaluate(.5f).Y < 0);
        Assert.True(clockwise.EvaluateDerivative(0).Y < 0);

        var full = new Arc2D(new(1, -1), 2, 0, MathF.Tau);
        Assert.InRange(Vector2.Distance(full.Start, full.End), 0, 1e-6f);
        Assert.InRange(Vector2.Distance(new(-1, -1), full.Evaluate(.5f)), 0, 1e-6f);
    }

    [Fact]
    public void ZeroRadiusIsAConstantCurve()
    {
        var arc = new Arc2D(new(4, 5), 0, 1, 3);
        Assert.Equal(new Vector2(4, 5), arc.Evaluate(.5f));
        Assert.Equal(Vector2.Zero, arc.EvaluateDerivative(.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Arc2D(Vector2.Zero, -1, 0, 1));
    }
}
