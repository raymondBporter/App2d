using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Three double-precision coordinates for intermediate geometric calculations.
/// Arithmetic follows IEEE 754 without validation. Equality compares stored coordinates without a tolerance.
/// </summary>
public readonly record struct Vector3d(double X, double Y, double Z) : IVector<Vector3d, double>
{
    public Vector3d(Vector3 value) : this(value.X, value.Y, value.Z) { }

    public static int Dimension => 3;
    public static Vector3d Zero => default;
    public static Vector3d One => new(1, 1, 1);
    public static Vector3d UnitX => new(1, 0, 0);
    public static Vector3d UnitY => new(0, 1, 0);
    public static Vector3d UnitZ => new(0, 0, 1);
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3d operator -(Vector3d value) => new(-value.X, -value.Y, -value.Z);
    public static Vector3d operator *(Vector3d value, double scale) => new(value.X * scale, value.Y * scale, value.Z * scale);
    public static Vector3d operator *(double scale, Vector3d value) => value * scale;
    public static Vector3d operator /(Vector3d value, double scale) => new(value.X / scale, value.Y / scale, value.Z / scale);

    public static double Dot(Vector3d left, Vector3d right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    public static Vector3d Min(Vector3d left, Vector3d right) => new(double.Min(left.X, right.X), double.Min(left.Y, right.Y), double.Min(left.Z, right.Z));
    public static Vector3d Max(Vector3d left, Vector3d right) => new(double.Max(left.X, right.X), double.Max(left.Y, right.Y), double.Max(left.Z, right.Z));
    public static Vector3d Abs(Vector3d value) => new(double.Abs(value.X), double.Abs(value.Y), double.Abs(value.Z));
    public static bool IsFinite(Vector3d value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    /// <summary>The right-handed cross product, perpendicular to both input vectors.</summary>
    public Vector3d Cross(Vector3d other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);
    public override string ToString() => $"<{X}, {Y}, {Z}>";
}
