using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Frame2DTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(-35)]
    [InlineData(450)]
    public void DegreeFramesPlaceLocalOffsetsAndProjectDirectionsWithoutTranslation(float degrees)
    {
        var frame = Frame2D.FromRotationDegrees(degrees, new(7, -3));
        var local = new Vector2(.32f, -.16f);
        var expected = frame.Origin + Rotation2D.Apply(local, float.DegreesToRadians(degrees));
        Assert.True(Vector2.Distance(expected, frame.TransformPoint(local)) < .000002f);
        Assert.Equal(frame.XAxis, frame.Matrix.GetRows().X);
        Assert.Equal(frame.YAxis, frame.Matrix.GetRows().Y);
        Assert.True(Vector2.Distance(local, frame.Matrix.TransposeTransformDirection(frame.TransformDirection(local))) < .000002f);

        var reflected = new Frame2D(frame.Origin, frame.XAxis, -frame.YAxis);
        Assert.True(Vector2.Distance(local, reflected.Matrix.TransposeTransformDirection(reflected.TransformDirection(local))) < .000002f);
    }

    [Fact]
    public void LocalOriginRemainsExactAtLargeWorldOffsets()
    {
        var frame = new Frame2D(new(100_000_000, -200_000_000), new(.3f, .8f), new(.45f, .7f));
        Assert.True(frame.TryToLocal(frame.Origin, out var local));
        Assert.Equal(Vector2.Zero, local);
    }

    [Fact]
    public void ReflectedShearedFramesRoundTripPointsAndSeparateDirectionsFromTranslation()
    {
        var frame = new Frame2D(new(4, -3), new(-2, .5f), new(.75f, 3));
        var local = new Vector2(2, -1);
        Assert.Equal(new Vector2(-.75f, -5), frame.TransformPoint(local));
        Assert.Equal(new Vector2(-4.75f, -2), frame.TransformDirection(local));
        Assert.True(frame.TryToLocal(frame.TransformPoint(local), out var restored));
        Assert.True(Vector2.Distance(local, restored) < .00001f);
        Assert.True(frame.TryInverse(out var inverse));
        Assert.True(Vector2.Distance(local, inverse.TransformPoint(frame.TransformPoint(local))) < .00001f);
    }

    [Fact]
    public void CompositionAppliesTheLocalFrameThenTheParent()
    {
        var local = Frame2D.FromRotation(.3f, new(2, 1));
        var parent = new Frame2D(new(12, -3), new(-2, 0), new(.5f, 3));
        var point = new Vector2(4, 2);
        var composed = local.Then(parent);
        Assert.True(Vector2.Distance(parent.TransformPoint(local.TransformPoint(point)), composed.TransformPoint(point)) < .00001f);
        Assert.Equal(composed.Origin + new Vector2(1, 2), composed.WithOrigin(composed.Origin + new Vector2(1, 2)).Origin);
    }

    [Fact]
    public void CollapsedFramesRemainUsableForForwardMappingAndReportNoInverse()
    {
        var frame = new Frame2D(new(5, 7), Vector2.Zero, Vector2.UnitY);
        Assert.Equal(new Vector2(5, 10), frame.TransformPoint(new(20, 3)));
        Assert.False(frame.TryToLocal(Vector2.Zero, out _));
        Assert.False(frame.TryInverse(out _));
        Assert.Throws<InvalidOperationException>(() => frame.ToLocal(Vector2.Zero));
        Assert.Equal(Vector2.One, Frame2D.Identity.TransformPoint(Vector2.One));
    }
}
