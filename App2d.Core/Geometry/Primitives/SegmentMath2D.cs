using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>Operations for 2D segments with single-precision coordinates.</summary>
public static class SegmentMath2D
{
    extension<TSegment>(TSegment segment) where TSegment : ISegment<TSegment, Vector2f>
    {
        /// <summary>The midpoint of the segment.</summary>
        public Vector2f Midpoint => AsVectorSegment(segment).Midpoint;
    }

    public static float Length<TSegment>(this TSegment segment)
        where TSegment : ISegment<TSegment, Vector2f> => AsVectorSegment(segment).Length();

    /// <summary>Samples the segment, clamping the amount to [0, 1].</summary>
    public static Vector2f  PointAt<TSegment>(this TSegment segment, float amount)
        where TSegment : ISegment<TSegment, Vector2f> => AsVectorSegment(segment).PointAt(amount);

    /// <summary>Swaps endpoints through the concrete type's static factory.</summary>
    public static TSegment Reversed<TSegment>(this TSegment segment)
        where TSegment : ISegment<TSegment, Vector2f> => TSegment.Create(segment.End, segment.Start);

    public static TSegment TransformedBy<TSegment>(this TSegment segment, Matrix3x2 transform)
        where TSegment : ISegment<TSegment, Vector2f>
    {
        var (start, end) = AsVectorSegment(segment).TransformedBy(transform);
        return TSegment.Create(start, end);
    }

    public static Segment<Vector2f, float> TransformedBy(this Segment<Vector2f, float> segment, Matrix3x2 transform) =>
        new(Vector2f.Transform(segment.Start, transform), Vector2f.Transform(segment.End, transform));

    private static Segment<Vector2f, float> AsVectorSegment<TSegment>(TSegment segment)
        where TSegment : ISegment<TSegment, Vector2f> => new(segment.Start, segment.End);
}
