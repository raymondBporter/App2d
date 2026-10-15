namespace App2d.Core.Geometry.Primitives;

/// <summary>A directed pair of endpoints that can create another segment of the same concrete type.</summary>
public interface ISegment<TSelf, TPoint> where TSelf : ISegment<TSelf, TPoint>
{
    TPoint Start { get; }
    TPoint End { get; }

    /// <summary>Creates a segment using the implementation's own validation.</summary>
    static abstract TSelf Create(TPoint start, TPoint end);
}
