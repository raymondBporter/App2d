using System.Numerics;

namespace App2d.Core.Mathematics;

public static class Vector2Extensions
{
    public static readonly Vector2 NegativeInfinity = new(float.NegativeInfinity, float.NegativeInfinity);
    public static readonly Vector2 PositiveInfinity = new(float.PositiveInfinity, float.PositiveInfinity);

    /// <summary>The perpendicular dot product of two vectors given as double components.</summary>
    public static double CrossDouble(double firstX, double firstY, double secondX, double secondY) =>
        firstX * secondY - firstY * secondX;

    extension(Vector2 value)
    {
        /// <summary>Heading in radians from +X, counter-clockwise in Y-up space. Zero vectors return zero.</summary>
        public float AngleRadians => Polar2D.AngleOf(value);

        public Polar2D ToPolar() => Polar2D.FromCartesian(value);

        /// <summary>Rotates counter-clockwise by radians in Y-up coordinates.</summary>
        public Vector2 Rotate(float radians) => Rotation2D.Apply(value, radians);

        /// <summary>Rotates this point about a pivot.</summary>
        public Vector2 RotateAround(Vector2 pivot, float radians) => Rotation2D.ApplyAround(value, pivot, radians);

        /// <summary>The perpendicular dot product, rounded to a float.</summary>
        public float Cross(Vector2 right) => (float)value.CrossDouble(right);

        /// <summary>The perpendicular dot product with double intermediates for geometric predicates.</summary>
        public double CrossDouble(Vector2 right) => Vector2Extensions.CrossDouble(value.X, value.Y, right.X, right.Y);

        /// <summary>Twice the signed area of triangle value, B, C; positive for a counter-clockwise turn.</summary>
        public double Orientation(Vector2 b, Vector2 c) =>
            Vector2Extensions.CrossDouble((double)b.X - value.X, (double)b.Y - value.Y, (double)c.X - value.X, (double)c.Y - value.Y);

        /// <summary>Rotates 90° counter-clockwise (in +Y-up orientation).</summary>
        public Vector2 PerpCcw => new(-value.Y, value.X);

        /// <summary>Rotates 90° clockwise (in +Y-up orientation).</summary>
        public Vector2 PerpCw => new(value.Y, -value.X);
    }
}
