using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A radius and an angle in radians, counter-clockwise from +X in Y-up coordinates.
/// Radius is finite and nonnegative. Angles are finite and retain full turns; they are not
/// automatically wrapped. Default is the origin. Equality compares the stored coordinates.
/// </summary>
public readonly record struct Polar<TReal> where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public Polar(TReal radius, TReal angleRadians)
    {
        Ensure.NonNegative(radius);
        Ensure.Finite(angleRadians);
        Radius = radius;
        AngleRadians = angleRadians;
    }

    public TReal Radius { get; }
    public TReal AngleRadians { get; }

    public void Deconstruct(out TReal radius, out TReal angleRadians) =>
        (radius, angleRadians) = (Radius, AngleRadians);

    public TVector ToCartesian<TVector>() where TVector : unmanaged, IVector2<TVector, TReal> =>
        TVector.Direction(AngleRadians) * Radius;

    public static TVector ToCartesian<TVector>(TReal radius, TReal angleRadians) where TVector : unmanaged, IVector2<TVector, TReal> =>
        new Polar<TReal>(radius, angleRadians).ToCartesian<TVector>();

    /// <summary>Returns the heading of a finite vector, with positive zero for the zero vector.</summary>
    public static TReal AngleOf<TVector>(TVector direction) where TVector : struct, INumberTuple2<TVector, TReal>
    {
        Ensure.Finite(direction.X, nameof(direction));
        Ensure.Finite(direction.Y, nameof(direction));
        return direction.X == TReal.Zero && direction.Y == TReal.Zero ? TReal.Zero : TReal.Atan2(direction.Y, direction.X);
    }

    /// <summary>Converts a finite vector whose magnitude fits the chosen precision.</summary>
    public static Polar<TReal> FromCartesian<TVector>(TVector value) where TVector : struct, INumberTuple2<TVector, TReal>
    {
        Ensure.Finite(value.X, nameof(value));
        Ensure.Finite(value.Y, nameof(value));
        // Preserve the existing float rounding and use a scaled magnitude for wider coordinates.
        var radius = typeof(TReal) == typeof(float) || typeof(TReal) == typeof(Half)
            ? TReal.CreateTruncating(new Vector2d(double.CreateTruncating(value.X), double.CreateTruncating(value.Y)).Length())
            : TReal.Hypot(value.X, value.Y);
        Ensure.Finite(radius, nameof(value));
        var angle = value.X == TReal.Zero && value.Y == TReal.Zero ? TReal.Zero : TReal.Atan2(value.Y, value.X);
        return new(radius, angle);
    }
}

/// <summary>Convenient concrete return types for the float and double polar representations.</summary>
public static class Polar2Extensions
{
    extension(Polar<float> polar)
    {
        public Vector2f ToCartesian() => polar.ToCartesian<Vector2f>();
        public static Vector2f ToCartesian(float radius, float angleRadians) => Polar<float>.ToCartesian<Vector2f>(radius, angleRadians);
    }

    extension(Polar<double> polar)
    {
        public Vector2d ToCartesian() => polar.ToCartesian<Vector2d>();
        public static Vector2d ToCartesian(double radius, double angleRadians) => Polar<double>.ToCartesian<Vector2d>(radius, angleRadians);
    }
}
