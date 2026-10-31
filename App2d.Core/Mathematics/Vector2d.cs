using System.Numerics;
using System.Runtime.Intrinsics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A pair of double-precision coordinates for intermediate geometric calculations.
/// Arithmetic follows IEEE 754 without validation. Equality compares stored coordinates without a tolerance.
/// </summary>
public readonly record struct Vector2d(double X, double Y) : IVector<Vector2d, double>
{
    public Vector2d(Vector2 value) : this(value.X, value.Y) { }

    public static int Dimension => 2;
    public static Vector2d Zero => default;
    public static Vector2d One => new(1, 1);
    public static Vector2d UnitX => new(1, 0);
    public static Vector2d UnitY => new(0, 1);
    public double this[int index] => index switch
    {
        0 => X,
        1 => Y,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    /// <summary>Unit vector at an angle in radians, counter-clockwise from +X in Y-up space.</summary>
    public static Vector2d FromAngle(double angleRadians)
    {
        var (sin, cos) = Math.SinCos(angleRadians);
        return new(cos, sin);
    }

    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator -(Vector2d value) => new(-value.X, -value.Y);
    public static Vector2d operator *(Vector2d value, double scale) => new(value.X * scale, value.Y * scale);
    public static Vector2d operator *(double scale, Vector2d value) => value * scale;
    public static Vector2d operator /(Vector2d value, double scale)
    {
        // One packed divide; constructing from components lets the JIT keep the other operators scalar.
        var quotient = Vector128.Create(value.X, value.Y) / Vector128.Create(scale);
        return new(quotient.GetElement(0), quotient.GetElement(1));
    }

    public static double Dot(Vector2d left, Vector2d right) => left.X * right.X + left.Y * right.Y;
    public static Vector2d Min(Vector2d left, Vector2d right) => new(double.Min(left.X, right.X), double.Min(left.Y, right.Y));
    public static Vector2d Max(Vector2d left, Vector2d right) => new(double.Max(left.X, right.X), double.Max(left.Y, right.Y));
    public static Vector2d Abs(Vector2d value) => new(double.Abs(value.X), double.Abs(value.Y));
    public static bool IsFinite(Vector2d value) => double.IsFinite(value.X) && double.IsFinite(value.Y);

    /// <summary>The perpendicular dot product; positive for a counter-clockwise turn in Y-up coordinates.</summary>
    public double Cross(Vector2d other) => X * other.Y - Y * other.X;

    /// <summary>Twice the signed triangle area; positive for a counter-clockwise turn in Y-up coordinates.</summary>
    public double Orientation(Vector2d b, Vector2d c) => (b - this).Cross(c - this);

    [System.Text.Json.Serialization.JsonIgnore]
    public Vector2d PerpCcw => new(-Y, X);
    [System.Text.Json.Serialization.JsonIgnore]
    public Vector2d PerpCw => new(Y, -X);
    /// <summary>Transforms a point using Matrix3x2's row-vector convention, with double-precision arithmetic.</summary>
    public static Vector2d Transform(Vector2d point, Matrix3x2 matrix) => new(
        point.X * matrix.M11 + point.Y * matrix.M21 + matrix.M31,
        point.X * matrix.M12 + point.Y * matrix.M22 + matrix.M32);

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector2 ToVector2() => new((float)X, (float)Y);
    public override string ToString() => $"<{X}, {Y}>";
}
