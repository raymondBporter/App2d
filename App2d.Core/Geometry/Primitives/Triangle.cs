namespace App2d.Core.Geometry.Primitives;

/// <summary>A lightweight vertex triple, including degenerate triangles. Validity belongs to the owning mesh.</summary>
public readonly record struct Triangle<TPoint>(TPoint A, TPoint B, TPoint C) : ITriangle<Triangle<TPoint>, TPoint>
{
    public static Triangle<TPoint> Create(TPoint a, TPoint b, TPoint c) => new(a, b, c);
}
