using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Physics.Motion;
using System.Text.Json;

namespace App2d.Tests.Mathematics.Progress;

public sealed class ProgressMapJsonTests
{
    [Fact]
    public void CurveDirectionAndTravelRoundTripIndependently()
    {
        foreach (var curve in Enum.GetValues<EasingCurve>()) AssertRoundTrip(curve);
        foreach (var direction in Enum.GetValues<EasingDirection>()) AssertRoundTrip(direction);
        foreach (var travel in Enum.GetValues<ProgressTravel>()) AssertRoundTrip(travel);
        Assert.Equal("\"in-out\"", JsonSerializer.Serialize(EasingDirection.InOut));
        Assert.Equal("\"reverse-ping-pong\"", JsonSerializer.Serialize(ProgressTravel.ReversePingPong));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EasingCurve>("\"smoth\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EasingDirection>("0"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProgressTravel>("0"));
    }

    [Fact]
    public void SavedMotionKeepsIndependentSelectionsPositionsAndVelocities()
    {
        var definition = new CurveMotionDefinition2D
        {
            Curve = LineSegmentCurve2D.Builder.Create(Vector2f.Zero, new(10f, 0f)),
            PeriodSeconds = 4d,
            Travel = ProgressTravel.PingPong,
            Ease = EasingCurve.Cubic,
            EaseDirection = EasingDirection.InOut
        };
        var json = JsonSerializer.Serialize(definition, GeometryJson2D.JsonOptions);
        var restored = JsonSerializer.Deserialize<CurveMotionDefinition2D>(json, GeometryJson2D.JsonOptions)!;
        Assert.Equal(definition.Travel, restored.Travel);
        Assert.Equal(definition.Ease, restored.Ease);
        Assert.Equal(definition.EaseDirection, restored.EaseDirection);
        var motion = restored.CreateMotion(new(100f, 20f));
        var expected = new CurveMotion2D(definition.Curve, 4d,
            ProgressMapd.PingPong.Then(Easingd.Power(3).WithDirection(EasingDirection.InOut)), new(100f, 20f));
        foreach (var time in new[] { 0d, .25d, 1d, 2d, 2.5d, 4d, 6.25d })
        {
            Assert.Equal(expected.Position(time), motion.Position(time));
            Assert.Equal(expected.Velocity(time), motion.Velocity(time));
        }
        Assert.Equal(new Vector2f(110f, 20f), motion.Position(2d));
        Assert.Equal(Vector2f.Zero, motion.Velocity(2d));
    }

    [Theory]
    [InlineData(ProgressTravel.Forward, 0f, 10f)]
    [InlineData(ProgressTravel.Reverse, 10f, 0f)]
    [InlineData(ProgressTravel.PingPong, 0f, 0f)]
    [InlineData(ProgressTravel.ReversePingPong, 10f, 10f)]
    public void OneShotTravelHoldsItsEndpoint(ProgressTravel travel, float start, float end)
    {
        var definition = new CurveMotionDefinition2D
        {
            Curve = LineSegmentCurve2D.Builder.Create(Vector2f.Zero, new(10f, 0f)),
            PeriodSeconds = 2d,
            Repeat = false,
            Travel = travel,
            Ease = EasingCurve.Smooth
        };
        var motion = definition.CreateMotion();
        Assert.Equal(new Vector2f(start, 0f), motion.Position(0d));
        Assert.Equal(new Vector2f(end, 0f), motion.Position(2d));
        Assert.Equal(motion.Position(2d), motion.Position(10d));
        Assert.Equal(Vector2f.Zero, motion.Velocity(10d));
        if (start != end) Assert.Throws<ArgumentException>(() => (definition with { Repeat = true }).Validate());
    }

    [Fact]
    public void PhysicalMotionRejectsDiscontinuousCurvesAndInvalidSelections()
    {
        var curve = LineSegmentCurve2D.Builder.Create(Vector2f.Zero, Vector2f.UnitX);
        var definition = new CurveMotionDefinition2D { Curve = curve, Repeat = false, Ease = EasingCurve.Step };
        Assert.Throws<ArgumentException>(definition.Validate);
        Assert.Throws<ArgumentException>(() => new CurveMotion2D(curve, 1d, Easingd.Step, repeat: false));
        Assert.Throws<ArgumentOutOfRangeException>(() => (definition with { Ease = EasingCurve.Smooth, Travel = (ProgressTravel)99 }).Validate());
    }

    private static void AssertRoundTrip<T>(T value) => Assert.Equal(value, JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value)));
}
