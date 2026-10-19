using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Physics.Motion;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Mathematics.Progress;

public sealed class ProgressMapJsonTests
{
    [Fact]
    public void EveryBuiltInKindRoundTripsAndResolvesToTheNamedMap()
    {
        foreach (var kind in Enum.GetValues<ProgressMapKind>())
        {
            var json = JsonSerializer.Serialize(kind);
            Assert.Equal(kind, JsonSerializer.Deserialize<ProgressMapKind>(json));
            Assert.Equal(kind, ProgressMaps.FromKind(kind).Kind);
        }
        Assert.Equal("\"ping-pong\"", JsonSerializer.Serialize(ProgressMapKind.PingPong));
        Assert.Equal("\"smoother\"", JsonSerializer.Serialize(Easing.Smoother.Kind));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProgressMapKind>("\"smoth\""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProgressMapKind>("0"));
    }

    [Fact]
    public void SavedPipelineKeepsCompositionOrderPositionsAndAnalyticVelocities()
    {
        var definition = new CurveMotionDefinition2D
        {
            Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(10, 0)),
            PeriodSeconds = 4,
            Progress = [ProgressMapKind.PingPong, ProgressMapKind.Smoother]
        };
        var json = JsonSerializer.Serialize(definition, GeometryJson2D.JsonOptions);
        var restored = JsonSerializer.Deserialize<CurveMotionDefinition2D>(json, GeometryJson2D.JsonOptions)!;
        var motion = restored.CreateMotion(new(100, 20));
        var expected = new CurveMotion2D(definition.Curve, 4, ProgressMaps.Compose(ProgressMaps.PingPong, Easing.Smoother), new(100, 20));
        foreach (var time in new[] { 0d, .25, 1, 2, 2.5, 4, 6.25 })
        {
            Assert.Equal(expected.Position(time), motion.Position(time));
            Assert.Equal(expected.Velocity(time), motion.Velocity(time));
        }
        Assert.Equal(new Vector2(110, 20), motion.Position(2));
        Assert.Equal(Vector2.Zero, motion.Velocity(2));
        var reversedOrder = ProgressMaps.Compose([ProgressMapKind.Smoother, ProgressMapKind.PingPong]);
        Assert.NotEqual(motion.Position(.5), new Vector2(100, 20) + definition.Curve.Evaluate((float)reversedOrder.Sample(.125)));
    }

    [Theory]
    [InlineData(false, 0, 10)]
    [InlineData(true, 10, 0)]
    public void OneShotPathsRunThereOrBackAndHoldTheirEndpoint(bool reverse, float start, float end)
    {
        var definition = new CurveMotionDefinition2D
        {
            Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, new(10, 0)),
            PeriodSeconds = 2,
            Repeat = false,
            Progress = reverse ? [ProgressMapKind.Reverse, ProgressMapKind.Smooth] : [ProgressMapKind.Smooth]
        };
        var motion = definition.CreateMotion();
        Assert.Equal(new Vector2(start, 0), motion.Position(0));
        Assert.Equal(new Vector2(5, 0), motion.Position(1));
        Assert.Equal(new Vector2(end, 0), motion.Position(2));
        Assert.Equal(motion.Position(2), motion.Position(10));
        Assert.Equal(Vector2.Zero, motion.Velocity(10));
        Assert.Throws<ArgumentException>(() => (definition with { Repeat = true }).Validate());
    }
}
