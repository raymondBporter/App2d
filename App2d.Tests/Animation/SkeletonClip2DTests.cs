using App2d.Core.Animation;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Animation;

public sealed class SkeletonClip2DTests
{
    private const int Body = 0, Shoulder = 1, Elbow = 2, Hand = 3, Hip = 4, Knee = 5, Foot = 6;
    private const int Arm = 0, Leg = 1;

    // The body stands at (0, 4). The arm hangs from a shoulder and rides on it; the leg reaches the ground and stays with it.
    private static Skeleton2D Figure() => new(
        new Joint2D("body", Skeleton2D.NoParent, new(new(0, 4))),
        new Joint2D("shoulder", Body, new(new(0, 1))),
        new Joint2D("elbow", Shoulder, new(new(2, 0))),
        new Joint2D("hand", Elbow, new(new(2, 0))),
        new Joint2D("hip", Body, new(default, -MathF.PI / 2)),
        new Joint2D("knee", Hip, new(new(2, 0), -.2f)),
        new Joint2D("foot", Knee, new(new(2, 0), MathF.PI / 2 + .2f)))
    {
        Limbs = [new(Hand, 1, Shoulder), new(Foot, 1)],
    };

    private static RigidTransform2D[] Pose(SkeletonClip2D clip, Skeleton2D skeleton, float time)
    {
        var local = new RigidTransform2D[skeleton.Count];
        clip.Pose(skeleton, time, local);
        var world = new RigidTransform2D[skeleton.Count];
        skeleton.ToWorld(local, world);
        return world;
    }

    [Fact]
    public void AnEmptyClipLeavesEveryJointWhereItRests()
    {
        var skeleton = Figure();
        var world = Pose(new("still", 1), skeleton, .5f);

        for (var i = 0; i < skeleton.Count; i++)
        {
            AssertClose(skeleton.RestWorld[i].Translation, world[i].Translation);
        }
    }

    [Fact]
    public void JointKeysMoveAndTurnFromRest()
    {
        var skeleton = Figure();
        var clip = new SkeletonClip2D("nod", 2)
        {
            Joints = [new(Body, Move: Track.Of<Vector2f>(new(0, default), new(2, new(1, -2))), Turn: Track.Linear(new(0, 0), new(2, 1)))],
        };
        var local = new RigidTransform2D[skeleton.Count];
        clip.Sample(skeleton, 1, local, new LimbSample2D[skeleton.Limbs.Length]);

        Assert.Equal(new RigidTransform2D(new(.5f, 3), .5f), local[Body]);
        Assert.Equal(skeleton.Joints[Shoulder].Rest, local[Shoulder]);
    }

    [Fact]
    public void AnArmFollowsItsFrameWhileALegStaysWithTheGround()
    {
        var skeleton = Figure();
        var clip = new SkeletonClip2D("lean", 1) { Joints = [new(Body, Move: Track.Constant(new Vector2f(.5f, -.25f)))] };
        var world = Pose(clip, skeleton, 0);

        AssertClose(skeleton.RestWorld[Hand].Translation + new Vector2f(.5f, -.25f), world[Hand].Translation);
        AssertClose(skeleton.RestWorld[Foot].Translation, world[Foot].Translation);
        Assert.Equal(0, Angles.WrapSigned(world[Foot].Rotation), 4);
    }

    [Fact]
    public void LimbKeysMoveTheEndInItsFrameAndCanFlipTheBend()
    {
        var skeleton = Figure();
        var clip = new SkeletonClip2D("wave", 2)
        {
            Joints = [new(Shoulder, Turn: Track.Constant(MathF.PI / 2))],
            Limbs = [new(Arm, Target: Track.Constant(new Vector2f(-1, 1)), Bend: Track.Held<int>(new(0, 1), new(1, -1)))],
        };
        var before = Pose(clip, skeleton, .5f);
        var after = Pose(clip, skeleton, 1.5f);

        // The hand rests 4 along the shoulder's X; the key moves it to (3, 1) in the shoulder's axes, which now point up.
        var expected = skeleton.RestWorld[Shoulder].Translation + new Vector2f(-1, 3);
        AssertClose(expected, before[Hand].Translation);
        AssertClose(expected, after[Hand].Translation);
        static float Side(RigidTransform2D[] world) => Vector2f.Cross(world[Hand].Translation - world[Shoulder].Translation, world[Elbow].Translation - world[Shoulder].Translation);
        Assert.True(Side(before) > .1f);
        Assert.True(Side(after) < -.1f);
    }

