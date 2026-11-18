using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A two-dimensional floating-point vector with named coordinates and concrete arithmetic.</summary>
public interface IVector2<TSelf, TReal> :
    IVector<TSelf, TReal>, 
    INumberTuple2<TSelf, TReal>
    where TSelf : unmanaged, IVector2<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    TReal Cross(TSelf other);
    TReal Orientation(TSelf second, TSelf third);
    TReal AngleRadians { get; }
    TSelf PerpCcw { get; }
    TSelf PerpCw { get; }
    TSelf Rotate(TReal radians);
    TSelf Rotate(TReal radians, TSelf pivot);
    Polar<TReal> ToPolar();
    Vector2 ToVector2();
    Vector2f ToVector2f();
    Vector2d ToVector2d();
}
