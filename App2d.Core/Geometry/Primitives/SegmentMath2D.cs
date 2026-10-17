using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>Shared operations on concrete segments with Vector2 endpoints.</summary>
public static class SegmentMath2D
{
    public static float Length<TSegment>(this TSegment segment)
        where TSegment : ISegment<TSegment, Vector2> => Vector2.Distance(segment.Start, segment.End);

    /// <summary>Samples the segment, clamping the amount to [0, 1].</summary>
    public static Vector2 PointAt<TSegment>(this TSegment segment, float amount)
        where TSegment : ISegment<TSegment, Vector2> =>
        Vector2.Lerp(segment.Start, segment.End, Clamping.Clamp01(amount));

    /// <summary>Swaps endpoints through the concrete type's static factory.</summary>
    public static TSegment Reversed<TSegment>(this TSegment segment)
        where TSegment : ISegment<TSegment, Vector2> => TSegment.Create(segment.End, segment.Start);

    public static TSegment TransformedBy<TSegment>(this TSegment segment, Matrix3x2 transform)
        where TSegment : ISegment<TSegment, Vector2> => TSegment.Create(
        Vector2.Transform(segment.Start, transform), Vector2.Transform(segment.End, transform));
}
