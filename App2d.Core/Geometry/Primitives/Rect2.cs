using App2d.Core.Geometry.Interval;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Ordered bounds using a concrete floating-point vector and its coordinate type. Axes allow outward infinities.
/// </summary>
public readonly record struct Rect2<TVector, TReal> : IRect2<TVector, TReal>
    where TVector : unmanaged, IVector2<TVector, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    [JsonConstructor]
    public Rect2(TVector min, TVector max)
    {
        Validate(min, max);
        Min = min;
        Max = max;
    }

    public static bool IsValid(TVector min, TVector max)
    {
        return IInterval<TReal>.IsValid(min.X, max.X) && IInterval<TReal>.IsValid(min.Y, max.Y);
    }

    public static void Validate(TVector min, TVector max)
    {
        IInterval<TReal>.Validate(min.X, max.X);
        IInterval<TReal>.Validate(min.Y, max.Y);
    }

    public static void Deconstruct(Rect2<TVector, TReal> rectangle, out TVector min, out TVector max)
    {
        rectangle.Deconstruct(out min, out max);
    }

    public void Deconstruct(out TVector min, out TVector max) => (min, max) = (Min, Max);

    private static readonly TReal Two = TReal.One + TReal.One;
    private static readonly TReal OneHalf = TReal.One / Two;

    public TVector Min { get; }
    public TVector Max { get; }
    public TReal Left => Min.X;
    public TReal Right => Max.X;
    public TReal Bottom => Min.Y;
    public TReal Top => Max.Y;
    public TVector TopLeft => TVector.Create(Min.X, Max.Y);
    public TVector TopCenter => TVector.Create(CenterX, Max.Y);
    public TVector TopRight => Max;
    public TVector CenterLeft => TVector.Create(Min.X, CenterY);
    public TVector CenterRight => TVector.Create(Max.X, CenterY);
    public TVector BottomLeft => Min;
    public TVector BottomCenter => TVector.Create(CenterX, Min.Y);
    public TVector BottomRight => TVector.Create(Max.X, Min.Y);
    public TReal CenterX => Rect2Math<TVector, TReal>.Midpoint(Min.X, Max.X);
    public TReal CenterY => Rect2Math<TVector, TReal>.Midpoint(Min.Y, Max.Y);
    public TVector Center => Rect2Math<TVector, TReal>.Center(this);
    public TReal Width => Rect2Math<TVector, TReal>.Width(this);
    public TReal Height => Rect2Math<TVector, TReal>.Height(this);
    public TReal MaxSideLength => TReal.Max(Width, Height);
    public TReal MinSideLength => TReal.Min(Width, Height);
    public TVector HalfSize => TVector.Create(
        Rect2Math<TVector, TReal>.HalfExtent(Min.X, Max.X),
        Rect2Math<TVector, TReal>.HalfExtent(Min.Y, Max.Y));
    public TVector Size => Rect2Math<TVector, TReal>.Size(this);
    public TReal Area => Rect2Math<TVector, TReal>.Area(this);
    public TReal DiagonalLength => TReal.Hypot(Width, Height);
    public Interval<TReal> ProjectionX => new(Min.X, Max.X);
    public Interval<TReal> ProjectionY => new(Min.Y, Max.Y);
    public bool IsFinite => Rect2Math<TVector, TReal>.IsFinite(this);

    public TReal DistanceToOriginX => TReal.Abs(TReal.Clamp(TReal.Zero, Min.X, Max.X));
    public TReal DistanceToOriginY => TReal.Abs(TReal.Clamp(TReal.Zero, Min.Y, Max.Y));
    public TReal DistanceToOrigin => TReal.Hypot(DistanceToOriginX, DistanceToOriginY);
    public TReal MaxDistanceToOriginX => TReal.Max(TReal.Abs(Min.X), TReal.Abs(Max.X));
    public TReal MaxDistanceToOriginY => TReal.Max(TReal.Abs(Min.Y), TReal.Abs(Max.Y));
    public TReal MaxDistanceToOrigin => TReal.Hypot(MaxDistanceToOriginX, MaxDistanceToOriginY);

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
        var halfSize = OneHalf * size;
        return new(center - halfSize, center + halfSize);
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
            return (point - rectangle.ClosestPoint(point)).Length;
        }
    }
}
