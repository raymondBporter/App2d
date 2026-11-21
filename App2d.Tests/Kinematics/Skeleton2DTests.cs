using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Kinematics;

public sealed class Skeleton2DTests
{
    private const int Body = 0, Shoulder = 1, Elbow = 2, Hand = 3, Hip = 4;

    // An arm of lengths 3 and 2 hanging from a body at (1, 2), plus a hip the arm must never disturb.
    private static Skeleton2D Figure() => new(
        new Joint2D("body", Skeleton2D.NoParent, new(new(1, 2))),
        new Joint2D("shoulder", Body, new(new(0, 1))),
        new Joint2D("elbow", Shoulder, new(new(3, 0))),
        new Joint2D("hand", Elbow, new(new(2, 0))),
        new Joint2D("hip", Body, new(new(0, -1))));

    private static RigidTransform2D[] World(Skeleton2D skeleton, RigidTransform2D[] local)
    {
        var world = new RigidTransform2D[skeleton.Count];
        skeleton.ToWorld(local, world);
        return world;
    }

    [Fact]
    public void RestPosePlacesEveryJointThroughItsParents()
    {
        var skeleton = Figure();
        var world = World(skeleton, skeleton.RestPose());

        AssertClose(new(1, 2), world[Body].Translation);
        AssertClose(new(1, 3), world[Shoulder].Translation);
        AssertClose(new(4, 3), world[Elbow].Translation);
        AssertClose(new(6, 3), world[Hand].Translation);
        AssertClose(new(1, 1), world[skeleton.IndexOf("hip")].Translation);
    }

    [Fact]
    public void TurningAJointCarriesItsChildrenAndNothingElse()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        local[Shoulder] = local[Shoulder] with { Rotation = MathF.PI / 2 };
        var world = World(skeleton, local);

