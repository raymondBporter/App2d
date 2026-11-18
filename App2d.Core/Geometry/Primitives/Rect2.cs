using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Ordered bounds using a concrete floating-point vector and its coordinate type.
/// </summary>
public readonly record struct Rect2<TVector, TReal> : IRect2<TVector, TReal>
    where TVector : unmanaged, IVector2<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    [JsonConstructor]
    public Rect2(TVector min, TVector max)
    {
        Ensure.LessThanOrEqual(min.X, max.X, allowInfinity: true);
        Ensure.LessThanOrEqual(min.Y, max.Y, allowInfinity: true);
        Min = min;
        Max = max;
    }

    private static readonly TReal Two = TReal.One + TReal.One;
    private static readonly TReal OneHalf = TReal.One / Two;

    public TVector Min { get; }
    public TVector Max { get; }
    public TReal Left => Min.X;
    public TReal Right => Max.X;
    public TReal Bottom => Min.Y;
    public TReal Top => Max.Y;
    public TReal Width => Rect2Math<TVector, TReal>.Width(this);
    public TReal Height => Rect2Math<TVector, TReal>.Height(this);
    public TReal Area => Rect2Math<TVector, TReal>.Area(this);
    public TVector Size => Rect2Math<TVector, TReal>.Size(this);
    public TVector Center => Rect2Math<TVector, TReal>.Center(this);
    public TReal MidX => Rect2Math<TVector, TReal>.Midpoint(Min.X, Max.X);
    public TReal MidY => Rect2Math<TVector, TReal>.Midpoint(Min.Y, Max.Y);
    public TVector HalfSize => TVector.Create(OneHalf * Width, OneHalf * Height);
    public Interval<TReal> XInterval => new(Min.X, Max.X);
    public Interval<TReal> YInterval => new(Min.Y, Max.Y);
    public TVector TopLeft => TVector.Create(Min.X, Max.Y);
    public TVector TopCenter => TVector.Create(MidX, Max.Y);
    public TVector TopRight => Max;
    public TVector CenterLeft => TVector.Create(Min.X, MidY);
    public TVector CenterRight => TVector.Create(Max.X, MidY);
    public TVector BottomLeft => Min;
    public TVector BottomCenter => TVector.Create(MidX, Min.Y);
    public TVector BottomRight => TVector.Create(Max.X, Min.Y);
    public bool IsFinite => Rect2Math<TVector, TReal>.IsFinite(this);

    public bool Contains(TVector point)
    {
        return Rect2Math<TVector, TReal>.Contains(this, point);
    }

    public bool ContainsInterior(TVector point)
    {
        return Rect2Math<TVector, TReal>.ContainsInterior(this, point);
    }

    public bool Contains<TOther>(TOther other)
        where TOther : IRect2<TVector, TReal>
    {
        return Rect2Math<TVector, TReal>.ContainsRectangle(this, other);
    }

    public bool Intersects<TOther>(TOther other)
        where TOther : IRect2<TVector, TReal>
    {
        return Rect2Math<TVector, TReal>.Intersects(this, other);
    }

    public bool TryIntersect<TOther>(TOther other, out Rect2<TVector, TReal> result)
        where TOther : IRect2<TVector, TReal>
    {
        return Rect2Math<TVector, TReal>.TryIntersect(this, other, out result);

    }

    public Rect2<TVector, TReal> Union<TOther>(TOther other)
        where TOther : IRect2<TVector, TReal>
    {
        return Rect2Math<TVector, TReal>.Union(this, other);
    }

    public TVector ClosestPoint(TVector point)
    {
        return Rect2Math<TVector, TReal>.ClosestPoint(this, point);
    }

    public TReal DistanceSquaredTo(TVector point)
    {
        return Rect2Math<TVector, TReal>.DistanceSquaredTo(this, point);
    }

    public Rect2<TVector, TReal> TranslatedBy(TVector offset)
    {
        return new(TVector.Create(Min.X + offset.X, Min.Y + offset.Y), TVector.Create(Max.X + offset.X, Max.Y + offset.Y));
    }

    public static Rect2<TVector, TReal> FromPoints(TVector first, TVector second)
    {
        return new(TVector.Create(TReal.Min(first.X, second.X), TReal.Min(first.Y, second.Y)), TVector.Create(TReal.Max(first.X, second.X), TReal.Max(first.Y, second.Y)));
    }

    public static Rect2<TVector, TReal> FromSize(TVector size, TVector center = default)
    {
        Ensure.NonNegative(size.X);
        Ensure.NonNegative(size.Y);
        Ensure.Finite(center.X);
        Ensure.Finite(center.Y);
        var x = OneHalf * size.X;
        var y = OneHalf * size.Y;
        return new(TVector.Create(center.X - x, center.Y - y), TVector.Create(center.X + x, center.Y + y));
    }
}

/// <summary>Rectangle operations that specifically need floating-point coordinates.</summary>
public static class Rect2
{
    public static Rect2<TVector, TReal> Unbounded<TVector, TReal>()
        where TVector : unmanaged, IVector2<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        return new(TVector.Create(TReal.NegativeInfinity, TReal.NegativeInfinity), TVector.Create(TReal.PositiveInfinity, TReal.PositiveInfinity));
    }

    extension<TVector, TReal>(Rect2<TVector, TReal> rectangle)
        where TVector : unmanaged, IVector2<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        public TReal DistanceTo(TVector point)
        {
            var closest = rectangle.ClosestPoint(point);
            return TReal.Hypot(point.X - closest.X, point.Y - closest.Y);
        }
    }
}
