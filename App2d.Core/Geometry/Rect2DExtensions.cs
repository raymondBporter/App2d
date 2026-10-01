using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Shared rectangle properties and queries for any <see cref="IRect2D"/>. Generic receivers keep value-type
/// rectangles unboxed. All coordinates belong to the rectangle's own space; Top means Max.Y.
/// </summary>
public static class Rect2DExtensions
{
    extension<T>(T rectangle) where T : IRect2D
    {
        /// <summary>Min.X.</summary>
        public float Left => rectangle.Min.X;

        /// <summary>Max.X.</summary>
        public float Right => rectangle.Max.X;

        /// <summary>Min.Y.</summary>
        public float Bottom => rectangle.Min.Y;

        /// <summary>Max.Y.</summary>
        public float Top => rectangle.Max.Y;

        /// <summary>The extent along X.</summary>
        public float Width => rectangle.Max.X - rectangle.Min.X;

        /// <summary>The extent along Y.</summary>
        public float Height => rectangle.Max.Y - rectangle.Min.Y;

        /// <summary>The extents along both axes.</summary>
        public Vector2 Size => rectangle.Max - rectangle.Min;

        /// <summary>Returns a typed size when both extents are finite and strictly positive.</summary>
        public bool TryGetPositiveSize(out Size2D size)
        {
            var width = rectangle.Max.X - rectangle.Min.X;
            var height = rectangle.Max.Y - rectangle.Min.Y;
            if (!float.IsFinite(width) || width <= 0f || !float.IsFinite(height) || height <= 0f)
            {
                size = default;
                return false;
            }
            size = new Size2D(width, height);
            return true;
        }

        /// <summary>Half the extents along both axes.</summary>
        public Vector2 HalfSize => (rectangle.Max - rectangle.Min) / 2f;

        /// <summary>Width times height.</summary>
        public float Area => Area2D.Rectangle(rectangle.Min, rectangle.Max);

        /// <summary>The midpoint of the rectangle; undefined for infinite bounds.</summary>
        public Vector2 Center => (rectangle.Min + rectangle.Max) / 2f;

        /// <summary>The midpoint along X.</summary>
        public float MidX => (rectangle.Min.X + rectangle.Max.X) / 2f;

        /// <summary>The midpoint along Y.</summary>
        public float MidY => (rectangle.Min.Y + rectangle.Max.Y) / 2f;

        /// <summary>The (Min.X, Max.Y) corner.</summary>
        public Vector2 TopLeft => new(rectangle.Min.X, rectangle.Max.Y);

        /// <summary>The midpoint of the top edge.</summary>
        public Vector2 TopCenter => new(rectangle.MidX, rectangle.Max.Y);

        /// <summary>The Max corner.</summary>
        public Vector2 TopRight => rectangle.Max;

        /// <summary>The midpoint of the left edge.</summary>
        public Vector2 CenterLeft => new(rectangle.Min.X, rectangle.MidY);

        /// <summary>The midpoint of the right edge.</summary>
        public Vector2 CenterRight => new(rectangle.Max.X, rectangle.MidY);

        /// <summary>The Min corner.</summary>
        public Vector2 BottomLeft => rectangle.Min;

        /// <summary>The midpoint of the bottom edge.</summary>
        public Vector2 BottomCenter => new(rectangle.MidX, rectangle.Min.Y);

        /// <summary>The (Max.X, Min.Y) corner.</summary>
        public Vector2 BottomRight => new(rectangle.Max.X, rectangle.Min.Y);

        /// <summary>True when every coordinate is finite.</summary>
        public bool IsFinite => NumericValidation.IsFinite(rectangle.Min) && NumericValidation.IsFinite(rectangle.Max);

        /// <summary>Tests whether a point lies inside, including all four edges.</summary>
        /// <param name="point">The point to test.</param>
        /// <returns>True when the point is in or on the rectangle.</returns>
        public bool Contains(Vector2 point) => Containment2D.Rectangle(point, rectangle.Min, rectangle.Max);

        /// <summary>Tests whether another rectangle lies entirely inside this one, shared edges included.</summary>
        /// <param name="other">The rectangle to test.</param>
        /// <returns>True when the whole other rectangle is contained.</returns>
        public bool Contains<TOther>(TOther other) where TOther : IRect2D => Containment2D.RectangleInRectangle(rectangle.Min, rectangle.Max, other.Min, other.Max);

        /// <summary>Tests overlap with another rectangle; touching edges and corners count.</summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns>True when the rectangles share at least a point.</returns>
        public bool Intersects<TOther>(TOther other) where TOther : IRect2D => Intersection2D.RectanglesOverlap(rectangle.Min, rectangle.Max, other.Min, other.Max);

        /// <summary>Computes the shared rectangle, including zero-area edge and corner contacts.</summary>
        /// <param name="other">The other rectangle.</param>
        /// <param name="result">The intersection, or default when the rectangles are disjoint.</param>
        /// <returns>True when the rectangles intersect.</returns>
        public bool TryIntersect<TOther>(TOther other, out Rect2D result) where TOther : IRect2D
        {
            if (!rectangle.Intersects(other))
            {
                result = default;
                return false;
            }
            result = new(Vector2.Max(rectangle.Min, other.Min), Vector2.Min(rectangle.Max, other.Max));
            return true;
        }

        /// <summary>The smallest rectangle containing both operands.</summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns>The bounding box of both rectangles.</returns>
        public Rect2D Union<TOther>(TOther other) where TOther : IRect2D => new(Vector2.Min(rectangle.Min, other.Min), Vector2.Max(rectangle.Max, other.Max));

