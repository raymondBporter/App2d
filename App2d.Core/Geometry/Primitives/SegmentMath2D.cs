using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>Shared operations on concrete segments with Vector2 endpoints.</summary>
public static class SegmentMath2D
{
    extension<TSegment>(TSegment segment) where TSegment : ISegment<TSegment, Vector2>
    {
        /// <summary>The midpoint of any segment with Vector2 endpoints; no point-interface or wrapper is needed.</summary>
        public Vector2 Midpoint => ((segment.Start.ToVector2d() + segment.End.ToVector2d()) * .5).ToVector2();
    }

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
