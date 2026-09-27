using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Shared rectangle properties and queries for any IRect2D. Generic receivers keep value-type
/// rectangles unboxed. All coordinates belong to the rectangle's own space; Top means Max.Y.
/// </summary>
public static class Rect2DExtensions
{
    extension<T>(T rectangle) where T : IRect2D
    {
        public float Left => rectangle.Min.X;
        public float Right => rectangle.Max.X;
        public float Bottom => rectangle.Min.Y;
        public float Top => rectangle.Max.Y;
        public float Width => rectangle.Max.X - rectangle.Min.X;
        public float Height => rectangle.Max.Y - rectangle.Min.Y;
        public Vector2 Size => rectangle.Max - rectangle.Min;
        public Vector2 HalfSize => (rectangle.Max - rectangle.Min) / 2f;
        public float Area => PrimitiveGeometry2D.RectangleArea(rectangle.Min, rectangle.Max);
        public Vector2 Center => (rectangle.Min + rectangle.Max) / 2f;
        public float MidX => (rectangle.Min.X + rectangle.Max.X) / 2f;
        public float MidY => (rectangle.Min.Y + rectangle.Max.Y) / 2f;

        public Vector2 TopLeft => new(rectangle.Min.X, rectangle.Max.Y);
        public Vector2 TopCenter => new(rectangle.MidX, rectangle.Max.Y);
        public Vector2 TopRight => rectangle.Max;
        public Vector2 CenterLeft => new(rectangle.Min.X, rectangle.MidY);
        public Vector2 CenterRight => new(rectangle.Max.X, rectangle.MidY);
        public Vector2 BottomLeft => rectangle.Min;
        public Vector2 BottomCenter => new(rectangle.MidX, rectangle.Min.Y);
        public Vector2 BottomRight => new(rectangle.Max.X, rectangle.Min.Y);

        public bool IsFinite =>
            float.IsFinite(rectangle.Min.X) && float.IsFinite(rectangle.Min.Y) &&
            float.IsFinite(rectangle.Max.X) && float.IsFinite(rectangle.Max.Y);

        /// <summary>Includes all four edges.</summary>
        public bool Contains(Vector2 point) =>
            PrimitiveGeometry2D.RectangleContainsPoint(point, rectangle.Min, rectangle.Max);

        /// <summary>True when the entire other rectangle is contained, including shared edges.</summary>
        public bool Contains<TOther>(TOther other) where TOther : IRect2D =>
            PrimitiveGeometry2D.RectangleContainsRectangle(rectangle.Min, rectangle.Max, other.Min, other.Max);

        /// <summary>Touching edges and corners count as intersection.</summary>
        public bool Intersects<TOther>(TOther other) where TOther : IRect2D =>
            PrimitiveGeometry2D.RectanglesIntersect(rectangle.Min, rectangle.Max, other.Min, other.Max);

        /// <summary>Returns the shared rectangle, including zero-area edge/corner contacts. On false, result is default.</summary>
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
        public Rect2D Union<TOther>(TOther other) where TOther : IRect2D =>
            new(Vector2.Min(rectangle.Min, other.Min), Vector2.Max(rectangle.Max, other.Max));

        /// <summary>The closest point in or on this rectangle. Interior points are returned unchanged.</summary>
        public Vector2 ClosestPoint(Vector2 point) => Vector2.Clamp(point, rectangle.Min, rectangle.Max);

        /// <summary>Euclidean distance to the filled rectangle (zero inside); coordinates must be finite.</summary>
        public float DistanceTo(Vector2 point) => Distance2D.DistanceToRectangle(point, rectangle.Min, rectangle.Max);

        /// <summary>Positive outside, zero on the boundary, negative inside; coordinates must be finite.</summary>
        public float SignedDistanceTo(Vector2 point) => Distance2D.SignedDistanceToRectangle(point, rectangle.Min, rectangle.Max);

        public float DistanceTo<TOther>(TOther other) where TOther : IRect2D =>
            Distance2D.Distance(rectangle.ToRect(), other.ToRect());

        /// <summary>Negative minimum escape translation when overlapping; both rectangles must be finite.</summary>
        public float SignedDistanceTo<TOther>(TOther other) where TOther : IRect2D =>
            Distance2D.SignedDistance(rectangle.ToRect(), other.ToRect());

        public Rect2D TranslatedBy(Vector2 offset)
        {
            ArgGuard.ThrowIfNotFinite(offset);
            return new(rectangle.Min + offset, rectangle.Max + offset);
        }

        /// <summary>Expands both sides of each axis by nonnegative finite amounts.</summary>
        public Rect2D InflatedBy(float x, float y)
        {
            ArgGuard.ThrowIfNegativeOrNotFinite(x);
            ArgGuard.ThrowIfNegativeOrNotFinite(y);
            var amount = new Vector2(x, y);
            return new(rectangle.Min - amount, rectangle.Max + amount);
        }

        /// <summary>Shrinks both sides of each axis. Oversized insets collapse that axis to its midpoint.</summary>
        public Rect2D InsetBy(float x, float y)
        {
            ArgGuard.ThrowIfNegativeOrNotFinite(x);
            ArgGuard.ThrowIfNegativeOrNotFinite(y);
            var amount = Vector2.Min(new(x, y), rectangle.HalfSize);
            return new(rectangle.Min + amount, rectangle.Max - amount);
        }

        public Rect2D ToRect() => new(rectangle.Min, rectangle.Max);

        /// <summary>Writes four finite corners counter-clockwise from Min, leaving extra entries untouched.</summary>
        public void WriteCorners(Span<Vector2> corners) =>
            VertexGenerator2D.WriteRectangle(corners, rectangle.Min, rectangle.Max);
    }
}
