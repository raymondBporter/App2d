using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Physics.Motion;
using System.Numerics;

namespace App2d.Tests.Mathematics.Progress;

public sealed class EasingTravelTests
{
    [Theory]
    [InlineData(ProgressMapKind.Linear, EasingTravel.Forward, 0d, 1d)]
    [InlineData(ProgressMapKind.Reverse, EasingTravel.Backward, 1d, 0d)]
    [InlineData(ProgressMapKind.PingPong, EasingTravel.ForwardAndBackward, 0d, 0d)]
    [InlineData(ProgressMapKind.ReversePingPong, EasingTravel.BackwardAndForward, 1d, 1d)]
    public void TravelDescribesEndpointsIndependentlyOfContinuity(ProgressMapKind kind, EasingTravel travel, double start, double end)
    {
        var map = ProgressMaps.FromKind(kind);
        Assert.Equal(travel, map.Travel);
        Assert.True(map.IsContinuous);
        Assert.Equal(start, map.Sample(0d));
        Assert.Equal(end, map.Sample(1d));
        if (travel == EasingTravel.ForwardAndBackward) Assert.Equal(1d, map.Sample(.5d));
        if (travel == EasingTravel.BackwardAndForward) Assert.Equal(0d, map.Sample(.5d));
    }

    [Fact]
    public void EveryNamedMapDeclaresMetadataAndTravelFilteringCoversAllKinds()
    {
        var kinds = Enum.GetValues<ProgressMapKind>();
        var grouped = Enum.GetValues<EasingTravel>().SelectMany(travel => ProgressMaps.ForTravel(travel)).ToArray();
        Assert.Equal(kinds.Order(), grouped.Order());
        foreach (var kind in kinds)
        {
            var map = ProgressMaps.FromKind(kind);
            Assert.NotNull(map.Travel);
            Assert.Equal(kind != ProgressMapKind.Step, map.IsContinuous);
            Assert.Contains(kind, ProgressMaps.ForTravel(map.Travel.Value));
        }
        Assert.Contains(ProgressMapKind.Step, ProgressMaps.ForTravel(EasingTravel.Forward));
        Assert.DoesNotContain(ProgressMapKind.Step, ProgressMaps.ForTravel(EasingTravel.Forward, continuousOnly: true));
        Assert.Null(ProgressMap.FromFunction(phase => phase * phase).Travel);
        Assert.Null(ProgressMap.FromFunction(phase => phase * phase).IsContinuous);
    }

    [Fact]
    public void StepHoldsUntilTheEndAndDeclaresItsDiscontinuity()
    {
        var step = ProgressMaps.FromKind(ProgressMapKind.Step);
        Assert.Equal(EasingTravel.Forward, step.Travel);
        Assert.False(step.IsContinuous);
        foreach (var phase in new[] { -1d, 0d, .5d, double.BitDecrement(1d) })
        {
            Assert.Equal(0d, step.Sample(phase));
            Assert.Equal(0d, step.Derivative(phase));
        }
        Assert.Equal(1d, step.Sample(1d));
        Assert.Equal(1d, step.Sample(2d));
        Assert.True(double.IsNaN(step.Derivative(1d)));
        Assert.True(double.IsNaN(step.Sample(double.NaN)));
        Assert.Equal(-2d, ProgressMaps.ReversePingPong.Derivative(.25d));
        Assert.Equal(2d, ProgressMaps.ReversePingPong.Derivative(.5d));
    }

    [Fact]
    public void PhysicalMotionRefusesDiscontinuousMaps()
    {
        var curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, Vector2.UnitX);
        var definition = new CurveMotionDefinition2D { Curve = curve, Repeat = false, Progress = [ProgressMapKind.Step] };
        Assert.Throws<ArgumentException>(definition.Validate);
        Assert.Throws<ArgumentException>(() => new CurveMotion2D(curve, 1d, ProgressMaps.Step, repeat: false));
    }

    [Fact]
    public void TransformationsDoNotGuessContinuityWhenCustomEndpointOrRangeContractsAreUnknown()
    {
        var half = new ProgressMap(phase => phase / 2d, _ => .5d, isContinuous: true);
        var reflectedHalves = ProgressMaps.EaseInOut(half);
        Assert.Null(reflectedHalves.IsContinuous);
        Assert.True(reflectedHalves.Sample(double.BitDecrement(.5d)) < .3d);
        Assert.True(reflectedHalves.Sample(.5d) > .7d);

        var doubled = new ProgressMap(phase => 2d * phase, _ => 2d, isContinuous: true);
        var continuousOnlyOnTheNormalizedDomain = new ProgressMap(phase => phase < 1.5d ? 0d : 1d, _ => 0d, isContinuous: true);
        Assert.Null(ProgressMaps.Compose(doubled, continuousOnlyOnTheNormalizedDomain).IsContinuous);
        Assert.True(ProgressMaps.Compose(ProgressMaps.PingPong, Easing.Smooth).IsContinuous);
    }
}