    [Fact]
    public void AContactPlantsTheFootWhileTheBodyTravelsPastIt()
    {
        var skeleton = Figure();
        var clip = new SkeletonClip2D("step", 2, loop: true)
        {
            Travel = Track.Of<Vector2f>(new(0, default), new(2, new(1, 0))),
            Contacts = [new(Leg, 0, 1, new(.25f, 0))],
        };
        var rest = skeleton.RestWorld[Foot].Translation;

        // In place the planted point slides back as the character travels forward; adding the travel back holds it still.
        AssertClose(rest + new Vector2f(.25f, 0), Pose(clip, skeleton, 0)[Foot].Translation);
        AssertClose(rest + new Vector2f(0, 0), Pose(clip, skeleton, .5f)[Foot].Translation);
        AssertClose(rest + new Vector2f(.25f, 0), Pose(clip, skeleton, .5f)[Foot].Translation + clip.TravelAt(skeleton, .5f));
        // Released at the finish: back to the leg's own goal.
        AssertClose(rest, Pose(clip, skeleton, 1)[Foot].Translation);
    }

    [Fact]
    public void ALayerTakesOverItsMaskAndLeavesTheRestAlone()
    {
        var skeleton = Figure();
        var walk = new SkeletonClip2D("walk", 1)
        {
            Joints = [new(Body, Move: Track.Constant(new Vector2f(0, -.5f)))],
            Limbs = [new(Arm, Target: Track.Constant(new Vector2f(-1, -1))), new(Leg, Target: Track.Constant(new Vector2f(.5f, 0)))],
        };
        var wave = new SkeletonClip2D("wave", 1)
        {
            Joints = [new(Body, Move: Track.Constant(new Vector2f(9, 9))), new(Shoulder, Turn: Track.Constant(.4f))],
            Limbs = [new(Arm, Target: Track.Constant(new Vector2f(-1, 1)), Bend: Track.Constant(-1)), new(Leg, Target: Track.Constant(new Vector2f(-9, 0)))],
        };
        var upper = new SkeletonMask2D([Shoulder], [Arm], []);
        RigidTransform2D[] local = new RigidTransform2D[skeleton.Count], over = new RigidTransform2D[skeleton.Count];
        LimbSample2D[] limbs = new LimbSample2D[2], overLimbs = new LimbSample2D[2];

        walk.Sample(skeleton, 0, local, limbs);
        wave.Sample(skeleton, 0, over, overLimbs);
        SkeletonClip2D.Overlay(local, limbs, over, overLimbs, upper, .5f);

        // The body and leg are outside the mask: the walk keeps them. The shoulder and arm are half way to the wave.
        Assert.Equal(skeleton.Joints[Body].Rest.Translation + new Vector2f(0, -.5f), local[Body].Translation);
        Assert.Equal(new LimbSample2D(new(.5f, 0), 1), limbs[Leg]);
        Assert.Equal(.2f, local[Shoulder].Rotation, 5);
        Assert.Equal(new LimbSample2D(new(-1, 0), -1, null, .5f), limbs[Arm]);

        // The arm's goal is worked out after layering, against the shoulder as the layering left it.
        SkeletonClip2D.Place(skeleton, local, limbs);
        var shoulder = skeleton.WorldOf(local, Shoulder);
        AssertClose(skeleton.RestWorld[Shoulder].Translation + new Vector2f(0, -.5f), shoulder.Translation);
        Assert.Equal(3, Vector2f.Distance(shoulder.Translation, skeleton.WorldOf(local, Hand).Translation), 3);
    }

    [Fact]
    public void APlantedFootLetsGoAsALayerTakesItsLegOver()
    {
        var skeleton = Figure();
        var stand = new SkeletonClip2D("stand", 1) { Contacts = [new(Leg, 0, 1, new(.25f, 0))] };
        var kick = new SkeletonClip2D("kick", 1) { Limbs = [new(Leg, Target: Track.Constant(new Vector2f(-.25f, .5f)))] };
        var legs = new SkeletonMask2D([], [Leg], []);
        var rest = skeleton.RestWorld[Foot].Translation;
        Vector2f FootAt(float weight)
        {
            RigidTransform2D[] local = new RigidTransform2D[skeleton.Count], over = new RigidTransform2D[skeleton.Count];
            LimbSample2D[] limbs = new LimbSample2D[2], overLimbs = new LimbSample2D[2];
            stand.Sample(skeleton, 0, local, limbs);
            kick.Sample(skeleton, 0, over, overLimbs);
            SkeletonClip2D.Overlay(local, limbs, over, overLimbs, legs, weight);
            SkeletonClip2D.Place(skeleton, local, limbs);
            return skeleton.WorldOf(local, Foot).Translation;
        }

        var planted = rest + new Vector2f(.25f, 0);
        AssertClose(planted, FootAt(0));
        // Half way: the leg's own goal is half way to the kick's key, and the foot is half way from the plant to that goal.
        AssertClose(Vector2f.Lerp(planted, rest + new Vector2f(-.125f, .25f), .5f), FootAt(.5f));
        AssertClose(rest + new Vector2f(-.25f, .5f), FootAt(1));
    }

