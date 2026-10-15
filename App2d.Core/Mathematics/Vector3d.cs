using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Three double-precision coordinates for intermediate geometric calculations.
/// Arithmetic follows IEEE 754 without validation. Equality compares stored coordinates without a tolerance.
/// </summary>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    public Vector3d(Vector3 value) : this(value.X, value.Y, value.Z) { }

    public static Vector3d Zero => default;

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d value) => new(-value.X, -value.Y, -value.Z);
    public static Vector3d operator *(Vector3d value, double scale) => new(value.X * scale, value.Y * scale, value.Z * scale);
    public static Vector3d operator *(double scale, Vector3d value) => value * scale;
    public static Vector3d operator /(Vector3d value, double scale) => new(value.X / scale, value.Y / scale, value.Z / scale);

    public double LengthSquared() => Dot(this);
    public double Length() => Math.Sqrt(LengthSquared());
    public double Dot(Vector3d other) => X * other.X + Y * other.Y + Z * other.Z;

    /// <summary>The right-handed cross product, perpendicular to both input vectors.</summary>
    public Vector3d Cross(Vector3d other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    public bool IsFinite() => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public static double DistanceSquared(Vector3d a, Vector3d b) => (a - b).LengthSquared();
    public static double Distance(Vector3d a, Vector3d b) => (a - b).Length();

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);
}
