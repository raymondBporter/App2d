using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Vector2Extensions
{
    public static readonly Vector2 NegativeInfinity = new(float.NegativeInfinity, float.NegativeInfinity);
    public static readonly Vector2 PositiveInfinity = new(float.NegativeInfinity, float.NegativeInfinity);

    extension(Vector2 value)
    {
        /// <summary>Heading in radians from +X, counter-clockwise in Y-up space. Zero vectors return zero.</summary>
        public float AngleRadians => Polar2D.AngleOf(value);

        public Polar2D ToPolar() => Polar2D.FromCartesian(value);

        public float Cross(Vector2 right) => value.X * right.Y - value.Y * right.X;

        /// <summary>Rotates 90° counter-clockwise (in +Y-up orientation).</summary>
        public Vector2 PerpCcw => new(-value.Y, value.X);

        /// <summary>Rotates 90° clockwise (in +Y-up orientation).</summary>
        public Vector2 PerpCw => new(value.Y, -value.X);
    }
}
