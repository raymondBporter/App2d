namespace App2d.Core.Geometry.Primitives;

/// <summary>Three ordered vertices and the concrete type's construction policy.</summary>
public interface ITriangle<TSelf, TPoint> where TSelf : ITriangle<TSelf, TPoint>
{
    TPoint A { get; }
    TPoint B { get; }
    TPoint C { get; }
    static abstract TSelf Create(TPoint a, TPoint b, TPoint c);
}
