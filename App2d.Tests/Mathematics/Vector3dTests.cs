using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Vector3dTests
{
    [Fact]
    public void WideningBeforeArithmeticPreservesDifferencesThatFloatsLose()
    {
        var first = new Vector3(16777216, 3, -16777216);
        var second = new Vector3(-1, 4, 1);
        var difference = first.ToVector3d() - second.ToVector3d();
        Assert.Equal(new Vector3d(16777217, -1, -16777217), difference);
        Assert.NotEqual((first - second).X, difference.X);
        Assert.Equal(first.ToVector3d(), difference + second.ToVector3d());
        Assert.Equal(new Vector3(16777216, -1, -16777216), difference.ToVector3());
    }

    [Fact]
    public void CrossProductIsRightHandedAndPerpendicularToBothInputs()
    {
        var a = new Vector3d(1, 2, 3);
        var b = new Vector3d(-2, 1, 4);
        var cross = a.Cross(b);
        Assert.Equal(new Vector3d(5, -10, 5), cross);
        Assert.Equal(0, cross.Dot(a));
        Assert.Equal(0, cross.Dot(b));
        Assert.Equal(-cross, b.Cross(a));
        Assert.Equal(Vector3.UnitZ.ToVector3d(), Vector3.UnitX.ToVector3d().Cross(Vector3.UnitY.ToVector3d()));
        Assert.Equal(a, (2 * a) / 2);
        Assert.Equal(Vector3d.Zero, a + -a);
    }

    [Fact]
    public void FloatExtremesHaveFiniteDoubleDistancesAndCrossProducts()
    {
        var a = new Vector3(float.MaxValue, float.MaxValue, 0).ToVector3d();
        var b = new Vector3d(-a.X, a.Y, 0);
        var cross = a.Cross(b);
        Assert.True(cross.IsFinite());
        Assert.Equal(2d * float.MaxValue * float.MaxValue, cross.Z);
        Assert.Equal(cross.Z, cross.Length());
        Assert.Equal(cross.Z * cross.Z, cross.LengthSquared());
        Assert.Equal(2 * Math.Sqrt(2) * float.MaxValue, Vector3d.Distance(a, -a), 1e24);
        Assert.True(double.IsFinite(Vector3d.DistanceSquared(a, -a)));

        var tiny = new Vector3(0, 0, float.Epsilon).ToVector3d();
        Assert.Equal(float.Epsilon, Vector3d.Distance(Vector3d.Zero, tiny));
        Assert.True(tiny.LengthSquared() > 0);
    }

    [Fact]
    public void NarrowingIsExplicitAndFollowsFloatRounding()
    {
        var value = new Vector3d(1d / 3, -(double)float.MaxValue * 2, 5);
        Assert.True(value.IsFinite());
        Assert.Equal(new Vector3(1f / 3, float.NegativeInfinity, 5), value.ToVector3());
        Assert.False(new Vector3d(double.NaN, 0, 0).IsFinite());
        Assert.False(new Vector3d(0, double.PositiveInfinity, 0).IsFinite());
        Assert.False(new Vector3d(0, 0, double.NegativeInfinity).IsFinite());
    }
}
