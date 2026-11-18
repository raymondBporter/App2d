using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// A rigid frame inside a shape's local space: a center and a unit axis for the frame's +X, with +Y the counter-clockwise
/// perpendicular. Oriented shapes keep their extents in this frame and project every query through it, so the raw
/// axis-aligned operations stay the only geometry. The constructor does not validate; callers pass a unit axis.
/// </summary>
public readonly record struct OrientedFrame2D(Vector2f Center, Vector2f Axis)
{
    /// <summary>How far an axis length may stray from one and still validate; matches <see cref="Mathematics.Transforms.Similarity2D"/>.</summary>
    public const float AxisTolerance = 0.001f;

    /// <summary>The frame's +Y: the counter-clockwise perpendicular of <see cref="Axis"/>.</summary>
    public Vector2f Perp => Axis.PerpCcw;

    /// <summary>True when the frame's +X is exactly local +X.</summary>
    public bool IsAxisAligned => Axis == Vector2f.UnitX;

    /// <summary>The heading of <see cref="Axis"/> in radians, counter-clockwise from local +X.</summary>
    public float AngleRadians => Polar<float>.AngleOf(Axis);

    /// <summary>The frame-to-local matrix in the row-vector convention of <see cref="Matrix3x2"/>.</summary>
    public Matrix3x2 Matrix => new(Axis.X, Axis.Y, Perp.X, Perp.Y, Center.X, Center.Y);

    /// <summary>Maps a local point into the frame.</summary>
    public Vector2f ToFrame(Vector2f point)
    {
        var offset = point - Center;
        return new(Vector2f.Dot(Axis, offset), Vector2f.Dot(Perp, offset));
    }

    /// <summary>Maps a frame point into local space.</summary>
    public Vector2f FromFrame(Vector2f point) => Center + Axis * point.X + Perp * point.Y;

    /// <summary>Maps a local direction into the frame; the transpose of the rotation, with no translation.</summary>
    public Vector2f ToFrameDirection(Vector2f direction) => new(Vector2f.Dot(Axis, direction), Vector2f.Dot(Perp, direction));

    /// <summary>Maps a frame direction into local space.</summary>
    public Vector2f FromFrameDirection(Vector2f direction) => Axis * direction.X + Perp * direction.Y;

    /// <summary>Maps every entry from the frame into local space in place.</summary>
    public void MapFromFrame(Span<Vector2f> points)
    {
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = FromFrame(points[i]);
        }
    }

    /// <summary>The local axis-aligned bounds of a box with these half extents in the frame, grown by an optional margin on every side.</summary>
    public Rect2f BoxBounds(Vector2f halfSize, float inflate = 0f)
    {
        var perp = Perp;
        var extent = new Vector2f(halfSize.X * MathF.Abs(Axis.X) + halfSize.Y * MathF.Abs(perp.X) + inflate, halfSize.X * MathF.Abs(Axis.Y) + halfSize.Y * MathF.Abs(perp.Y) + inflate);
        return new(Center - extent, Center + extent);
    }

    /// <summary>The local axis-aligned bounds of an ellipse with these radii in the frame.</summary>
    public Rect2f EllipseBounds(Vector2f radii)
    {
        var perp = Perp;
        var extent = new Vector2f(MathF.Sqrt(Square(radii.X * Axis.X) + Square(radii.Y * perp.X)), MathF.Sqrt(Square(radii.X * Axis.Y) + Square(radii.Y * perp.Y)));
        return new(Center - extent, Center + extent);
    }

    /// <summary>An unset axis reads as local +X, so default values and object initializers stay axis-aligned.</summary>
    public static Vector2f AxisOrUnitX(Vector2f axis) => axis == default ? Vector2f.UnitX : axis;

    /// <summary>The unit axis at a heading in radians; a zero heading is exactly local +X.</summary>
    public static Vector2f AxisFromAngle(float angleRadians) => angleRadians == 0f ? Vector2f.UnitX : Vector2f.Direction(angleRadians);

    /// <summary>Rejects an axis that is not finite or not within <see cref="AxisTolerance"/> of unit length.</summary>
    public static void ValidateAxis(Vector2f axis, string name)
    {
        Ensure.Finite(axis, name);
        Ensure.LessThanOrEqual(MathF.Abs(axis.Length() - 1f), AxisTolerance, name);
    }

    private static float Square(float value) => value * value;
}
