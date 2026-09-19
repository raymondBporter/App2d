using App2d.Core.Kinematics;
using System.Numerics;

namespace App2d.Tests.Kinematics;

public sealed class TwoBoneIk2DTests
{
    [Fact]
    public void ReachesTargetAndPreservesBothLinkLengths()
    {
        var target = new Vector2(6f, 4f);
        var pose = TwoBoneIk2D.Solve(Vector2.Zero, target, 5f, 4f, 1);

        Assert.True(pose.ReachesTarget);
        AssertClose(5f, Vector2.Distance(pose.Root, pose.Joint));
        AssertClose(4f, Vector2.Distance(pose.Joint, pose.End));
        AssertClose(target, pose.End);
    }

    [Fact]
    public void ClampsUnreachableTargetToMaximumReach()
    {
        var pose = TwoBoneIk2D.Solve(Vector2.Zero, new Vector2(100f, 0f), 5f, 4f, 1);

        Assert.False(pose.ReachesTarget);
        Assert.Equal(8.999f, pose.End.X, 3);
        Assert.Equal(0f, pose.End.Y, 3);
    }

    [Fact]
    public void BendDirectionChoosesOppositeJointSolutions()
    {
        var positive = TwoBoneIk2D.Solve(Vector2.Zero, new Vector2(6f, 0f), 5f, 4f, 1);
        var negative = TwoBoneIk2D.Solve(Vector2.Zero, new Vector2(6f, 0f), 5f, 4f, -1);

        Assert.Equal(positive.Joint.X, negative.Joint.X, 5);
        Assert.Equal(positive.Joint.Y, -negative.Joint.Y, 5);
    }

    [Fact]
    public void RejectsZeroBendDirection() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TwoBoneIk2D.Solve(Vector2.Zero, Vector2.One, 1f, 1f, 0));

    private static void AssertClose(float expected, float actual) =>
        Assert.Equal(expected, actual, 4);

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        AssertClose(expected.X, actual.X);
        AssertClose(expected.Y, actual.Y);
    }
}

