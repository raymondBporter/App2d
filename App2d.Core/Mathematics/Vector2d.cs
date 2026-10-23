using System.Numerics;

namespace App2d.Core.Mathematics;

public interface IVecvtor<TSelf, Real> :
    IAdditionOperators<TSelf, TSelf, TSelf>,
    ISubtractionOperators<TSelf, TSelf, TSelf>,
    IDivisionOperators<TSelf, Real, TSelf>,
    IMultiplyOperators<TSelf, Real, TSelf>,
    IUnaryNegationOperators<TSelf, TSelf>,
    IAdditiveIdentity<TSelf, TSelf>,
    IMultiplicativeIdentity<TSelf, Real>
    where TSelf : IVecvtor<TSelf, Real>
    where Real : IBinaryFloatingPointIeee754<Real>
{
    static abstract int Dimension { get; }
    static abstract Real Length { get; }
    static abstract Real LengthSquared { get; }
    static abstract TSelf Zero { get; }
    static abstract TSelf One { get; }
}

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

    /// <summary>Twice the signed triangle area; positive for a counter-clockwise turn in Y-up coordinates.</summary>
    public double Orientation(Vector2d b, Vector2d c) => (b - this).Cross(c - this);

    public Vector2d PerpCcw => new(-Y, X);
    public Vector2d PerpCw => new(Y, -X);
    public bool IsFinite() => double.IsFinite(X) && double.IsFinite(Y);

    public static double DistanceSquared(Vector2d a, Vector2d b) => (a - b).LengthSquared();
    public static double Distance(Vector2d a, Vector2d b) => (a - b).Length();

    /// <summary>Transforms a point using Matrix3x2's row-vector convention, with double-precision arithmetic.</summary>
    public static Vector2d Transform(Vector2d point, Matrix3x2 matrix) => new(
        point.X * matrix.M11 + point.Y * matrix.M21 + matrix.M31,
        point.X * matrix.M12 + point.Y * matrix.M22 + matrix.M32);

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector2 ToVector2() => new((float)X, (float)Y);
}
