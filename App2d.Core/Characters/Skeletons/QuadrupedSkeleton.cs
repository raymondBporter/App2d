using App2d.Core.Kinematics;

namespace App2d.Core.Characters.Skeletons;

/// <summary>A four-legged body seen from the side, facing +X: body, head and tail, and a near and a far pair of legs that IK places on the ground.</summary>
public static class QuadrupedSkeleton
{
    public const int
        Body = 0, BodyUp = 1, Head = 2, HeadUp = 3, Tail = 4, TailUp = 5, FarHindRoot = 6, FarHindJoint = 7, FarHindFoot = 8, FarFrontRoot = 9,
        FarFrontJoint = 10, FarFrontFoot = 11, NearHindRoot = 12, NearHindJoint = 13, NearHindFoot = 14, NearFrontRoot = 15, NearFrontJoint = 16,
        NearFrontFoot = 17;
    /// <summary>Indices into the skeleton's limbs.</summary>
    public const int FarHind = 0, FarFront = 1, NearHind = 2, NearFront = 3;
    /// <summary>Indices into the skeleton's mounts.</summary>
    public const int
        HeadArt = 0, BodyArt = 1, TailToTailUp = 2, FarHindRootToFarHindJoint = 3, FarHindJointToFarHindFoot = 4, FarHindFootUpright = 5,
        FarFrontRootToFarFrontJoint = 6, FarFrontJointToFarFrontFoot = 7, FarFrontFootUpright = 8, NearHindRootToNearHindJoint = 9,
        NearHindJointToNearHindFoot = 10, NearHindFootUpright = 11, NearFrontRootToNearFrontJoint = 12, NearFrontJointToNearFrontFoot = 13,
        NearFrontFootUpright = 14, BodyToBodyUp = 15, HeadToHeadUp = 16;

    /// <summary>The standard quadruped. The triceratops is this same skeleton with different art.</summary>
    public static Skeleton2D Skeleton { get; } = new(
        new("body", Skeleton2D.NoParent, new(new(-0.25f, 0.92f))),
        new("body-up", Body, new(new(0f, 0.5f))),
        new("head", Body, new(new(0.97f, -0.06f))),
        new("head-up", Head, new(new(0f, 0.5f))),
        new("tail", Body, new(new(-0.79f, 0.06f))),
        new("tail-up", Tail, new(new(0f, 0.5f))),
        new("far-hind-root", Body, new(new(-0.47f, -0.08f), -1.1723f)) { Layer = 0.16f },
        new("far-hind-joint", FarHindRoot, new(new(0.4123f, 0f), -0.7694f)),
        new("far-hind-foot", FarHindJoint, new(new(0.3863f, 0f), 1.9417f)),
        new("far-front-root", Body, new(new(0.63f, -0.08f), -1.8767f)) { Layer = 0.16f },
        new("far-front-joint", FarFrontRoot, new(new(0.3985f, 0f), 0.6768f)),
        new("far-front-foot", FarFrontJoint, new(new(0.3863f, 0f), 1.1999f)),
        new("near-hind-root", Body, new(new(-0.7f, -0.08f), -1.1723f)) { Layer = -0.12f },
        new("near-hind-joint", NearHindRoot, new(new(0.4123f, 0f), -0.7694f)),
        new("near-hind-foot", NearHindJoint, new(new(0.3863f, 0f), 1.9417f)),
        new("near-front-root", Body, new(new(0.4f, -0.08f), -1.8767f)) { Layer = -0.12f },
        new("near-front-joint", NearFrontRoot, new(new(0.3985f, 0f), 0.6768f)),
        new("near-front-foot", NearFrontJoint, new(new(0.3863f, 0f), 1.1999f)))
    {
        Limbs = [new(FarHindFoot, -1), new(FarFrontFoot, 1), new(NearHindFoot, -1), new(NearFrontFoot, 1)],
        Mounts = [
            Mount2D.Aimed("head-art", Head, HeadUp), Mount2D.Aimed("body-art", Body, BodyUp), Mount2D.Aimed("tail-to-tail-up", Tail, TailUp),
            Mount2D.Aimed("far-hind-root-to-far-hind-joint", FarHindRoot, FarHindJoint),
            Mount2D.Aimed("far-hind-joint-to-far-hind-foot", FarHindJoint, FarHindFoot), Mount2D.Upright("far-hind-foot-upright", FarHindFoot),
            Mount2D.Aimed("far-front-root-to-far-front-joint", FarFrontRoot, FarFrontJoint),
            Mount2D.Aimed("far-front-joint-to-far-front-foot", FarFrontJoint, FarFrontFoot), Mount2D.Upright("far-front-foot-upright", FarFrontFoot),
            Mount2D.Aimed("near-hind-root-to-near-hind-joint", NearHindRoot, NearHindJoint),
            Mount2D.Aimed("near-hind-joint-to-near-hind-foot", NearHindJoint, NearHindFoot), Mount2D.Upright("near-hind-foot-upright", NearHindFoot),
            Mount2D.Aimed("near-front-root-to-near-front-joint", NearFrontRoot, NearFrontJoint),
            Mount2D.Aimed("near-front-joint-to-near-front-foot", NearFrontJoint, NearFrontFoot),
            Mount2D.Upright("near-front-foot-upright", NearFrontFoot), Mount2D.Aimed("body-to-body-up", Body, BodyUp),
            Mount2D.Aimed("head-to-head-up", Head, HeadUp)],
    };

