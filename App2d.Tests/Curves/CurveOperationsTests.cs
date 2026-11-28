using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Curves;

/// <summary>Operations written once on the Curve wrapper: slice, reverse, reparameterize, clamp and extend.</summary>
public sealed class CurveOperationsTests
{
    private static readonly QuadraticBezier<double, double> Square = new(0d, 0d, 1d);

    [Fact]
    public void SmoothReparameterizationUsesTheChainRule()
    {
        var eased = Square.Ops.Reparameterize(Easingd.Smooth);

        Assert.Equal(0.0244140625d, eased.Evaluate(.25d));
        Assert.Equal(0.3515625d, eased.Derivative(.25d));
        Assert.Equal(0d, eased.Derivative(0d));
        Assert.Equal(0d, eased.Derivative(1d));
    }

    [Fact]
    public void ReversingAVectorCurveReversesItsTangent()
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4), new(4, 2, 1));
        var reversed = curve.Ops.Reparameterize(ProgressMapf.Reverse).Clamped();

        Assert.Equal(curve.Evaluate(.75f), reversed.Evaluate(.25f));
        Assert.Equal(-curve.Derivative(.75f), reversed.Derivative(.25f));
        Assert.Equal(-curve.Derivative(1f), reversed.Derivative(0f));
    }

    [Theory]
    [InlineData(-.25f, 1f)]
    [InlineData(1.25f, 0f)]
    public void AClampedCurveHoldsStillPastItsEnds(float amount, float parameter)
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4), new(4, 2, 1));
        var reversed = curve.Ops.Reparameterize(ProgressMapf.Reverse).Clamped();

        Assert.Equal(curve.Evaluate(parameter), reversed.Evaluate(amount));
        Assert.Equal(Vector3f.Zero, reversed.Derivative(amount));
    }

    [Theory]
    [InlineData(.125f, 0f, 0f)]
    [InlineData(.25f, 0f, 4f)]
    [InlineData(.5f, 1f, 4f)]
    [InlineData(.75f, 2f, 4f)]
    [InlineData(.875f, 2f, 0f)]
    public void OvershootingAClampedSourceHoldsItsEndpoints(float amount, float position, float velocity)
    {
        var curve = new QuadraticBezier<Vector2f, float>(Vector2f.Zero, Vector2f.UnitX, Vector2f.UnitX * 2f);
        var reparameterized = curve.Ops.Clamped().Reparameterize(ProgressMapf.Affine(2f, -.5f));

        Assert.Equal(Vector2f.UnitX * position, reparameterized.Evaluate(amount));
        Assert.Equal(Vector2f.UnitX * velocity, reparameterized.Derivative(amount));
    }

    [Fact]
    public void ConstantMapHoldsEvenWhereTheSourceHasNoTangent()
    {
        var stationary = new CornerCurve().Ops.Reparameterize(ProgressMapf.Constant(.5f));

        Assert.Equal(0f, stationary.Evaluate(.25f));
        Assert.Equal(0f, stationary.Derivative(.25f));
        Assert.Equal(0f, stationary.Derivative(.75f));
    }

    [Fact]
    public void StepJumpKeepsItsNaNDerivative()
    {
        Assert.True(double.IsNaN(Square.Ops.Reparameterize(Easingd.Step).Derivative(1d)));
    }

    [Fact]
    public void ExtendRunsStraightPastBothEnds()
    {
        var extended = Square.Ops.Extended();

        Assert.Equal(2d, extended.Evaluate(1.5d));
        Assert.Equal(2d, extended.Derivative(1.5d));
        Assert.Equal(0d, extended.Evaluate(-1d));
        Assert.Equal(0d, extended.Derivative(-1d));
        Assert.Equal(.25d, extended.Evaluate(.5d));
    }

    [Fact]
    public void EachSideChoosesItsOwnExtrapolation()
    {
        var curve = Square.Ops.Extrapolated(Extrapolation.Clamp, Extrapolation.Extend);

        Assert.Equal(0d, curve.Evaluate(-1d));
        Assert.Equal(3d, curve.Evaluate(2d));
        Assert.Equal(4d, Square.Ops.Extrapolated(Extrapolation.None, Extrapolation.None).Evaluate(2d));
    }

    [Fact]
    public void ClampedKeepsTheTangentAtExactEndpoints()
    {
        var clamped = Square.Ops.Clamped();

        Assert.Equal(2d, clamped.Derivative(1d));
        Assert.Equal(0d, clamped.Derivative(1.0001d));
        Assert.Equal(1d, clamped.Evaluate(5d));
        Assert.Equal(1d, Square.Ops.EvaluateClamped(5d));
        Assert.Equal(0d, Square.Ops.EvaluateClamped(-5d));
    }

    [Fact]
    public void SliceMatchesTheSourceAndScalesTheTangent()
    {
        var slice = Square.Ops.Slice(.25d, .75d);

        Assert.Equal(Square.Evaluate(.5d), slice.Evaluate(.5d));
        Assert.Equal(Square.Derivative(.5d) * .5d, slice.Derivative(.5d));
        Assert.Equal(Square.Evaluate(.25d), slice.Evaluate(0d));
        Assert.Equal(Square.Evaluate(.75d), slice.Evaluate(1d));
    }

    [Fact]
    public void SplitGivesBothHalves()
    {
        var (left, right) = Square.Ops.Split(.25d);

        Assert.Equal(Square.Evaluate(.125d), left.Evaluate(.5d));
        Assert.Equal(Square.Evaluate(.625d), right.Evaluate(.5d));
        Assert.Equal(left.Evaluate(1d), right.Evaluate(0d));
    }

    [Fact]
    public void SampleWritesBothEndpointsInAnyDimension()
    {
        var curve = new CubicBezier<Vector3f, float>(new(0, 1, -2), new(1, 2, 3), new(3, -1, -4), new(4, 2, 1));
        var points = new Vector3f[5];

        curve.Ops.Sample(points);

        Assert.Equal(curve.Evaluate(0), points[0]);
        Assert.Equal(curve.Evaluate(.5f), points[2]);
        Assert.Equal(curve.Evaluate(1), points[4]);
        Assert.Equal(5, Square.Ops.Sample(4).Length);
        Assert.ThrowsAny<ArgumentException>(() => Square.Ops.Sample(0));
        Assert.ThrowsAny<ArgumentException>(() => Square.Ops.Sample(new double[1]));
    }

    [Fact]
    public void AnyTwoDimensionalCurveCanBecomeAPolyline()
    {
        var arc = Arc2D.Builder.Create(Vector2f.Zero, 2, 0, MathF.PI);
        var half = arc.Ops.Slice(0f, .5f);

        var polyline = Curve2D.ToPolyline(half, 4, 12f, .01f);

        Assert.Equal(arc.Evaluate(0), polyline.Points[0]);
        Assert.Equal(arc.Evaluate(.5f), polyline.Points[^1]);
        Assert.True(polyline.Points.Length > 5);
    }

    [Theory]
    [InlineData(1.5d)]
    [InlineData(-.1d)]
    [InlineData(double.NaN)]
    public void SplitRejectsAParameterOutsideTheCurve(double parameter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Square.Ops.Split(parameter));
    }

    [Fact]
    public void ReverseIsSliceOneZero()
    {
        var reversed = Square.Ops.Reverse();

        Assert.Equal(Square.Evaluate(.75d), reversed.Evaluate(.25d));
        Assert.Equal(-Square.Derivative(.75d), reversed.Derivative(.25d));
    }

    [Fact]
    public void ZeroWidthSliceIsStationary()
    {
        var point = Square.Ops.Slice(.3d, .3d);

        Assert.Equal(Square.Evaluate(.3d), point.Evaluate(.9d));
        Assert.Equal(0d, point.Derivative(.9d));
    }

    [Fact]
    public void ZeroWidthSliceIsStationaryEvenWhereTheSourceIsNot()
    {
        // The step's derivative is NaN at its jump; a slice that never moves must not multiply it by zero.
        Assert.Equal(0d, Easingd.Step.Ops.Slice(1d, 1d).Derivative(.5d));
        Assert.Equal(Square.Evaluate(.3d), Square.Ops.Slice(.3d, .3d).Evaluate(double.PositiveInfinity));
        Assert.True(double.IsNaN(Square.Ops.Slice(.3d, .3d).Evaluate(double.NaN)));
    }

    [Fact]
    public void ZeroWidthSliceKeepsNaNInBothValueAndTangent()
    {
        Assert.True(double.IsNaN(Square.Ops.Slice(.3d, .3d).Derivative(double.NaN)));
    }

    [Fact]
    public void WrappersConstructFreelyAndValidateOnRequest()
    {
        var line = LineSegmentCurve2D.Create(new(1, 2), new(4, 6));

        // Construction never checks, so loaded data can be built first and validated afterwards.
        var slice = new Sliced<QuadraticBezier<double, double>, double, double>(Square, double.NaN, 1d);
        var reparameterized = new Reparameterized<QuadraticBezier<double, double>, ProgressMapd, double, double>(Square, null!);
        var transformed = new Transformed<LineSegmentCurve2D, Affine2D, Vector2f, float>(line, null!);
        var extrapolated = new Extrapolated<QuadraticBezier<double, double>, double, double>(Square, (Extrapolation)7, Extrapolation.Clamp);

        Assert.ThrowsAny<ArgumentException>(slice.Validate);
        Assert.ThrowsAny<ArgumentException>(() => (slice with { From = 0d, To = double.PositiveInfinity }).Validate());
        Assert.Throws<ArgumentNullException>(reparameterized.Validate);
        Assert.Throws<ArgumentNullException>(transformed.Validate);
        Assert.ThrowsAny<ArgumentException>(extrapolated.Validate);
        Assert.ThrowsAny<ArgumentException>(() => (extrapolated with { Before = Extrapolation.None, After = (Extrapolation)(-1) }).Validate());
        Assert.Equal(slice with { From = 0d }, (slice with { From = 0d }).Validated());
    }

    [Fact]
    public void OpsIsTheValidatedConstructionPath()
    {
        var line = LineSegmentCurve2D.Create(new(1, 2), new(4, 6));

        Assert.ThrowsAny<ArgumentException>(() => Square.Ops.Slice(double.NaN, 1d));
        Assert.Throws<ArgumentNullException>(() => Square.Ops.Reparameterize<ProgressMapd>(null!));
        Assert.Throws<ArgumentNullException>(() => line.Ops.Transform<Affine2D>(null!));
        Assert.ThrowsAny<ArgumentException>(() => Square.Ops.Extrapolated((Extrapolation)7, Extrapolation.Clamp));
    }

    [Fact]
    public void NaNPassesThroughEveryWrapper()
    {
        var ops = Square.Ops;

        Assert.True(double.IsNaN(ops.Slice(.25d, .75d).Evaluate(double.NaN)));
        Assert.True(double.IsNaN(ops.Reverse().Evaluate(double.NaN)));
        Assert.True(double.IsNaN(ops.Reparameterize(new SmoothStep()).Evaluate(double.NaN)));
        Assert.True(double.IsNaN(ops.Clamped().Evaluate(double.NaN)));
        Assert.True(double.IsNaN(ops.Extended().Evaluate(double.NaN)));
        Assert.True(double.IsNaN(ops.EvaluateClamped(double.NaN)));
    }

    [Fact]
    public void ChainedStructCurveDoesNotAllocate()
    {
        // Boxing one struct costs at least 24 bytes a call, 240,000 bytes a pass. The runtime itself can allocate a
        // few hundred bytes on this thread at any time, so the bound is under one byte per call, on the quietest pass.
        var sum = 0d;
        var allocated = long.MaxValue;
        for (var pass = 0; pass < 4; pass++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++) sum += Chain(i * 1e-4d);
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.InRange(allocated, 0, 9_999);
        Assert.True(double.IsFinite(sum));

        static double Chain(double t)
        {
            var curve = Square.Ops.Slice(.25d, .75d).Reparameterize(new SmoothStep()).Clamped();
            return curve.Evaluate(t) + curve.Derivative(t);
        }
    }

    [Fact]
    public void OpsWorksOnAnErasedCurve2D()
    {
        ICurve2D line = LineSegmentCurve2D.Create(new(1, 2), new(4, 6));

        Assert.Equal(new Vector2f(7, 10), line.Ops.Extended().Evaluate(2f));
        Assert.Equal(new Vector2f(4, 6), line.Ops.EvaluateClamped(2f));
    }

    [Fact]
    public void AProgressMapIsACurve()
    {
        var firstHalf = Easingf.Smooth.Ops.Slice(0f, .5f);

        Assert.Equal(.5f, firstHalf.Evaluate(1f));
        Assert.Equal(Easingf.Smooth.Derivative(.5f) * .5f, firstHalf.Derivative(1f));
    }

    private readonly struct SmoothStep : IDifferentiableCurve<double, double>
    {
        public Interval<double> Domain => Interval.UnitInterval<double>();
        public double Evaluate(double t) => t * t * (3 - 2 * t);
        public double Derivative(double t) => 6 * t * (1 - t);
    }

    private readonly struct CornerCurve : IDifferentiableCurve<float, float>
    {
        public Interval<float> Domain => Interval.UnitInterval<float>();
        public float Evaluate(float t) => MathF.Abs(t - .5f);
        public float Derivative(float t) => t == .5f ? float.NaN : MathF.Sign(t - .5f);
    }
}