        AssertClose(new(1, 6), world[Elbow].Translation);
        AssertClose(new(1, 8), world[Hand].Translation);
        Assert.Equal(MathF.PI / 2, world[Hand].Rotation, 5);
        AssertClose(new(1, 1), world[Hip].Translation);
    }

    [Fact]
    public void WorldOfOneJointMatchesTheWholePose()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        local[Body] = new(new(-2, 5), .7f);
        local[Shoulder] = local[Shoulder] with { Rotation = -1.3f };
        local[Elbow] = local[Elbow] with { Rotation = .4f };
        var world = World(skeleton, local);

        for (var i = 0; i < skeleton.Count; i++)
        {
            AssertClose(world[i].Translation, skeleton.WorldOf(local, i).Translation);
            Assert.Equal(world[i].Rotation, skeleton.WorldOf(local, i).Rotation, 5);
        }
    }

    [Fact]
    public void JointsMustFollowTheirParentsAndHaveDistinctNames()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Skeleton2D(new Joint2D("child", 1, default), new Joint2D("parent", Skeleton2D.NoParent, default)));
        Assert.ThrowsAny<ArgumentException>(() => new Skeleton2D(new Joint2D("a", Skeleton2D.NoParent, default), new Joint2D("a", 0, default)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ReachLandsTheEndOnTheTargetByTurningOnlyTheTwoJointsAboveIt(int bend)
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        var before = (RigidTransform2D[])local.Clone();
        var target = new Vector2f(4, 6);

        Assert.True(SkeletonIk2D.Reach(skeleton, local, Hand, target, bend));
        var world = World(skeleton, local);

        AssertClose(target, world[Hand].Translation);
        Assert.Equal(3, Vector2f.Distance(world[Shoulder].Translation, world[Elbow].Translation), 4);
        Assert.Equal(2, Vector2f.Distance(world[Elbow].Translation, world[Hand].Translation), 4);
        Assert.Equal(before[Body], local[Body]);
        Assert.Equal(before[Hand], local[Hand]);
        Assert.Equal(before[Hip], local[Hip]);
        Assert.Equal(before[Shoulder].Translation, local[Shoulder].Translation);
        Assert.Equal(before[Elbow].Translation, local[Elbow].Translation);
    }

    [Fact]
    public void BendChoosesTheSideTheMiddleJointFoldsTo()
    {
        var skeleton = Figure();
        var target = new Vector2f(4, 6);
        float Side(int bend)
        {
            var local = skeleton.RestPose();
            SkeletonIk2D.Reach(skeleton, local, Hand, target, bend);
            var world = World(skeleton, local);
            return Vector2f.Cross(target - world[Shoulder].Translation, world[Elbow].Translation - world[Shoulder].Translation);
        }

        Assert.True(Side(1) > .1f);
        Assert.True(Side(-1) < -.1f);
    }

    [Fact]
    public void ReachWorksUnderAMovedAndTurnedParentAndAnAlreadyBentLimb()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        local[Body] = new(new(-3, .5f), 2.2f);
        local[Shoulder] = local[Shoulder] with { Rotation = -.9f };
        local[Elbow] = local[Elbow] with { Rotation = 1.4f };
        var shoulder = skeleton.WorldOf(local, Shoulder).Translation;
        var target = shoulder + new Vector2f(-2.5f, 1.5f);

        Assert.True(SkeletonIk2D.Reach(skeleton, local, Hand, target, -1));

        AssertClose(target, skeleton.WorldOf(local, Hand).Translation);
        AssertClose(shoulder, skeleton.WorldOf(local, Shoulder).Translation);
    }

    [Fact]
    public void PlaceReachesAndAlsoTurnsTheEndToTheTarget()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        local[Body] = new(new(-3, .5f), 2.2f);
        var target = new RigidTransform2D(skeleton.WorldOf(local, Shoulder).Translation + new Vector2f(-2.5f, 1.5f), .3f);

        Assert.True(SkeletonIk2D.Place(skeleton, local, Hand, target, 1));

        var hand = skeleton.WorldOf(local, Hand);
        AssertClose(target.Translation, hand.Translation);
        Assert.Equal(0, Angles.WrapSigned(target.Rotation - hand.Rotation), 4);
    }

    [Fact]
    public void ReachStaysWithinHalfATurnOfTheCurrentPose()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        local[Shoulder] = local[Shoulder] with { Rotation = 4 * MathF.Tau };

        SkeletonIk2D.Reach(skeleton, local, Hand, new(4, 6), 1);

        Assert.InRange(local[Shoulder].Rotation, 4 * MathF.Tau - MathF.PI, 4 * MathF.Tau + MathF.PI);
    }

    [Fact]
    public void AnOutOfReachTargetStraightensTheLimbTowardIt()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();

        Assert.False(SkeletonIk2D.Reach(skeleton, local, Hand, new(1, 103), 1));

        AssertClose(new(1, 8), skeleton.WorldOf(local, Hand).Translation);
    }

    [Fact]
    public void AMountSitsOnOneJointAndTakesItsAngleFromWhereItIsTold()
    {
        var skeleton = Figure();
        var local = skeleton.RestPose();
        local[Body] = new(new(1, 2), .5f);
        local[Shoulder] = local[Shoulder] with { Rotation = .25f };
        local[Elbow] = local[Elbow] with { Rotation = 1 };
        var world = World(skeleton, local);
        var offset = new RigidTransform2D(new(.5f, 0), .1f);

        var on = Mount2D.On("on", Hand, offset).World(world);
        var turning = Mount2D.Turning("turning", Hand, Body, offset).World(world);
        var upright = Mount2D.Upright("upright", Hand, offset).World(world);
        var aimed = Mount2D.Aimed("aimed", Hand, Elbow).World(world);

        // Each offset is laid out along the angle the mount takes, from the hand.
        AssertClose(world[Hand].Translation + Vector2f.Rotate(new(.5f, 0), 1.75f), on.Translation);
        Assert.Equal(1.85f, on.Rotation, 5);
        AssertClose(world[Hand].Translation + Vector2f.Rotate(new(.5f, 0), .5f), turning.Translation);
        Assert.Equal(.6f, turning.Rotation, 5);
        AssertClose(world[Hand].Translation + new Vector2f(.5f, 0), upright.Translation);
        Assert.Equal(.1f, upright.Rotation, 5);
        // The forearm runs from the elbow to the hand at 1.75, so the hand's +Y aims back along it.
        AssertClose(world[Hand].Translation, aimed.Translation);
        AssertClose(Vector2f.Rotate(new(0, 1), aimed.Rotation), Vector2f.Rotate(new(-1, 0), 1.75f));
    }

    [Fact]
    public void FreeLimitsLeaveAPoseAlone()
    {
        var rest = new RigidTransform2D(new(1, 0), MathF.PI / 2);
        var moved = new RigidTransform2D(new(7, -4), 9);

        Assert.Equal(moved, JointLimits2D.Free.Clamp(rest, moved));
    }

    [Fact]
    public void AHingeTurnsWithinItsLimitsAndDoesNotSlide()
    {
        var rest = new RigidTransform2D(new(1, 0), MathF.PI / 2);
        var limits = JointLimits2D.Hinge(new(-1, .5f));

        var high = limits.Clamp(rest, new(new(7, -4), MathF.PI / 2 + 2));
        var low = limits.Clamp(rest, new(new(7, -4), MathF.PI / 2 - 2));

        AssertClose(rest.Translation, high.Translation);
        Assert.Equal(MathF.PI / 2 + .5f, high.Rotation, 5);
        Assert.Equal(MathF.PI / 2 - 1, low.Rotation, 5);
    }

    [Fact]
    public void ASliderMovesOnlyAlongItsRestAxisWithinItsTravel()
    {
        // Rest X points along the parent's +Y, so the slider travels up and down in the parent.
        var rest = new RigidTransform2D(new(1, 0), MathF.PI / 2);
        var limits = JointLimits2D.Slider(new(0, 2));

        var past = limits.Clamp(rest, new(new(1.3f, 5), 3));
        var inside = limits.Clamp(rest, new(new(.2f, 1.5f), 3));

        AssertClose(new(1, 2), past.Translation);
        AssertClose(new(1, 1.5f), inside.Translation);
        Assert.Equal(rest.Rotation, past.Rotation, 5);
    }

    [Fact]
    public void ClampingAPoseAppliesEachJointsOwnLimits()
    {
        var skeleton = new Skeleton2D(
            new Joint2D("root", Skeleton2D.NoParent, default),
            new Joint2D("door", 0, new(new(2, 0)), JointLimits2D.Hinge(new(0, 1))),
            new Joint2D("bolt", 0, new(new(0, 3)), JointLimits2D.Fixed));
        var local = skeleton.RestPose();
        local[0] = new(new(5, 5), 1);
        local[1] = new(new(9, 9), 4);
        local[2] = new(new(9, 9), 4);

        skeleton.Clamp(local);

        Assert.Equal(new RigidTransform2D(new(5, 5), 1), local[0]);
        AssertClose(new(2, 0), local[1].Translation);
        Assert.Equal(1, local[1].Rotation, 5);
        AssertClose(new(0, 3), local[2].Translation);
        Assert.Equal(0, local[2].Rotation, 5);
    }

    private static void AssertClose(Vector2f expected, Vector2f actual) =>
        Assert.True(Vector2f.Distance(expected, actual) < 1e-3f, $"Expected {expected}, got {actual}.");
}
