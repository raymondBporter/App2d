using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Kinematics;

/// <summary>
/// A named place on a skeleton where something can be attached. It sits at one joint and takes its angle from elsewhere:
/// that joint, a different joint, nothing (it stays upright), or by aiming its +Y at a joint. <see cref="Local"/> is a fixed
/// offset and turn on top. What is attached is drawn in the mount's own axes. <see cref="Layer"/> is added to its joint's
/// layer for whatever is drawn on it: a sword on the back sits behind the body its joint belongs to.
/// </summary>
public readonly record struct Mount2D(string Name, int At, int Turn, int Toward, RigidTransform2D Local, float Layer = 0)
{
    /// <summary>Sits on a joint and turns with it.</summary>
    public static Mount2D On(string name, int joint, RigidTransform2D local = default, float layer = 0) => new(name, joint, joint, Skeleton2D.NoParent, local, layer);
    /// <summary>Sits on one joint and turns with another, as a held sword sits in the hand but turns with the shoulder.</summary>
    public static Mount2D Turning(string name, int at, int with, RigidTransform2D local = default, float layer = 0) => new(name, at, with, Skeleton2D.NoParent, local, layer);
    /// <summary>Sits on a joint and never turns.</summary>
    public static Mount2D Upright(string name, int at, RigidTransform2D local = default, float layer = 0) => new(name, at, Skeleton2D.NoParent, Skeleton2D.NoParent, local, layer);
    /// <summary>Sits on a joint with its +Y pointing at another, as a shin guard points from the foot to the knee.</summary>
    public static Mount2D Aimed(string name, int at, int toward, RigidTransform2D local = default, float layer = 0) => new(name, at, at, toward, local, layer);

    /// <summary>Where the mount is, given every joint's transform in the skeleton's own space.</summary>
    public RigidTransform2D World(ReadOnlySpan<RigidTransform2D> world)
    {
        var angle = Turn == Skeleton2D.NoParent ? 0 : world[Turn].Rotation;
        if (Toward != Skeleton2D.NoParent)
        {
            // Two joints in the same place give no direction to aim along; the mount then turns with its joint.
            var aim = world[Toward].Translation - world[At].Translation;
            if (aim.LengthSquared() > 1e-10f) angle = aim.AngleRadians - MathF.PI / 2;
        }
        return Local.Then(new(world[At].Translation, angle));
    }
}
