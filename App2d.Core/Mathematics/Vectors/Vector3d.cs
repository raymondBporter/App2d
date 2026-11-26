using System.Numerics;

namespace App2d.Core.Mathematics.Vectors;

/// <summary>
/// Three double-precision coordinates for intermediate geometric calculations.
/// Arithmetic follows IEEE 754 without validation. Equality compares stored coordinates without a tolerance.
/// Only storage, constants and operators are declared here; Dot, Cross, Length and the rest are inherited from IVector3 and IVector.
/// </summary>
public readonly record struct Vector3d(double X, double Y, double Z) : IVector3<Vector3d, double>
{
    public Vector3d(Vector3 value) : this(value.X, value.Y, value.Z) { }

    public static Vector3d Create(double x, double y, double z) => new(x, y, z);
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

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector3 ToVector3() => new((float)X, (float)Y, (float)Z);
    public Vector3f ToVector3f() => new(ToVector3());
    public override string ToString() => $"<{X}, {Y}, {Z}>";
}
