using System.Numerics;
using System.Runtime.Intrinsics;

namespace App2d.Core.Mathematics;

/// <summary>A vector of four doubles. Arithmetic follows IEEE 754 without validation.</summary>
public readonly record struct Vector4d(double X, double Y, double Z, double W) : IVector<Vector4d, double>
{
    public Vector4d(Vector4 value) : this(value.X, value.Y, value.Z, value.W) { }

    public static int Dimension => 4;
    public static Vector4d FromCoordinates(ReadOnlySpan<double> coordinates) => new(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
    public static Vector4d Zero => default;
    public static Vector4d One => new(1, 1, 1, 1);
    public static Vector4d UnitX => new(1, 0, 0, 0);
    public static Vector4d UnitY => new(0, 1, 0, 0);
    public static Vector4d UnitZ => new(0, 0, 1, 0);
    public static Vector4d UnitW => new(0, 0, 0, 1);
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        3 => W,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public static Vector4d operator +(Vector4d left, Vector4d right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
    public static Vector4d operator -(Vector4d left, Vector4d right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
    public static Vector4d operator -(Vector4d value) => new(-value.X, -value.Y, -value.Z, -value.W);
    public static Vector4d operator *(Vector4d value, double scale) => new(value.X * scale, value.Y * scale, value.Z * scale, value.W * scale);
    public static Vector4d operator *(double scale, Vector4d value) => value * scale;
    public static Vector4d operator /(Vector4d value, double scale)
    {
        var quotient = Vector256.Create(value.X, value.Y, value.Z, value.W) / Vector256.Create(scale);
        return new(quotient.GetElement(0), quotient.GetElement(1), quotient.GetElement(2), quotient.GetElement(3));
    }

    public static double Dot(Vector4d left, Vector4d right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z + left.W * right.W;
    public static Vector4d Min(Vector4d left, Vector4d right) => new(double.Min(left.X, right.X), double.Min(left.Y, right.Y), double.Min(left.Z, right.Z), double.Min(left.W, right.W));
    public static Vector4d Max(Vector4d left, Vector4d right) => new(double.Max(left.X, right.X), double.Max(left.Y, right.Y), double.Max(left.Z, right.Z), double.Max(left.W, right.W));
    public static Vector4d Abs(Vector4d value) => new(double.Abs(value.X), double.Abs(value.Y), double.Abs(value.Z), double.Abs(value.W));
    public static bool IsFinite(Vector4d value) => double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z) && double.IsFinite(value.W);

    public double Dot(Vector4d other) => Dot(this, other);
    public Vector4d Min(Vector4d other) => Min(this, other);
    public Vector4d Max(Vector4d other) => Max(this, other);
    public Vector4d Abs() => Abs(this);
    public bool IsFinite() => IsFinite(this);
    public double LengthSquared() => IVectorExtensions.LengthSquared<Vector4d, double>(this);
    public double Length() => IVectorExtensions.Length<Vector4d, double>(this);
    public Vector4d Normalized() => IVectorExtensions.Normalize<Vector4d, double>(this);
    public double DistanceSquared(Vector4d other) => IVectorExtensions.DistanceSquared<Vector4d, double>(this, other);
    public double Distance(Vector4d other) => IVectorExtensions.Distance<Vector4d, double>(this, other);
    public Vector4d Lerp(Vector4d other, double amount) => IVectorExtensions.Lerp<Vector4d, double>(this, other, amount);
    public Vector4d Midpoint(Vector4d other) => IVectorExtensions.Midpoint<Vector4d, double>(this, other);
    public Vector4d Project(Vector4d unitNormal) => IVectorExtensions.Project<Vector4d, double>(this, unitNormal);
    public Vector4d Reject(Vector4d unitNormal) => IVectorExtensions.Reject<Vector4d, double>(this, unitNormal);
    public Vector4d Reflect(Vector4d unitNormal) => IVectorExtensions.Reflect<Vector4d, double>(this, unitNormal);
    public Vector4d Clamp(Vector4d minimum, Vector4d maximum) => IVectorExtensions.Clamp<Vector4d, double>(this, minimum, maximum);

    [System.Text.Json.Serialization.JsonIgnore]
    public Vector3d XYZ => new(X, Y, Z);
    public Vector4 ToVector4() => new((float)X, (float)Y, (float)Z, (float)W);
    public override string ToString() => $"<{X}, {Y}, {Z}, {W}>";
}
