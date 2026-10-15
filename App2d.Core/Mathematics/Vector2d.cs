using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A pair of double-precision coordinates for intermediate geometric calculations.
/// Arithmetic follows IEEE 754 without validation. Equality compares stored coordinates without a tolerance.
/// </summary>
public readonly record struct Vector2d(double X, double Y)
{
    public Vector2d(Vector2 value) : this(value.X, value.Y) { }

    public static Vector2d Zero => default;

    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator -(Vector2d value) => new(-value.X, -value.Y);
    public static Vector2d operator *(Vector2d value, double scale) => new(value.X * scale, value.Y * scale);
    public static Vector2d operator *(double scale, Vector2d value) => value * scale;
    public static Vector2d operator /(Vector2d value, double scale) => new(value.X / scale, value.Y / scale);

    public double LengthSquared() => Dot(this);
    public double Length() => Math.Sqrt(LengthSquared());
    public double Dot(Vector2d other) => X * other.X + Y * other.Y;

    /// <summary>The perpendicular dot product; positive for a counter-clockwise turn in Y-up coordinates.</summary>
    public double Cross(Vector2d other) => X * other.Y - Y * other.X;

    public Vector2d PerpCcw => new(-Y, X);
    public Vector2d PerpCw => new(Y, -X);
    public bool IsFinite() => double.IsFinite(X) && double.IsFinite(Y);

    public static double DistanceSquared(Vector2d a, Vector2d b) => (a - b).LengthSquared();
    public static double Distance(Vector2d a, Vector2d b) => (a - b).Length();

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector2 ToVector2() => new((float)X, (float)Y);
}