        /// <summary>The closest point in or on this rectangle; interior points are returned unchanged.</summary>
        /// <param name="point">The query point.</param>
        /// <returns>The point clamped to the rectangle.</returns>
        public Vector2 ClosestPoint(Vector2 point) => ClosestPoint2D.OnRectangle(point, rectangle.Min, rectangle.Max);

        /// <summary>The distance from a finite point to the filled rectangle; zero inside.</summary>
        /// <param name="point">The query point.</param>
        /// <returns>The nonnegative distance.</returns>
        public float DistanceTo(Vector2 point) => Distance2D.DistanceToRectangle(point, rectangle.Min, rectangle.Max);

        /// <summary>The squared distance from a finite point to the filled rectangle; zero inside.</summary>
        /// <param name="point">The query point.</param>
        /// <returns>The nonnegative squared distance.</returns>
        public float DistanceSquaredTo(Vector2 point) => Distance2D.DistanceSquaredToRectangle(point, rectangle.Min, rectangle.Max);

        /// <summary>The signed distance from a finite point: positive outside, zero on the boundary, negative inside.</summary>
        /// <param name="point">The query point.</param>
        /// <returns>The signed distance to the boundary.</returns>
        public float SignedDistanceTo(Vector2 point) => Distance2D.SignedDistanceToRectangle(point, rectangle.Min, rectangle.Max);

        /// <summary>The distance between two filled rectangles; zero when touching or overlapping.</summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns>The nonnegative gap.</returns>
        public float DistanceTo<TOther>(TOther other) where TOther : IRect2D => Distance2D.Distance(rectangle.ToRect(), other.ToRect());

        /// <summary>The signed distance between two finite rectangles: the gap, or the negated minimum escape translation when overlapping.</summary>
        /// <param name="other">The other rectangle.</param>
        /// <returns>The signed distance.</returns>
        public float SignedDistanceTo<TOther>(TOther other) where TOther : IRect2D => Distance2D.SignedDistance(rectangle.ToRect(), other.ToRect());

        /// <summary>Moves the rectangle by a finite offset.</summary>
        /// <param name="offset">The translation to apply.</param>
        /// <returns>The translated rectangle.</returns>
        public Rect2D TranslatedBy(Vector2 offset)
        {
            ArgGuard.ThrowIfNotFinite(offset);
            return new(rectangle.Min + offset, rectangle.Max + offset);
        }

        /// <summary>Expands both sides of each axis by nonnegative finite amounts.</summary>
        /// <param name="x">The amount added to the left and right.</param>
        /// <param name="y">The amount added to the bottom and top.</param>
        /// <returns>The inflated rectangle.</returns>
        public Rect2D InflatedBy(float x, float y)
        {
            ArgGuard.ThrowIfNotFiniteOrNegative(x);
            ArgGuard.ThrowIfNotFiniteOrNegative(y);
            var amount = new Vector2(x, y);
            return new(rectangle.Min - amount, rectangle.Max + amount);
        }

        /// <summary>Shrinks both sides of each axis by nonnegative finite amounts. Oversized insets collapse that axis to its midpoint.</summary>
        /// <param name="x">The amount removed from the left and right.</param>
        /// <param name="y">The amount removed from the bottom and top.</param>
        /// <returns>The inset rectangle.</returns>
        public Rect2D InsetBy(float x, float y)
        {
            ArgGuard.ThrowIfNotFiniteOrNegative(x);
            ArgGuard.ThrowIfNotFiniteOrNegative(y);
            var amount = Vector2.Min(new(x, y), rectangle.HalfSize);
            return new(rectangle.Min + amount, rectangle.Max - amount);
        }

        /// <summary>Copies Min and Max into a <see cref="Rect2D"/> value.</summary>
        /// <returns>An equivalent rectangle value.</returns>
        public Rect2D ToRect() => new(rectangle.Min, rectangle.Max);

        /// <summary>
        /// Encloses this rectangle after a matrix transform. Rotation or shear transforms all four corners;
        /// otherwise the two corners are scaled and translated directly. This bounds the box, not the shape
        /// inside it. Non-finite rectangles stay <see cref="Rect2D.Unbounded"/> so broad-phase candidates survive.
        /// </summary>
        /// <param name="transform">The local-to-world matrix to apply.</param>
        /// <returns>The axis-aligned box around the transformed corners.</returns>
        public Rect2D TransformedBy(Matrix3x2 transform)
        {
            if (!rectangle.IsFinite) return Rect2D.Unbounded;
            if (transform.M12 == 0f && transform.M21 == 0f)
            {
                var scale = new Vector2(transform.M11, transform.M22);
                var first = rectangle.Min * scale + transform.Translation;
                var second = rectangle.Max * scale + transform.Translation;
                return new(Vector2.Min(first, second), Vector2.Max(first, second));
            }
            Span<Vector2> corners = stackalloc Vector2[4];
            rectangle.WriteCorners(corners);
            for (var i = 0; i < corners.Length; i++) corners[i] = Vector2.Transform(corners[i], transform);
            return Rect2D.FromPoints(corners);
        }

        /// <summary>Writes the four finite corners counter-clockwise from Min, leaving extra entries untouched.</summary>
        /// <param name="corners">A buffer of at least four entries.</param>
        public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, rectangle.Min, rectangle.Max);
    }
}
