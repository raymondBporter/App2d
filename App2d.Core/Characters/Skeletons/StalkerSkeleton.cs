using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

/// <summary>A three-legged stalker seen from the side, facing +X: a body, and three legs that IK places on the ground.</summary>
public static class StalkerSkeleton
{
    public const int Body = 0, Hip0 = 1, Knee0 = 2, Foot0 = 3, Hip1 = 4, Knee1 = 5, Foot1 = 6, Hip2 = 7, Knee2 = 8, Foot2 = 9;
    /// <summary>Indices into the skeleton's limbs.</summary>
    public const int Foot0Chain = 0, Foot1Chain = 1, Foot2Chain = 2;
    /// <summary>Indices into the skeleton's mounts.</summary>
    public const int Stinger = 0, BodyUpright = 1;

    /// <summary>The stalker at rest on its three legs.</summary>
    public static Skeleton2D Skeleton { get; } = new(
        new("body", Skeleton2D.NoParent, new(new(0f, 0.9f))),
        new("hip-0", Body, new(new(-0.35f, -0.1f), -1.212f)) { Layer = -0.1f },
        new("knee-0", Hip0, new(new(0.4272f, 0f), -0.7175f)),
        new("foot-0", Knee0, new(new(0.4272f, 0f), 1.9296f)),
        new("hip-1", Body, new(new(0f, -0.1f), -1.212f)),
        new("knee-1", Hip1, new(new(0.4272f, 0f), -0.7175f)),
        new("foot-1", Knee1, new(new(0.4272f, 0f), 1.9296f)),
        new("hip-2", Body, new(new(0.35f, -0.1f), -1.212f)) { Layer = 0.1f },
        new("knee-2", Hip2, new(new(0.4272f, 0f), -0.7175f)),
        new("foot-2", Knee2, new(new(0.4272f, 0f), 1.9296f)))
    {
        Limbs = [new(Foot0, 1), new(Foot1, 1), new(Foot2, 1)],
        Mounts = [Mount2D.On("stinger", Body, new(new(0.5f, -0.02f))), Mount2D.Upright("body-upright", Body)],
    };
}
