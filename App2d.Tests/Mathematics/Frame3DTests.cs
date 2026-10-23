using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Frame3DTests
{
    [Fact]
    public void ReflectedShearedFramesRoundTripPointsAndSeparateDirectionsFromTranslation()
    {
        var frame = new Frame3D(new(4, -3, 7), new(-2, .5f, 0), new(.75f, 3, 1), new(0, .25f, 2));
        var point = new Vector3(2, -1, 3);
        Near(new(-.75f, -4.25f, 12), frame.TransformPoint(point));
        Near(new(-4.75f, -1.25f, 5), frame.TransformDirection(point));
        Near(Vector3.Transform(point, frame.Matrix), frame.TransformPoint(point));
        Near(Vector3.TransformNormal(point, frame.Matrix), frame.TransformDirection(point));
        Assert.True(frame.TryToLocal(frame.TransformPoint(point), out var local));
        Near(point, local);
        Assert.True(frame.TryInverse(out var inverse));
        Near(point, inverse.TransformPoint(frame.TransformPoint(point)));
    }

    [Fact]
    public void CompositionAppliesTheLocalFrameThenTheParentInAllThreeDimensions()
    {
        var local = Frame3D.FromRotation(new(.3f, -.6f, .2f), new(2, 1, -3));
        var parent = new Frame3D(new(12, -3, 8), new(-2, 0, 0), new(.5f, 3, 0), new(0, 0, 4));
        var point = new Vector3(4, 2, 1);
        var composed = local.Then(parent);
        Near(parent.TransformPoint(local.TransformPoint(point)), composed.TransformPoint(point));
        Near(Vector3.Transform(point, local.Matrix * parent.Matrix), composed.TransformPoint(point));
        var moved = composed.WithOrigin(Vector3.One);
        Assert.Equal(Vector3.One, moved.Origin);
        Assert.Equal(composed.XAxis, moved.XAxis);
        Assert.Equal(composed.YAxis, moved.YAxis);
        Assert.Equal(composed.ZAxis, moved.ZAxis);
    }

    [Fact]
    public void RotationOrderIsXThenYThenZAndTheNormalRotatesWithTheFrame()
    {
        var frame = Frame3D.FromRotation(new(MathF.PI / 2, MathF.PI / 2, MathF.PI / 2));
        Near(-Vector3.UnitZ, frame.XAxis);
        Near(Vector3.UnitY, frame.YAxis);
        Near(Vector3.UnitX, frame.ZAxis);
        var twist = Frame3D.FromRotation(new(MathF.PI / 2, 0, 0));
        Near(Vector3.UnitZ, twist.YAxis);
        Near(-Vector3.UnitY, twist.ZAxis);
    }

    [Fact]
    public void LargeOriginsRemainExactAndCollapsedFramesReportNoInverse()
    {
        var frame = new Frame3D(new(100_000_000, -200_000_000, 300_000_000), new(.3f, .8f, .1f), new(.45f, .7f, 0), Vector3.UnitZ);
        Assert.True(frame.TryToLocal(frame.Origin, out var local));
        Assert.Equal(Vector3.Zero, local);
        var collapsed = new Frame3D(new(5, 7, 2), Vector3.Zero, Vector3.UnitY, Vector3.UnitZ);
        Assert.Equal(new Vector3(5, 10, 6), collapsed.TransformPoint(new(20, 3, 4)));
        Assert.False(collapsed.TryToLocal(Vector3.Zero, out _));
        Assert.False(collapsed.TryInverse(out _));
        Assert.Throws<InvalidOperationException>(() => collapsed.ToLocal(Vector3.Zero));
        Assert.Equal(Vector3.One, Frame3D.Identity.TransformPoint(Vector3.One));
        Frame3D zero = default;
        Assert.Equal(Vector3.Zero, Vector3.Transform(Vector3.One, zero.Matrix));
        var parent = Frame3D.Identity.WithOrigin(new(1, 2, 3));
        Assert.Equal(parent.Origin, zero.Then(parent).TransformPoint(Vector3.One));
        Assert.False(zero.TryInverse(out _));
    }

    [Fact]
    public void PerspectiveAndNonfiniteFramesAreRejected()
    {
        var perspective = Matrix4x4.Identity;
        perspective.M14 = .5f;
        Assert.Throws<ArgumentException>(() => new Frame3D(perspective));
        Assert.Throws<ArgumentOutOfRangeException>(() => Frame3D.FromRotation(new(float.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Frame3D.Identity.WithOrigin(new(float.PositiveInfinity)));
    }

    private static void Near(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < .00001f, $"Expected {expected}, got {actual}.");
}