    /// <summary>The baby triceratops: the same joints at seven tenths of the size. It has its own clips.</summary>
    public static Skeleton2D Baby { get; } = new(
        new("body", Skeleton2D.NoParent, new(new(-0.175f, 0.644f))),
        new("body-up", Body, new(new(0f, 0.35f))),
        new("head", Body, new(new(0.679f, -0.042f))),
        new("head-up", Head, new(new(0f, 0.35f))),
        new("tail", Body, new(new(-0.553f, 0.042f))),
        new("tail-up", Tail, new(new(0f, 0.35f))),
        new("far-hind-root", Body, new(new(-0.329f, -0.056f), -1.1723f)) { Layer = 0.112f },
        new("far-hind-joint", FarHindRoot, new(new(0.2886f, 0f), -0.7694f)),
        new("far-hind-foot", FarHindJoint, new(new(0.2704f, 0f), 1.9417f)),
        new("far-front-root", Body, new(new(0.441f, -0.056f), -1.8767f)) { Layer = 0.112f },
        new("far-front-joint", FarFrontRoot, new(new(0.2789f, 0f), 0.6768f)),
        new("far-front-foot", FarFrontJoint, new(new(0.2704f, 0f), 1.1999f)),
        new("near-hind-root", Body, new(new(-0.49f, -0.056f), -1.1723f)) { Layer = -0.084f },
        new("near-hind-joint", NearHindRoot, new(new(0.2886f, 0f), -0.7694f)),
        new("near-hind-foot", NearHindJoint, new(new(0.2704f, 0f), 1.9417f)),
        new("near-front-root", Body, new(new(0.28f, -0.056f), -1.8767f)) { Layer = -0.084f },
        new("near-front-joint", NearFrontRoot, new(new(0.2789f, 0f), 0.6768f)),
        new("near-front-foot", NearFrontJoint, new(new(0.2704f, 0f), 1.1999f)))
    {
        Limbs = [new(FarHindFoot, -1), new(FarFrontFoot, 1), new(NearHindFoot, -1), new(NearFrontFoot, 1)],
        Mounts = [
            Mount2D.Aimed("head-art", Head, HeadUp), Mount2D.Aimed("body-art", Body, BodyUp), Mount2D.Aimed("tail-to-tail-up", Tail, TailUp),
            Mount2D.Aimed("far-hind-root-to-far-hind-joint", FarHindRoot, FarHindJoint),
            Mount2D.Aimed("far-hind-joint-to-far-hind-foot", FarHindJoint, FarHindFoot), Mount2D.Upright("far-hind-foot-upright", FarHindFoot),
            Mount2D.Aimed("far-front-root-to-far-front-joint", FarFrontRoot, FarFrontJoint),
            Mount2D.Aimed("far-front-joint-to-far-front-foot", FarFrontJoint, FarFrontFoot), Mount2D.Upright("far-front-foot-upright", FarFrontFoot),
            Mount2D.Aimed("near-hind-root-to-near-hind-joint", NearHindRoot, NearHindJoint),
            Mount2D.Aimed("near-hind-joint-to-near-hind-foot", NearHindJoint, NearHindFoot), Mount2D.Upright("near-hind-foot-upright", NearHindFoot),
            Mount2D.Aimed("near-front-root-to-near-front-joint", NearFrontRoot, NearFrontJoint),
            Mount2D.Aimed("near-front-joint-to-near-front-foot", NearFrontJoint, NearFrontFoot),
            Mount2D.Upright("near-front-foot-upright", NearFrontFoot), Mount2D.Aimed("body-to-body-up", Body, BodyUp),
            Mount2D.Aimed("head-to-head-up", Head, HeadUp)],
    };
}
