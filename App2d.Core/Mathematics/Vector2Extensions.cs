using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Vector2Extensions
{
    public static float Cross(this Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;

    /// <summary>Rotates 90° counter-clockwise (in +Y-up orientation).</summary>
    public static Vector2 PerpCcw(this Vector2 value) => new(-value.Y, value.X);

    /// <summary>Rotates 90° clockwise (in +Y-up orientation).</summary>
    public static Vector2 PerpCw(this Vector2 value) => new(value.Y, -value.X);
}
