// Generated from the authored JSON rigs by LegacySkeletonEmitter in App2d.Tests. While those rigs exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named GeneratedRigTests) instead of editing this file.
using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

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
    public const int HeadArt = 0, BodyArt = 1;

    /// <summary>The standard quadruped. The triceratops is this same skeleton with different art.</summary>
    public static Skeleton2D Skeleton { get; } = new(
        new("body", Skeleton2D.NoParent, new(new(-0.25f, 0.92f))),
        new("body-up", Body, new(new(0f, 0.5f))),
        new("head", Body, new(new(0.97f, -0.06f))),
        new("head-up", Head, new(new(0f, 0.5f))),
        new("tail", Body, new(new(-0.79f, 0.06f))),
        new("tail-up", Tail, new(new(0f, 0.5f))),
        new("far-hind-root", Body, new(new(-0.47f, -0.08f), -1.17227f)) { Layer = 0.16f },
        new("far-hind-joint", FarHindRoot, new(new(0.41231f, 0f), -0.769414f)),
        new("far-hind-foot", FarHindJoint, new(new(0.386264f, 0f), 1.94169f)),
        new("far-front-root", Body, new(new(0.63f, -0.08f), -1.87668f)) { Layer = 0.16f },
        new("far-front-joint", FarFrontRoot, new(new(0.398497f, 0f), 0.67677f)),
        new("far-front-foot", FarFrontJoint, new(new(0.386264f, 0f), 1.19991f)),
        new("near-hind-root", Body, new(new(-0.7f, -0.08f), -1.17227f)) { Layer = -0.12f },
        new("near-hind-joint", NearHindRoot, new(new(0.41231f, 0f), -0.769414f)),
        new("near-hind-foot", NearHindJoint, new(new(0.386264f, 0f), 1.94169f)),
        new("near-front-root", Body, new(new(0.4f, -0.08f), -1.87668f)) { Layer = -0.12f },
        new("near-front-joint", NearFrontRoot, new(new(0.398497f, 0f), 0.67677f)),
        new("near-front-foot", NearFrontJoint, new(new(0.386264f, 0f), 1.19991f)))
    {
        Limbs = [new(FarHindFoot, -1), new(FarFrontFoot, 1), new(NearHindFoot, -1), new(NearFrontFoot, 1)],
        Mounts = [Mount2D.Aimed("head-art", Head, HeadUp), Mount2D.Aimed("body-art", Body, BodyUp)],
    };

    /// <summary>The baby triceratops: the same joints at seven tenths of the size. It has its own clips.</summary>
    public static Skeleton2D Baby { get; } = new(
        new("body", Skeleton2D.NoParent, new(new(-0.175f, 0.644f))),
        new("body-up", Body, new(new(0f, 0.35f))),
        new("head", Body, new(new(0.679f, -0.042f))),
        new("head-up", Head, new(new(0f, 0.35f))),
        new("tail", Body, new(new(-0.553f, 0.042f))),
        new("tail-up", Tail, new(new(0f, 0.35f))),
        new("far-hind-root", Body, new(new(-0.329f, -0.056f), -1.17227f)) { Layer = 0.112f },
        new("far-hind-joint", FarHindRoot, new(new(0.288617f, 0f), -0.769414f)),
        new("far-hind-foot", FarHindJoint, new(new(0.270385f, 0f), 1.94169f)),
        new("far-front-root", Body, new(new(0.441f, -0.056f), -1.87668f)) { Layer = 0.112f },
        new("far-front-joint", FarFrontRoot, new(new(0.278948f, 0f), 0.67677f)),
        new("far-front-foot", FarFrontJoint, new(new(0.270385f, 0f), 1.19991f)),
        new("near-hind-root", Body, new(new(-0.49f, -0.056f), -1.17227f)) { Layer = -0.084f },
        new("near-hind-joint", NearHindRoot, new(new(0.288617f, 0f), -0.769414f)),
        new("near-hind-foot", NearHindJoint, new(new(0.270385f, 0f), 1.94169f)),
        new("near-front-root", Body, new(new(0.28f, -0.056f), -1.87668f)) { Layer = -0.084f },
        new("near-front-joint", NearFrontRoot, new(new(0.278948f, 0f), 0.67677f)),
        new("near-front-foot", NearFrontJoint, new(new(0.270385f, 0f), 1.1999f)))
    {
        Limbs = [new(FarHindFoot, -1), new(FarFrontFoot, 1), new(NearHindFoot, -1), new(NearFrontFoot, 1)],
        Mounts = [Mount2D.Aimed("head-art", Head, HeadUp), Mount2D.Aimed("body-art", Body, BodyUp)],
    };
}
