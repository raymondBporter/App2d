using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A radius and an angle in radians, counter-clockwise from +X in Y-up coordinates.
/// Radius is finite and nonnegative. Angles are finite and retain full turns; they are not
/// automatically wrapped. Default is the origin. Equality compares the stored coordinates.
/// </summary>
public readonly record struct Polar2D
{
    public Polar2D(float radius, float angleRadians)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(radius);
        ArgGuard.ThrowIfNotFinite(angleRadians);
        Radius = radius;
        AngleRadians = angleRadians;
    }

    public float Radius { get; }
    public float AngleRadians { get; }

    public void Deconstruct(out float radius, out float angleRadians)
    {
        radius = Radius;
        angleRadians = AngleRadians;
    }

    public Vector2 ToCartesian() => Direction(AngleRadians) * Radius;

    /// <summary>Converts radius/angle directly without allocating a coordinate object.</summary>
    public static Vector2 ToCartesian(float radius, float angleRadians) => new Polar2D(radius, angleRadians).ToCartesian();

    /// <summary>Returns the unit direction at a finite angle. Zero radians points along +X.</summary>
    public static Vector2 Direction(float angleRadians)
    {
        ArgGuard.ThrowIfNotFinite(angleRadians);
        var (sin, cos) = MathF.SinCos(angleRadians);
        return new(cos, sin);
    }

    /// <summary>
    /// Returns a finite vector's heading in [-PI, PI]. The zero vector (including signed zero)
    /// has no geometric heading; this API uses zero. No near-zero tolerance is imposed.
    /// </summary>
    public static float AngleOf(Vector2 direction)
    {
        ArgGuard.ThrowIfNotFinite(direction);
        return direction == Vector2.Zero ? 0f : MathF.Atan2(direction.Y, direction.X);
    }

    /// <summary>
    /// Converts a finite vector relative to the origin. The resulting radius must fit a float;
    /// intermediate squares use double precision to avoid overflow/underflow for representable radii.
    /// </summary>
    public static Polar2D FromCartesian(Vector2 value)
    {
        ArgGuard.ThrowIfNotFinite(value);
        var radius = (float)Math.Sqrt((double)value.X * value.X + (double)value.Y * value.Y);
        ArgGuard.ThrowIfNotFinite(radius, nameof(value));
        return new(radius, AngleOf(value));
    }
}
