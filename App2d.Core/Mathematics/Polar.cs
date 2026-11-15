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

    public Vec2<TReal> ToCartesian() => Vec2<TReal>.Direction(AngleRadians) * Radius;

    public static Vec2<TReal> ToCartesian(TReal radius, TReal angleRadians) =>
        new Polar<TReal>(radius, angleRadians).ToCartesian();

    /// <summary>Returns the heading of a finite vector, with positive zero for the zero vector.</summary>
    public static TReal AngleOf(Vec2<TReal> direction)
    {
        Ensure.Finite(direction, nameof(direction));
        return direction == Vec2<TReal>.Zero ? TReal.Zero : direction.AngleRadians;
    }

    /// <summary>Converts a finite vector whose magnitude fits the chosen precision.</summary>
    public static Polar<TReal> FromCartesian(Vec2<TReal> value)
    {
        Ensure.Finite(value, nameof(value));
        // Preserve the existing float rounding and use a scaled magnitude for wider coordinates.
        var radius = typeof(TReal) == typeof(float) || typeof(TReal) == typeof(Half)
            ? TReal.CreateTruncating(value.ToVector2d().Length())
            : TReal.Hypot(value.X, value.Y);
        Ensure.Finite(radius, nameof(value));
        var angle = value == Vec2<TReal>.Zero ? TReal.Zero : value.AngleRadians;
        return new(radius, angle);
    }
}
