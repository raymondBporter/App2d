using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Ordered axis-aligned bounds in the coordinate type's own precision. Min and Max are never NaN;
/// infinities are allowed when supported by TReal. Generic receivers keep concrete rectangles unboxed.
/// </summary>
public interface IRect2<TVector, TReal>
    where TVector : unmanaged, IVector2<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    TVector Min { get; }
    TVector Max { get; }
}
