using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Mathematics;

public sealed class InterpolatorTests
{
    private sealed record State(float Intensity, Vector2f Offset, string Label);

    [Fact]
    public void VectorChannelsCanUseIndependentCurvesAndLeaveTheWholeValueRuleInPlace()
    {
        var rule = Interpolation.Default<Vector3d, double>().WithProgress(Easingd.Smooth)
            .With(Vector3d.Channels.X, Easingd.Power(2))
            .With(Vector3d.Channels.Y, Easingd.Linear);
        Assert.Equal(new Vector3d(.5d, 4d, 5d), rule.Evaluate(Vector3d.Zero, new(8d, 16d, 32d), .25d));
    }

    [Fact]
    public void LaterOverlappingChannelsWinAndAlwaysReadTheOriginalEndpoints()
    {
        var rule = Interpolation.Default<Vector2f, float>()
            .With(Vector2f.Channels.V, Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth))
            .With(Vector2f.Channels.X, Easingf.Power(2))
            .With(Vector2f.Channels.X, Easingf.Power(3));
        Assert.Equal(new Vector2f(1f, 8f), rule.Evaluate(Vector2f.Zero, new(8f, 16f), .5f));
    }

    [Fact]
    public void AnOuterProgressMapIsSampledOnceAndTransformsAllItsChildren()
    {
        var samples = 0;
        var map = new ProgressMapf(time => { samples++; return time * time; },
            _ => throw new InvalidOperationException("Value interpolation needs no derivative."));
        var whole = Interpolation.Default<Vector2f, float>()
            .With(Vector2f.Channels.X, Interpolation.Linear<float>())
            .With(Vector2f.Channels.Y, Interpolation.Linear<float>())
            .WithProgress(map);
        Assert.Equal(new Vector2f(2f, 4f), whole.Evaluate(Vector2f.Zero, new(8f, 16f), .5f));
        Assert.Equal(1, samples);

        var separate = Interpolation.Default<Vector2f, float>().WithProgress(map)
            .With(Vector2f.Channels.X, Interpolation.Linear<float>());
        Assert.Equal(new Vector2f(4f, 4f), separate.Evaluate(Vector2f.Zero, new(8f, 16f), .5f));
        Assert.Equal(2, samples);
    }

    [Fact]
    public void NewTypesAndArbitraryChannelsNeedNoInterpolationInterface()
    {
        var intensity = new InterpolationChannel<State, float>(static value => value.Intensity,
            static (value, replacement) => value with { Intensity = replacement });
        var offset = new InterpolationChannel<State, Vector2f>(static value => value.Offset,
            static (value, replacement) => value with { Offset = replacement });
        var label = new InterpolationChannel<State, string>(static value => value.Label,
            static (value, replacement) => value with { Label = replacement });
        var rule = Interpolation.Hold<State, float>()
            .With(intensity, Easingf.Smooth)
            .With(offset, Interpolation.Default<Vector2f, float>())
            .With(label, Interpolation.Hold<string, float>());
        var start = new State(0f, Vector2f.Zero, "start");
        var end = new State(8f, new(8f, 16f), "end");
        Assert.Equal(new State(4f, new(4f, 8f), "start"), rule.Evaluate(start, end, .5f));
    }

    [Fact]
    public void RigidTranslationAndRotationUseTheirOwnRulesWhileRetainingRigidity()
    {
        var rule = Interpolation.Default<RigidTransform2D, float>()
            .With(RigidTransform2D.Channels.Translation, Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth))
            .With(RigidTransform2D.Channels.Rotation, Easingf.Power(2));
        var value = rule.Evaluate(RigidTransform2D.Identity, new(new(8f, 16f), 2f * MathF.Tau), .5f);
        Assert.Equal(new Vector2f(4f, 8f), value.Translation);
        Assert.Equal(MathF.PI, value.Rotation);
        Assert.Equal(1f, value.TransformDirection(Vector2f.UnitX).Length(), 6);
    }

    [Fact]
    public void RgbCanBeInterpolatedAsAGroupWithoutChangingAlpha()
    {
        var rgb = Interpolation.Hold<Vector4f, float>()
            .With(Vector4f.Channels.RGB, Interpolation.Default<Vector3f, float>().WithProgress(Easingf.Smooth));
        var start = new Vector4f(0f, 0f, 0f, .2f);
        var end = new Vector4f(8f, 16f, 24f, .8f);
        Assert.Equal(new Vector4f(4f, 8f, 12f, .2f), rgb.Evaluate(start, end, .5f));
        Assert.Equal(.35f, rgb.With(Vector4f.Channels.A, Easingf.Power(2)).Evaluate(start, end, .5f).W, 6);

        var precise = Interpolation.Hold<Vector4d, double>()
            .With(Vector4d.Channels.R, Easingd.Linear).With(Vector4d.Channels.A, Easingd.Linear);
        Assert.Equal(new Vector4d(1.000000000025d, 2d, 3d, .25d),
            precise.Evaluate(new(1d, 2d, 3d, 0d), new(1.0000000001d, 9d, 9d, 1d), .25d));
    }

    [Fact]
    public void PolarInterpolationFollowsAnArcAndLetsTheCallerChooseEachCoordinateRule()
    {
        var rule = Interpolation.Polar<Vector2f, float>();
        var middle = rule.Evaluate(Vector2f.UnitX, Vector2f.UnitY, .5f);
        Assert.Equal(1f, middle.Length(), 6);
        Assert.InRange(Vector2f.Distance(new(MathF.Sqrt(.5f)), middle), 0f, 1e-6f);
        Assert.NotEqual(Vector2f.Lerp(Vector2f.UnitX, Vector2f.UnitY, .5f), middle);

        var a = Vector2d.FromAngle(170d * Math.PI / 180d);
        var b = Vector2d.FromAngle(-170d * Math.PI / 180d);
        Assert.InRange(Vector2d.Distance(new(-1d, 0d), Interpolation.Polar<Vector2d, double>().Evaluate(a, b, .5d)), 0d, 1e-14d);
        var longArc = Interpolation.Polar<Vector2d, double>(angle: Interpolation.Linear<double>());
        Assert.InRange(Vector2d.Distance(Vector2d.UnitX, longArc.Evaluate(a, b, .5d)), 0d, 1e-14d);
    }

    [Fact]
    public void PolarOriginAndSignedRadiusOvershootHaveExplicitBehavior()
    {
        var rule = Interpolation.Polar<Vector2f, float>();
        Assert.InRange(Vector2f.Distance(Vector2f.UnitY, rule.Evaluate(Vector2f.Zero, new(0f, 2f), .5f)), 0f, 1e-6f);
        var signed = Interpolation.Polar<Vector2d, double>(radius: new Interpolator<double, double>(static (_, _, _) => -2d));
        Assert.Equal(new Vector2d(-2d, 0d), signed.Evaluate(Vector2d.UnitX, new(2d, 0d), .5d));
    }

    [Fact]
    public void CoordinateConversionCanComposeAnyCoordinateChannels()
    {
        var coordinates = Interpolation.Default<Polar<double>, double>()
            .With(Polar<double>.Channels.Radius, Easingd.Smooth)
            .With(Polar<double>.Channels.Angle, Interpolation.ShortestAngle<double>());
        var rule = coordinates.InCoordinates(static value => value.ToPolar(), static value => value.ToCartesian());
        Assert.InRange(Vector2d.Distance(new(Math.Sqrt(2d)), rule.Evaluate(Vector2d.UnitX, new(0d, 3d), .5d)), 0d, 1e-14d);
    }

    [Fact]
    public void BezierStrategiesSupportRelativeTimingAndAbsoluteExcursionsWithoutLosingPrecision()
    {
        var absolute = Interpolation.Bezier(1f / 3f, 20f, 2f / 3f, 20f, absolute: true);
        Assert.Equal(17.5f, absolute.Evaluate(10f, 10f, .5f));
        var relative = Interpolation.Bezier(0f, 1f / 3f, 0f, 2f / 3f);
        Assert.Equal(5f, relative.Evaluate(0f, 10f, .125f), 5);
        var precise = Interpolation.Bezier(1d / 3d, 1.0000000001d, 2d / 3d, 1.0000000001d, absolute: true);
        Assert.Equal(1.000000000075d, precise.Evaluate(1d, 1d, .5d), 15);
    }

    [Fact]
    public void ClampingRestrictsOnlyTheDeclaredInputAndPreservesOutputOvershoot()
    {
        var rule = Interpolation.Linear<double>().WithProgress(ProgressMapd.Affine(1.2d, 0d).FromRange(2d, 6d));
        Assert.Equal(12d, rule.EvaluateClamped(0d, 10d, 20d));
        Assert.Equal(54d, rule.Evaluate(0d, 10d, 20d), 12);

        var absolute = Interpolation.Bezier(1f / 3f, -4f, 2f / 3f, 16f, absolute: true);
        Assert.Equal(11f, absolute.Evaluate(2f, 8f, -.5f));
        Assert.Equal(2f, absolute.EvaluateClamped(2f, 8f, -.5f));
        Assert.Equal(8f, absolute.EvaluateClamped(2f, 8f, 1.5f));
    }

    [Fact]
    public void ChannelOverridesAndCoordinateConversionsRetainTheInputSpan()
    {
        var curve = Interpolation.Default<Vector2d, double>().WithProgress(Easingd.Linear.FromRange(2d, 6d))
            .With(Vector2d.Channels.X, Easingd.Smooth.FromRange(2d, 6d));
        Assert.Equal(new Vector2d(.625d, 2d), curve.EvaluateClamped(Vector2d.Zero, new(4d, 8d), 3d));
        Assert.Equal(new Vector2d(4d, 8d), curve.EvaluateClamped(Vector2d.Zero, new(4d, 8d), 20d));
        var converted = curve.InCoordinates(static value => value, static value => value);
        Assert.Equal(2d, converted.InputBegin);
        Assert.Equal(6d, converted.InputEnd);
    }
}
