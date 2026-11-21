// Generated from the authored JSON rigs by LegacySkeletonEmitter in App2d.Tests. While those rigs exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named GeneratedRigTests) instead of editing this file.
using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

/// <summary>A flying pterosaur seen from the side, facing +X. Every joint is keyed; nothing is placed by IK.</summary>
public static class PterosaurSkeleton
{
    public const int
        Body = 0, BodyUp = 1, Head = 2, HeadUp = 3, Jaw = 4, JawUp = 5, NearShoulder = 6, NearBend = 7, NearTip = 8, NearLeg = 9, NearFoot = 10,
        NearToe0 = 11, NearToe1 = 12, NearToe2 = 13, FarShoulder = 14, FarBend = 15, FarTip = 16, FarLeg = 17, FarFoot = 18, FarToe0 = 19,
        FarToe1 = 20, FarToe2 = 21;
    /// <summary>Indices into the skeleton's mounts.</summary>
    public const int HeadArt = 0, BodyArt = 1;

    /// <summary>The pterosaur at rest, wings spread.</summary>
    public static Skeleton2D Skeleton { get; } = new(
        new("body", Skeleton2D.NoParent, new(new(0f, 0.7f))),
        new("body-up", Body, new(new(0f, 0.5f))),
        new("head", Body, new(new(0.3f, 0.05f))),
        new("head-up", Head, new(new(0f, 0.5f))),
        new("jaw", Head, new(new(0.14f, -0.07f))),
        new("jaw-up", Jaw, new(new(0f, 0.5f))),
        new("near-shoulder", Body, new(new(-0.1f, 0.12f))) { Layer = -0.02f },
        new("near-bend", NearShoulder, new(new(-0.38f, 0.48f))),
        new("near-tip", NearBend, new(new(-0.6f, 0.25f))),
        new("near-leg", Body, new(new(-0.08f, -0.19f))) { Layer = -0.02f },
        new("near-foot", NearLeg, new(new(-0.01f, -0.19f))),
        new("near-toe-0", NearFoot, new(new(-0.045f, -0.05f))),
        new("near-toe-1", NearFoot, new(new(0f, -0.065f))),
        new("near-toe-2", NearFoot, new(new(0.045f, -0.05f))),
        new("far-shoulder", Body, new(new(0.1f, 0.12f))) { Layer = 0.12f },
        new("far-bend", FarShoulder, new(new(0.38f, 0.48f))),
        new("far-tip", FarBend, new(new(0.6f, 0.25f))),
        new("far-leg", Body, new(new(0.08f, -0.19f))) { Layer = 0.12f },
        new("far-foot", FarLeg, new(new(0.01f, -0.19f))),
        new("far-toe-0", FarFoot, new(new(-0.045f, -0.05f))),
        new("far-toe-1", FarFoot, new(new(0f, -0.065f))),
        new("far-toe-2", FarFoot, new(new(0.045f, -0.05f))))
    {
        Mounts = [Mount2D.Aimed("head-art", Head, HeadUp), Mount2D.Aimed("body-art", Body, BodyUp)],
    };
}
