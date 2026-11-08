using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>Operations for 2D segments using System.Numerics types.</summary>
public static class SegmentMath2D
{
    extension<TSegment>(TSegment segment) where TSegment : ISegment<TSegment, Vector2>
    {
        /// <summary>The midpoint of a segment with Vector2 endpoints.</summary>
        public Vector2 Midpoint => AsVectorSegment(segment).Midpoint.ToVector2();
    }

    public static float Length<TSegment>(this TSegment segment)
        where TSegment : ISegment<TSegment, Vector2> => AsVectorSegment(segment).Length();

    /// <summary>Samples the segment, clamping the amount to [0, 1].</summary>
    public static Vector2 PointAt<TSegment>(this TSegment segment, float amount)
        where TSegment : ISegment<TSegment, Vector2> => AsVectorSegment(segment).PointAt(amount).ToVector2();

    /// <summary>Swaps endpoints through the concrete type's static factory.</summary>
    public static TSegment Reversed<TSegment>(this TSegment segment)
        where TSegment : ISegment<TSegment, Vector2> => TSegment.Create(segment.End, segment.Start);

    public static TSegment TransformedBy<TSegment>(this TSegment segment, Matrix3x2 transform)
        where TSegment : ISegment<TSegment, Vector2>
    {
        var (start, end) = AsVectorSegment(segment).TransformedBy(transform);
        return TSegment.Create(start.ToVector2(), end.ToVector2());
    }

    public static Segment<Vector2f, float> TransformedBy(this Segment<Vector2f, float> segment, Matrix3x2 transform) =>
        new(Vector2f.Transform(segment.Start, transform), Vector2f.Transform(segment.End, transform));

    private static Segment<Vector2f, float> AsVectorSegment<TSegment>(TSegment segment)
        where TSegment : ISegment<TSegment, Vector2> => new(new(segment.Start), new(segment.End));
}