    [Fact]
    public void MountKeysTurnAMountOutOfThePlaneAndLayerByTheMask()
    {
        var swing = new SkeletonClip2D("swing", 2) { Mounts = [new(1, Track.Of<Vector3f>(new(0, default), new(2, new(1, 0, 2))))] };
        var hold = new SkeletonClip2D("hold", 1) { Mounts = [new(0, Track.Constant(new Vector3f(9, 9, 9))), new(1, Track.Constant(new Vector3f(0, 1, 0)))] };
        Vector3f[] turns = new Vector3f[2], layer = new Vector3f[2];

        swing.SampleMounts(1, turns);
        hold.SampleMounts(0, layer);
        Assert.Equal(default, turns[0]);
        Assert.Equal(new Vector3f(.5f, 0, 1), turns[1]);

        // Only the masked mount moves toward the layer; the other keeps what was underneath.
        SkeletonClip2D.OverlayMounts(turns, layer, new SkeletonMask2D([], [], [1]), .5f);
        Assert.Equal(default, turns[0]);
        Assert.Equal(new Vector3f(.25f, .5f, .5f), turns[1]);
    }

    [Fact]
    public void LayersAddDownTheTreeAndALimbsEndTakesItsFromTheLimbsFrame()
    {
        var joints = Figure().Joints.ToArray();
        joints[Body] = joints[Body] with { Layer = .1f };
        joints[Shoulder] = joints[Shoulder] with { Layer = -.3f };
        joints[Hip] = joints[Hip] with { Layer = .2f };
        var skeleton = new Skeleton2D(joints) { Limbs = Figure().Limbs };
        var clip = new SkeletonClip2D("turn", 1)
        {
            Joints = [new(Shoulder, Layer: Track.Linear(new(0, 0), new(1, 1))), new(Hip, Layer: Track.Constant(-1f))],
        };
        float[] keyed = new float[skeleton.Count], layers = new float[skeleton.Count];

        Assert.Equal([.1f, -.2f, -.2f, -.2f, .3f, .3f, .3f], skeleton.RestLayers.Select(l => MathF.Round(l, 4)));

        clip.SampleLayers(.5f, keyed);
        skeleton.ToLayers(keyed, layers);
        // The arm is framed on its shoulder, so the hand follows the shoulder's keyed layer. The leg is framed on the ground,
        // so the foot keeps its resting layer while the hip and knee move.
        Assert.Equal([.1f, .3f, .3f, .3f, -.7f, -.7f, .3f], layers.Select(l => MathF.Round(l, 4)));

        // An overlay moves only the masked joints' keyed layers.
        float[] over = [0, 2, 0, 0, 5, 0, 0];
        SkeletonClip2D.OverlayLayers(keyed, over, new SkeletonMask2D([Shoulder], [], []), .5f);
        Assert.Equal(1.25f, keyed[Shoulder], 5);
        Assert.Equal(-1, keyed[Hip], 5);
    }

    [Fact]
    public void ABuildScalesTheMotionKeyedOnItsJointsLimbsAndTravel()
    {
        var standard = Figure();
        var joints = standard.Joints.ToArray();
        joints[Body] = joints[Body] with { MoveScale = 2 };
        var large = new Skeleton2D(joints) { Limbs = [standard.Limbs[Arm] with { TargetScale = 3 }, standard.Limbs[Leg] with { TargetScale = .5f }], TravelScale = 4 };
        var clip = new SkeletonClip2D("step", 2)
        {
            Joints = [new(Body, Move: Track.Constant(new Vector2f(.1f, -.2f)), Turn: Track.Constant(.3f))],
            Limbs = [new(Arm, Target: Track.Constant(new Vector2f(-.5f, .25f)))],
            Travel = Track.Of<Vector2f>(new(0, default), new(2, new(1, 0))),
            Contacts = [new(Leg, 0, 2, new(.5f, 0))],
        };
        var local = new RigidTransform2D[large.Count];
        var limbs = new LimbSample2D[large.Limbs.Length];
        var goals = new LimbGoal2D[large.Limbs.Length];
        clip.Sample(large, 1, local, limbs);
        SkeletonClip2D.Goals(large, local, limbs, goals);

        // Moves double; turns are angles and do not scale.
        Assert.Equal(new RigidTransform2D(large.Joints[Body].Rest.Translation + new Vector2f(.2f, -.4f), .3f), local[Body]);
        // The arm's keyed move triples, in its shoulder's frame, which the body has moved and turned.
        var shoulder = large.WorldOf(local, Shoulder);
        AssertClose(shoulder.TransformPoint(new Vector2f(4, 0) + new Vector2f(-1.5f, .75f)), goals[Arm].Target.Translation);
        // Travel quadruples, and the planted foot's keyed offset halves.
        Assert.Equal(new Vector2f(2, 0), clip.TravelAt(large, 1));
        AssertClose(large.RestWorld[Foot].Translation + new Vector2f(.25f, 0) - new Vector2f(2, 0), goals[Leg].Target.Translation);
    }

    private static void AssertClose(Vector2f expected, Vector2f actual) =>
        Assert.True(Vector2f.Distance(expected, actual) < 1e-3f, $"Expected {expected}, got {actual}.");
}
