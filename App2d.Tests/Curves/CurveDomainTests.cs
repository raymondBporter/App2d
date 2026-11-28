using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Curves;

namespace App2d.Tests.Curves;

/// <summary>A curve says where it lives, and every operation works over that domain instead of assuming [0, 1].</summary>
public sealed class CurveDomainTests
{
    private static readonly QuadraticBezier<double, double> Square = new(0d, 0d, 1d);
    private static readonly QuadraticBezier<double, double> Identity = new(0d, .5d, 1d);

    [Fact]
    public void GeometryAndBeziersLiveOnTheUnitInterval()
    {
        Assert.Equal(new Interval<double>(0d, 1d), Square.Domain);
        Assert.Equal(new Interval<float>(0f, 1f), LineSegmentCurve2D.Create(new(1, 2), new(4, 6)).Domain);
        Assert.Equal(new Interval<float>(0f, 1f), Easingf.Smooth.Domain);
        Assert.Equal(new Interval<double>(0d, 1d), Square.Ops.Slice(.2d, .4d).Domain);
    }

    [Fact]
    public void AProgressMapsDomainIsItsOrderedInputSpan()
    {
        Assert.Equal(new Interval<float>(2f, 6f), Easingf.Smooth.FromRange(6f, 2f).Domain);
        Assert.Equal(new Interval<float>(2f, 6f), Easingf.Smooth.FromRange(2f, 6f).Domain);
    }

    [Fact]
    public void OpsClampsWhereTheMapItselfClamps()
    {
        var ranged = Easingf.Smooth.FromRange(6f, 2f);
        var clamped = ranged.Ops.Clamped();

        Assert.Equal(ranged.EvaluateClamped(20f), ranged.Ops.EvaluateClamped(20f));
        Assert.Equal(ranged.EvaluateClamped(-20f), ranged.Ops.EvaluateClamped(-20f));
        Assert.Equal(ranged.Evaluate(6f), clamped.Evaluate(20f));
        Assert.Equal(ranged.Evaluate(2f), clamped.Evaluate(-20f));
        Assert.Equal(0f, clamped.Derivative(20f));
        Assert.Equal(ranged.Evaluate(4f), clamped.Evaluate(4f));
    }

    [Fact]
    public void ExtendRunsStraightFromTheDomainEnds()
    {
        var extended = Easingf.Smooth.FromRange(10f, 20f).Ops.Extended();

        Assert.Equal(1f, extended.Evaluate(25f));
        Assert.Equal(0f, extended.Evaluate(5f));
        Assert.Equal(.5f, extended.Evaluate(15f));
    }

    [Fact]
    public void NormalizedMapsTheUnitIntervalOntoTheDomain()
    {
        var normalized = Easingf.Smooth.FromRange(10f, 20f).Ops.Normalized();

        Assert.Equal(new Interval<float>(0f, 1f), normalized.Domain);
        Assert.Equal(.5f, normalized.Evaluate(.5f));
        Assert.Equal(Easingf.Smooth.Evaluate(.25f), normalized.Evaluate(.25f), 6);
        Assert.Equal(Easingf.Smooth.Derivative(.25f), normalized.Derivative(.25f), 5);
    }

    [Fact]
    public void ReverseSplitAndSampleFollowTheDomain()
    {
        var ranged = Easingf.Smooth.FromRange(10f, 20f).Ops;
        var (left, right) = ranged.Split(15f);

        Assert.Equal(1f, ranged.Reverse().Evaluate(0f));
        Assert.Equal(0f, ranged.Reverse().Evaluate(1f));
        Assert.Equal(0f, left.Evaluate(0f));
        Assert.Equal(.5f, left.Evaluate(1f));
        Assert.Equal(.5f, right.Evaluate(0f));
        Assert.Equal(1f, right.Evaluate(1f));
        Assert.Equal([0f, .5f, 1f], ranged.Sample(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ranged.Split(.5f));
    }

    [Fact]
    public void ConcatenateRunsOneCurveThenTheNext()
    {
        var joined = Square.Ops.Concatenate(Identity);

        Assert.Equal(new Interval<double>(0d, 2d), joined.Domain);
        Assert.Equal(.25d, joined.Evaluate(.5d));
        Assert.Equal(1d, joined.Evaluate(1d));
        Assert.Equal(2d, joined.Derivative(1d));
        Assert.Equal(.5d, joined.Evaluate(1.5d));
        Assert.Equal(1d, joined.Derivative(1.5d));
        Assert.Equal(1d, joined.Clamped().Evaluate(5d));
        Assert.True(double.IsNaN(joined.Evaluate(double.NaN)));
    }

    [Fact]
    public void ConcatenateStartsTheSecondCurveAtItsOwnBeginning()
    {
        var joined = Square.Ops.Concatenate(Easingd.Smooth.FromRange(10d, 20d));

        Assert.Equal(new Interval<double>(0d, 11d), joined.Domain);
        Assert.Equal(0d, joined.Evaluate(1.0000001d), 6);
        Assert.Equal(.5d, joined.Evaluate(6d));
        Assert.Equal(1d, joined.Evaluate(11d));
        Assert.Throws<ArgumentNullException>(() => Square.Ops.Concatenate<ProgressMapd>(null!));
    }

    [Fact]
    public void TheEndOfAnOffUnitDomainIsReachedExactly()
    {
        // 0.3 + (1.4 - 0.3) * 1 rounds to 1.3999999 in float, one step short of the end.
        var map = ProgressMapf.Forward.FromRange(.3f, 1.4f);
        var joined = new QuadraticBezier<float, float>(0f, 0f, 1f).Ops.Concatenate(map);

        Assert.Equal(1f, map.Ops.Normalized().Evaluate(1f));
        Assert.Equal(0f, map.Ops.Normalized().Evaluate(0f));
        Assert.Equal(1f, map.Ops.Sample(2)[^1]);
        Assert.Equal(1f, map.Ops.Split(.7f).Right.Evaluate(1f));
        Assert.Equal(0f, map.Ops.Reverse().Evaluate(1f));
        Assert.Equal(1f, joined.Evaluate(joined.Domain.Max));
    }

    [Fact]
    public void ConcatenateRejectsAnUnboundedDomain()
    {
        Assert.ThrowsAny<ArgumentException>(() => Square.Ops.Concatenate(new Unbounded()));
    }

    private readonly struct Unbounded : IDifferentiableCurve<double, double>
    {
        public Interval<double> Domain => new(double.NegativeInfinity, 1d);
        public double Evaluate(double t) => t;
        public double Derivative(double t) => 1d;
    }

    [Fact]
    public void ADomainAwareChainDoesNotAllocate()
    {
        var sum = 0d;
        var allocated = long.MaxValue;
        for (var pass = 0; pass < 4; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++) sum += Chain(i * 2e-4d);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Boxing one struct costs at least 24 bytes a call; the bound is under one byte per call on the quietest pass.
        Assert.InRange(allocated, 0, 9_999);
        Assert.True(double.IsFinite(sum));

        static double Chain(double t)
        {
            var curve = Square.Ops.Concatenate(Identity).Clamped();
            return curve.Evaluate(t) + curve.Derivative(t) + curve.Normalized().Evaluate(t * .5d);
        }
    }
}
