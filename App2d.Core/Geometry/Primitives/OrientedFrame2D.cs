using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// A rigid frame inside a shape's local space: a center and a unit axis for the frame's +X, with +Y the counter-clockwise
/// perpendicular. Oriented shapes keep their extents in this frame and project every query through it, so the raw
/// axis-aligned operations stay the only geometry. The constructor does not validate; callers pass a unit axis.
/// </summary>
public readonly record struct OrientedFrame2D(Vector2 Center, Vector2 Axis)
{
    /// <summary>How far an axis length may stray from one and still validate; matches <see cref="Mathematics.Transforms.Similarity2D"/>.</summary>
    public const float AxisTolerance = 0.001f;

    /// <summary>The frame's +Y: the counter-clockwise perpendicular of <see cref="Axis"/>.</summary>
    public Vector2 Perp => Axis.PerpCcw;

    /// <summary>True when the frame's +X is exactly local +X.</summary>
    public bool IsAxisAligned => Axis == Vector2.UnitX;

    /// <summary>The heading of <see cref="Axis"/> in radians, counter-clockwise from local +X.</summary>
    public float AngleRadians => Polar2D.AngleOf(Axis);

    /// <summary>The frame-to-local matrix in the row-vector convention of <see cref="Matrix3x2"/>.</summary>
    public Matrix3x2 Matrix => new(Axis.X, Axis.Y, Perp.X, Perp.Y, Center.X, Center.Y);

    /// <summary>Maps a local point into the frame.</summary>
    public Vector2 ToFrame(Vector2 point)
    {
        var offset = point - Center;
        return new(Vector2.Dot(Axis, offset), Vector2.Dot(Perp, offset));
    }

    /// <summary>Maps a frame point into local space.</summary>
    public Vector2 FromFrame(Vector2 point) => Center + Axis * point.X + Perp * point.Y;

    /// <summary>Maps a local direction into the frame; the transpose of the rotation, with no translation.</summary>
    public Vector2 ToFrameDirection(Vector2 direction) => new(Vector2.Dot(Axis, direction), Vector2.Dot(Perp, direction));

    /// <summary>Maps a frame direction into local space.</summary>
    public Vector2 FromFrameDirection(Vector2 direction) => Axis * direction.X + Perp * direction.Y;

    /// <summary>Maps every entry from the frame into local space in place.</summary>
    public void MapFromFrame(Span<Vector2> points)
    {
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = FromFrame(points[i]);
        }
    }

    /// <summary>The local axis-aligned bounds of a box with these half extents in the frame, grown by an optional margin on every side.</summary>
    public Rect2D BoxBounds(Vector2 halfSize, float inflate = 0f)
    {
        var perp = Perp;
        var extent = new Vector2(halfSize.X * MathF.Abs(Axis.X) + halfSize.Y * MathF.Abs(perp.X) + inflate, halfSize.X * MathF.Abs(Axis.Y) + halfSize.Y * MathF.Abs(perp.Y) + inflate);
        return new(Center - extent, Center + extent);
    }

    /// <summary>The local axis-aligned bounds of an ellipse with these radii in the frame.</summary>
    public Rect2D EllipseBounds(Vector2 radii)
    {
        var perp = Perp;
        var extent = new Vector2(MathF.Sqrt(Square(radii.X * Axis.X) + Square(radii.Y * perp.X)), MathF.Sqrt(Square(radii.X * Axis.Y) + Square(radii.Y * perp.Y)));
        return new(Center - extent, Center + extent);
    }

    /// <summary>An unset axis reads as local +X, so default values and object initializers stay axis-aligned.</summary>
    public static Vector2 AxisOrUnitX(Vector2 axis) => axis == default ? Vector2.UnitX : axis;

    /// <summary>The unit axis at a heading in radians.</summary>
    public static Vector2 AxisFromAngle(float angleRadians) => Polar2D.Direction(angleRadians);

    /// <summary>Rejects an axis that is not finite or not within <see cref="AxisTolerance"/> of unit length.</summary>
    public static void ValidateAxis(Vector2 axis, string name) => ArgGuard.ThrowIf(!axis.IsFinite() || MathF.Abs(axis.Length() - 1f) > AxisTolerance, "The axis must be a finite unit vector.", name);

    private static float Square(float value) => value * value;
}
