using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Numeric rectangle queries. TSelf parameters retain the caller's concrete bounds type;
/// TVector and TReal are explicit because C# does not infer them from constraints.
/// </summary>
public static class Rect2Math<TVector, TReal>
    where TVector : unmanaged, IVector2<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public static TReal Width<TSelf>(TSelf rectangle)
        where TSelf : IRect2<TVector, TReal>
    {
        return rectangle.Max.X - rectangle.Min.X;
    }

    public static TReal Height<TSelf>(TSelf rectangle)
        where TSelf : IRect2<TVector, TReal>
    {
        return rectangle.Max.Y - rectangle.Min.Y;
    }

    public static TReal Area<TSelf>(TSelf rectangle)
        where TSelf : IRect2<TVector, TReal>
    {
        return Width(rectangle) * Height(rectangle);
    }

    public static TVector Size<TSelf>(TSelf rectangle)
        where TSelf : IRect2<TVector, TReal>
    {
        return rectangle.Max - rectangle.Min;
    }

    public static TVector Center<TSelf>(TSelf rectangle)
        where TSelf : IRect2<TVector, TReal>
    {
        return TVector.Create(Midpoint(rectangle.Min.X, rectangle.Max.X), Midpoint(rectangle.Min.Y, rectangle.Max.Y));
    }

    public static bool IsFinite<TSelf>(TSelf rectangle)
        where TSelf : IRect2<TVector, TReal>
    {
        return rectangle.Min.IsFinite() && rectangle.Max.IsFinite();
    }

    public static bool Contains<TSelf>(TSelf rectangle, TVector point) where TSelf : IRect2<TVector, TReal>
    {
        return point.X >= rectangle.Min.X && point.X <= rectangle.Max.X && point.Y >= rectangle.Min.Y && point.Y <= rectangle.Max.Y;
    }

    public static bool ContainsInterior<TSelf>(TSelf rectangle, TVector point) where TSelf : IRect2<TVector, TReal> =>
        point.X > rectangle.Min.X && point.X < rectangle.Max.X && point.Y > rectangle.Min.Y && point.Y < rectangle.Max.Y;

    public static bool ContainsRectangle<TSelf, TOther>(TSelf rectangle, TOther other)
        where TSelf : IRect2<TVector, TReal>
        where TOther : IRect2<TVector, TReal> => Contains(rectangle, other.Min) && Contains(rectangle, other.Max);

    public static bool Intersects<TSelf, TOther>(TSelf rectangle, TOther other)
        where TSelf : IRect2<TVector, TReal>
        where TOther : IRect2<TVector, TReal> =>
        rectangle.Min.X <= other.Max.X && other.Min.X <= rectangle.Max.X &&
        rectangle.Min.Y <= other.Max.Y && other.Min.Y <= rectangle.Max.Y;

    public static bool TryIntersect<TSelf, TOther>(TSelf rectangle, TOther other, out Rect2<TVector, TReal> result)
        where TSelf : IRect2<TVector, TReal>
        where TOther : IRect2<TVector, TReal>
    {
        if (!Intersects(rectangle, other))
        {
            result = default;
            return false;
        }
        result = new(
            TVector.Create(TReal.Max(rectangle.Min.X, other.Min.X), TReal.Max(rectangle.Min.Y, other.Min.Y)),
            TVector.Create(TReal.Min(rectangle.Max.X, other.Max.X), TReal.Min(rectangle.Max.Y, other.Max.Y)));
        return true;
    }

    public static Rect2<TVector, TReal> Union<TSelf, TOther>(TSelf rectangle, TOther other)
        where TSelf : IRect2<TVector, TReal>
        where TOther : IRect2<TVector, TReal> => new(
            TVector.Create(TReal.Min(rectangle.Min.X, other.Min.X), TReal.Min(rectangle.Min.Y, other.Min.Y)),
            TVector.Create(TReal.Max(rectangle.Max.X, other.Max.X), TReal.Max(rectangle.Max.Y, other.Max.Y)));

    public static TVector ClosestPoint<TSelf>(TSelf rectangle, TVector point) where TSelf : IRect2<TVector, TReal> =>
        TVector.Create(TReal.Clamp(point.X, rectangle.Min.X, rectangle.Max.X), TReal.Clamp(point.Y, rectangle.Min.Y, rectangle.Max.Y));

    public static TReal DistanceSquaredTo<TSelf>(TSelf rectangle, TVector point) where TSelf : IRect2<TVector, TReal>
    {
        var offset = point - ClosestPoint(rectangle, point);
        return offset.Dot(offset);
    }

    internal static TReal Midpoint(TReal first, TReal second)
    {
        var two = TReal.One + TReal.One;
        var sum = first + second;
        return TReal.IsInfinity(sum) && TReal.IsFinite(first) && TReal.IsFinite(second)
            ? first / two + second / two : sum / two;
    }
}
