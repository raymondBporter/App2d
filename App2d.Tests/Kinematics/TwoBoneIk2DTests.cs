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
        Assert.Equal(9f * (1 - 1e-5f), pose.End.X, 4);
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

    [Theory, InlineData(2), InlineData(0)]
    public void ExactReachIncludesStraightAndFoldedConfigurations(float distance)
    {
        var pose = TwoBoneIk2D.SolveExact(Vector2.Zero, new(distance, 0), 1, 1, 1);
        AssertClose(new(distance, 0), pose.End);
        AssertClose(1, Vector2.Distance(pose.Root, pose.Joint)); AssertClose(1, Vector2.Distance(pose.Joint, pose.End));
        Assert.True(pose.ReachesTarget);
    }

    [Theory, InlineData(.02f, .01f), InlineData(1000, .01f), InlineData(.01f, 1000)]
    public void ExactReachHandlesUnequalLengthsAtBothBoundaries(float first, float second)
    {
        foreach (var target in new[] { Vector2.Zero, new Vector2(2000, 0) })
        {
            var pose = TwoBoneIk2D.SolveExact(Vector2.Zero, target, first, second, -1);
            Assert.InRange(MathF.Abs(first - Vector2.Distance(pose.Root, pose.Joint)), 0, 1e-4f);
            Assert.InRange(MathF.Abs(second - Vector2.Distance(pose.Joint, pose.End)), 0, 1e-4f);
            AssertClose(target == Vector2.Zero ? MathF.Abs(first - second) : first + second, pose.End.X);
        }
    }

    [Theory, InlineData(.4f), InlineData(1.7f), InlineData(-2.4f)]
    public void RotatedFullExtensionDoesNotAmplifyFloatNoiseIntoABend(float angle)
    {
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var pose = TwoBoneIk2D.SolveExact(Vector2.Zero, direction * 2.5f, 1.5f, 1, 1);
        Assert.InRange(Vector2.Distance(direction * 1.5f, pose.Joint), 0, 1e-5f);
    }

    private static void AssertClose(float expected, float actual) =>
        Assert.Equal(expected, actual, 4);

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        AssertClose(expected.X, actual.X);
        AssertClose(expected.Y, actual.Y);
    }
}

