using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Rotation in the XY plane, in radians, without constructing a transform or shape.</summary>
public static class Rotation2D
{
    public static Vector2 Apply(Vector2 value, float radians)
    {
        if (radians == 0f) return value;
        var (sin, cos) = MathF.SinCos(radians);
        return new(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
    }

    /// <summary>Rotates XY while preserving depth exactly.</summary>
    public static Vector3 ApplyXY(Vector3 value, float radians) =>
        new(Apply(new(value.X, value.Y), radians), value.Z);
}
