using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Ordered bounds using a concrete floating-point vector and its coordinate type. Axes allow outward infinities.
/// Every formula is a member of this struct; nothing forwards to a helper. Another type that has Min and Max
/// uses them through <see cref="From"/>.
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

    public TVector Min { get; }
    public TVector Max { get; }

    // ---- Construction and validation ----

    /// <summary>The rectangle covering the whole plane.</summary>
    public static Rect2<TVector, TReal> Unbounded => new(TVector.Create(TReal.NegativeInfinity, TReal.NegativeInfinity), TVector.Create(TReal.PositiveInfinity, TReal.PositiveInfinity));

    /// <summary>Copies and validates the corners of any type that has Min and Max. A struct is not boxed.</summary>
    public static Rect2<TVector, TReal> From<TOther>(TOther other) where TOther : IRect2<TVector, TReal> => new(other.Min, other.Max);

    public static Rect2<TVector, TReal> FromPoints(TVector first, TVector second) => new(Lower(first, second), Upper(first, second));

    public static Rect2<TVector, TReal> FromSize(TVector size, TVector center = default)
    {
        Ensure.NonNegative(size.X);
        Ensure.NonNegative(size.Y);
        Ensure.Finite(center.X);
        Ensure.Finite(center.Y);
        var halfSize = Real<TReal>.Half * size;
        return new(center - halfSize, center + halfSize);
    }

    public static bool IsValid(TVector min, TVector max) => Interval<TReal>.IsValid(min.X, max.X) && Interval<TReal>.IsValid(min.Y, max.Y);

    public static void Validate(TVector min, TVector max)
    {
        Interval<TReal>.Validate(min.X, max.X);
        Interval<TReal>.Validate(min.Y, max.Y);
    }

    public static void Deconstruct(Rect2<TVector, TReal> rectangle, out TVector min, out TVector max) => rectangle.Deconstruct(out min, out max);

    public void Deconstruct(out TVector min, out TVector max) => (min, max) = (Min, Max);

    // ---- Edges, anchors and sizes ----

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
    public TReal CenterX => Midpoint(Min.X, Max.X);
    public TReal CenterY => Midpoint(Min.Y, Max.Y);
    public TVector Center => TVector.Create(CenterX, CenterY);
    public TReal Width => Max.X - Min.X;
    public TReal Height => Max.Y - Min.Y;
    public TReal MaxSideLength => TReal.Max(Width, Height);
    public TReal MinSideLength => TReal.Min(Width, Height);
    public TVector HalfSize => TVector.Create(HalfExtent(Min.X, Max.X), HalfExtent(Min.Y, Max.Y));
    public TVector Size => Max - Min;
    public TReal Area => Width * Height;
    public TReal DiagonalLength => TReal.Hypot(Width, Height);
    public Interval<TReal> ProjectionX => new(Min.X, Max.X);
    public Interval<TReal> ProjectionY => new(Min.Y, Max.Y);
    public bool IsFinite => TVector.IsFinite(Min) && TVector.IsFinite(Max);

    public TReal DistanceToOriginX => TReal.Abs(TReal.Clamp(TReal.Zero, Min.X, Max.X));
    public TReal DistanceToOriginY => TReal.Abs(TReal.Clamp(TReal.Zero, Min.Y, Max.Y));
    public TReal DistanceToOrigin => TReal.Hypot(DistanceToOriginX, DistanceToOriginY);
    public TReal MaxDistanceToOriginX => TReal.Max(TReal.Abs(Min.X), TReal.Abs(Max.X));
    public TReal MaxDistanceToOriginY => TReal.Max(TReal.Abs(Min.Y), TReal.Abs(Max.Y));
    public TReal MaxDistanceToOrigin => TReal.Hypot(MaxDistanceToOriginX, MaxDistanceToOriginY);

    // ---- Points against the rectangle ----

    /// <summary>Tests whether a point lies inside, including all four edges.</summary>
    public bool Contains(TVector point) => point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;

    /// <summary>Tests the open interior; points on any edge or corner are excluded.</summary>
    public bool ContainsInterior(TVector point) => point.X > Min.X && point.X < Max.X && point.Y > Min.Y && point.Y < Max.Y;

    /// <summary>The closest point in or on this rectangle; interior points are returned unchanged.</summary>
    public TVector ClosestPoint(TVector point) => TVector.Create(TReal.Clamp(point.X, Min.X, Max.X), TReal.Clamp(point.Y, Min.Y, Max.Y));

    /// <summary>The squared distance to the filled rectangle; zero inside.</summary>
    public TReal DistanceSquaredTo(TVector point) => TVector.DistanceSquared(point, ClosestPoint(point));

    /// <summary>The distance to the filled rectangle; zero inside. Hypot keeps large finite offsets from overflowing.</summary>
    public TReal DistanceTo(TVector point)
    {
        var offset = point - ClosestPoint(point);
        return TReal.Hypot(offset.X, offset.Y);
    }

    // ---- Two rectangles ----

    /// <summary>Tests whether another rectangle lies entirely inside this one, shared edges included.</summary>
    public bool Contains(Rect2<TVector, TReal> other) => Contains(other.Min) && Contains(other.Max);

    /// <summary>Tests overlap with another rectangle; touching edges and corners count.</summary>
    public bool Intersects(Rect2<TVector, TReal> other) => Min.X <= other.Max.X && other.Min.X <= Max.X && Min.Y <= other.Max.Y && other.Min.Y <= Max.Y;

    /// <summary>Computes the shared rectangle, including zero-area edge and corner contacts; default when disjoint.</summary>
    public bool TryIntersect(Rect2<TVector, TReal> other, out Rect2<TVector, TReal> result)
    {
        if (!Intersects(other))
        {
            result = default;
            return false;
        }
        result = new(Upper(Min, other.Min), Lower(Max, other.Max));
        return true;
    }

    /// <summary>The smallest rectangle containing both operands.</summary>
    public Rect2<TVector, TReal> Union(Rect2<TVector, TReal> other) => new(Lower(Min, other.Min), Upper(Max, other.Max));

    public Rect2<TVector, TReal> TranslatedBy(TVector offset) => new(Min + offset, Max + offset);

    // Per-coordinate TReal.Min/Max, so NaN propagates into the constructor's validation for every vector type.
    private static TVector Lower(TVector a, TVector b) => TVector.Create(TReal.Min(a.X, b.X), TReal.Min(a.Y, b.Y));
    private static TVector Upper(TVector a, TVector b) => TVector.Create(TReal.Max(a.X, b.X), TReal.Max(a.Y, b.Y));

    // Halving each endpoint first keeps the midpoint of two large finite values finite.
    private static TReal Midpoint(TReal first, TReal second)
    {
        var two = Real<TReal>.Two;
        var sum = first + second;
        return TReal.IsInfinity(sum) && TReal.IsFinite(first) && TReal.IsFinite(second)
            ? first / two + second / two : sum / two;
    }

    private static TReal HalfExtent(TReal min, TReal max)
    {
        var two = Real<TReal>.Two;
        var extent = max - min;
        return TReal.IsInfinity(extent) && TReal.IsFinite(min) && TReal.IsFinite(max)
            ? max / two - min / two : extent / two;
    }
}
