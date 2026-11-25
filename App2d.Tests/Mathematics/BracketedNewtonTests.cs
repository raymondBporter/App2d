using App2d.Core.Mathematics.RootFinding;

namespace App2d.Tests.Mathematics;

/// <summary>The solver takes any differentiable scalar curve; there is no separate function interface.</summary>
public sealed class BracketedNewtonTests
{
    [Fact]
    public void SolvesABezierTimeCurveDirectly()
    {
        var time = new CubicBezier<double, double>(0d, .1d, .9d, 1d);

        Assert.True(BracketedNewton.TrySolve(time, .3d, 64, out var parameter));
        Assert.Equal(.3d, time.Evaluate(parameter), 14);
    }

    [Fact]
    public void InvertsAProgressMap()
    {
        Assert.True(BracketedNewton.TrySolve(Easingd.Smooth, .104d, 64, out var phase));
        Assert.Equal(.2d, phase, 12);
    }

    [Fact]
    public void SolvesAChainedCurve()
    {
        var curve = new QuadraticBezier<double, double>(0d, 0d, 1d).Ops.Reparameterize(Easingd.Smooth);

        Assert.True(BracketedNewton.TrySolve(curve, .25d, 128, out var parameter));
        Assert.Equal(.25d, curve.Evaluate(parameter), 12);
    }

    [Theory]
    [InlineData(1.5d)]
    [InlineData(-.25d)]
    [InlineData(double.NaN)]
    public void RejectsATargetOutsideTheNormalizedRange(double target)
    {
        Assert.ThrowsAny<ArgumentException>(() => BracketedNewton.TrySolve(Easingd.Smooth, target, 64, out _));
    }

    [Fact]
    public void ReportsExhaustionWithItsLastEstimate()
    {
        Assert.False(BracketedNewton.TrySolve(Easingd.Smooth, .104d, 1, out var phase));
        Assert.InRange(phase, 0d, 1d);
    }
}
