using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>A bone's world-space XY frame. Local +X runs from its origin to its tip.</summary>
public readonly record struct BoneFrame2D(Vector2 Origin, float Angle, float Length)
{
    public Vector2 Tip => At(new(Length, 0));
    public Vector2 At(Vector2 local) => Origin + Rotation2D.Apply(local, Angle);

    public static BoneFrame2D FromTransform(Matrix3x2 world, float length) =>
        new(new(world.M31, world.M32), MathF.Atan2(world.M12, world.M11), length);
}
