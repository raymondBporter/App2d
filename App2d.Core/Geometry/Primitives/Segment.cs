namespace App2d.Core.Geometry.Primitives;

/// <summary>A lightweight directed endpoint pair. Validity belongs to the owning geometry.</summary>
public readonly record struct Segment<TPoint>(TPoint Start, TPoint End) : ISegment<Segment<TPoint>, TPoint>
{
    public static Segment<TPoint> Create(TPoint start, TPoint end) => new(start, end);
}
