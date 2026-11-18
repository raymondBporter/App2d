using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Interval;

/// <summary>
/// A nonempty closed [Min, Max] scalar range. Endpoints are ordered and never NaN.
/// Negative/positive infinity may represent an unbounded lower/upper end.
/// Implementations retain their own storage, validation and any additional domain-specific data.
/// Supplying Min and Max is enough to use all shared queries directly or through the Interval extensions.
/// Static queries receive concrete interval types so querying a struct does not require boxing it.
/// </summary>
/// <typeparam name="T">The numeric type of the endpoints.</typeparam>
public interface IInterval<T> where T : INumber<T>
{
    /// <summary>The smallest value in the range.</summary>
    T Min { get; }

    /// <summary>The largest value in the range.</summary>
    T Max { get; }

    public static bool IsValidMin (T value) => !T.IsNaN(value) && !T.IsPositiveInfinity(value);
    public static bool IsValidMax(T value) => !T.IsNaN(value) && !T.IsNegativeInfinity(value);

    public static bool IsValid(T min, T max)
    {
        return IsValidMin(min) &&
               IsValidMax(max) &&
               NumericValidation.IsLessThanOrEqual(min, max);
    }

    /// <summary>Requires ordered, non-NaN endpoints, allowing only outward infinities.</summary>
    public static void Validate(T min, T max)
    {
        Ensure.Number(min);
        Ensure.Number(max);
        Ensure.NotPositiveInfinity(min);
        Ensure.NotNegativeInfinity(max);
        Ensure.LessThanOrEqual(min, max, allowInfinity: true);
    }

    /// <summary>Validates any implementation against the shared endpoint contract.</summary>
    public static void Validate<TInterval>(TInterval interval)
        where TInterval : IInterval<T>
    {
        Validate(interval.Min, interval.Max);
    }

    /// <summary>Whether the interval has a finite lower endpoint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBoundedBelow<TInterval>(TInterval interval)
        where TInterval : IInterval<T>
    {
        return T.IsFinite(interval.Min);
    }

    /// <summary>Whether the interval has a finite upper endpoint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBoundedAbove<TInterval>(TInterval interval)
        where TInterval : IInterval<T>
    {
        return T.IsFinite(interval.Max);
    }

    /// <summary>Whether the interval is unbounded in both directions.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEntire<TInterval>(TInterval interval)
        where TInterval : IInterval<T>
    {
        return !IsBoundedBelow(interval) && !IsBoundedAbove(interval);
    }

    /// <summary>Whether the interval contains just one value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSingleton<TInterval>(TInterval interval)
        where TInterval : IInterval<T>
    {
        return interval.Min == interval.Max;
    }

    /// <summary>The endpoint difference; zero for a single value. Arithmetic uses T's overflow rules.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Length<TInterval>(TInterval interval)
        where TInterval : IInterval<T>
    {
        return interval.Min == interval.Max ? T.Zero : interval.Max - interval.Min;
    }

    /// <summary>Tests containment of a value, including both endpoints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Contains<TInterval>(TInterval interval, T value)
        where TInterval : IInterval<T>
    {
        return value >= interval.Min && value <= interval.Max;
    }

    /// <summary>Clamps a value to the closed interval, using T's standard clamping behavior.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Clamp<TInterval>(TInterval interval, T value)
        where TInterval : IInterval<T>
    {
        return T.Clamp(value, interval.Min, interval.Max);
    }

    /// <summary>Tests whether the entire inner interval lies in the outer interval.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ContainsInterval<TOuter, TInner>(TOuter outer, TInner inner)
        where TOuter : IInterval<T>
        where TInner : IInterval<T>
    {
        return inner.Min >= outer.Min && inner.Max <= outer.Max;
    }

    /// <summary>Tests overlap, including a single shared endpoint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Overlaps<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T>
    {
        return first.Min <= second.Max && second.Min <= first.Max;
    }

    /// <summary>
    /// Finds the shared interval. Touching endpoints produce a zero-length intersection.
    /// Returns false and sets intersection to default when disjoint; default itself is the valid range [0, 0].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryIntersect<TFirst, TSecond>(TFirst first, TSecond second, out Interval<T> intersection)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T>
    {
        var min = T.Max(first.Min, second.Min);
        var max = T.Min(first.Max, second.Max);
        if (min > max)
        {
            intersection = default;
            return false;
        }
        intersection = new(min, max);
        return true;
    }

    /// <summary>The smallest interval containing both inputs, including any gap between them.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Hull<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T>
    {
        return new(T.Min(first.Min, second.Min), T.Max(first.Max, second.Max));
    }

    /// <summary>The nearest-point distance; zero for overlapping or touching intervals. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Distance<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T>
    {
        if (first.Max < second.Min) return checked(second.Min - first.Max);
        if (second.Max < first.Min) return checked(first.Min - second.Max);
        return T.Zero;
    }

    /// <summary>The greatest distance between a point in each interval. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T MaxDistance<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T>
    {
        var forward = first.Min <= second.Max ? checked(second.Max - first.Min) : checked(first.Min - second.Max);
        var backward = second.Min <= first.Max ? checked(first.Max - second.Min) : checked(second.Min - first.Max);
        return T.Max(forward, backward);
    }

    /// <summary>
    /// Positive for a gap, zero at contact, or negative by the smallest translation to touching contact.
    /// Containment uses the distance to escape, not the intersection's length. Uses checked T arithmetic;
    /// unsigned types cannot represent a negative penetration distance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T SignedDistance<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T>
    {
        if (first.Max <= second.Min) return checked(second.Min - first.Max);
        if (second.Max <= first.Min) return checked(first.Min - second.Max);
        var depth = T.Min(checked(first.Max - second.Min), checked(second.Max - first.Min));
        return checked(T.Zero - depth);
    }

    /// <summary>
    /// Evaluates a finite interval at t, including extrapolation. Exact t=0/1 returns the endpoints;
    /// a singleton always returns its endpoint. Arithmetic, including integer arithmetic, uses T.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Lerp<TInterval>(TInterval interval, T t) where TInterval : IInterval<T>
    {
        var min = Ensure.Finite(interval.Min);
        var max = Ensure.Finite(interval.Max);
        if (t == T.Zero || min == max) return min;
        if (t == T.One) return max;
        // Opposite signs can overflow max-min even when the interpolated value is finite.
        return min < T.Zero && max > T.Zero
            ? checked((T.One - t) * min + t * max)
            : checked(min + t * (max - min));
    }

    /// <summary>Evaluates a finite interval after clamping t to [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T LerpClamped<TInterval>(TInterval interval, T t) where TInterval : IInterval<T>
    {
        return Lerp(interval, T.Clamp(t, T.Zero, T.One));
    }

    /// <summary>
    /// Recovers t from a value in a finite interval, allowing extrapolation; returns zero for a singleton.
    /// Division uses T, so integer parameters truncate. Checked intermediate arithmetic may overflow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InverseLerp<TInterval>(TInterval interval, T value) where TInterval : IInterval<T>
    {
        var min = Ensure.Finite(interval.Min);
        var max = Ensure.Finite(interval.Max);
        if (min == max || value == min) return T.Zero;
        if (value == max) return T.One;
        var width = checked(max - min);
        var offset = checked(value - min);
        if (T.IsFinite(width) && T.IsFinite(offset)) return offset / width;
        // Halving finite endpoints avoids infinity/infinity when a floating-point difference overflows.
        var two = T.One + T.One;
        return (value / two - min / two) / (max / two - min / two);
    }

    /// <summary>Recovers t for a finite interval and clamps it to [0, 1]; returns zero for a singleton.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InverseLerpClamped<TInterval>(TInterval interval, T value) where TInterval : IInterval<T>
    {
        return InverseLerp(interval, T.Clamp(value, interval.Min, interval.Max));
    }

    /// <summary>Moves both endpoints by a finite offset. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Translate<TInterval>(TInterval interval, T offset) where TInterval : IInterval<T>
    {
        Ensure.Finite(offset);
        return new(checked(interval.Min + offset), checked(interval.Max + offset));
    }

    /// <summary>Reflects around zero, producing [-Max, -Min]. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Reflect<TInterval>(TInterval interval) where TInterval : IInterval<T>
    {
        return new(checked(-interval.Max), checked(-interval.Min));
    }

    /// <summary>Reflects around a finite center. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Reflect<TInterval>(TInterval interval, T center) where TInterval : IInterval<T>
    {
        Ensure.Finite(center);
        return new(ReflectValue(interval.Max, center), ReflectValue(interval.Min, center));
    }

    /// <summary>Scales around zero, reordering negative scales. Zero collapses even an unbounded interval to [0, 0].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Scale<TInterval>(TInterval interval, T scale) where TInterval : IInterval<T>
    {
        return Scale(interval, scale, T.Zero);
    }

    /// <summary>
    /// Maps each endpoint v to center + scale*(v-center), with finite scale and center.
    /// Reorders negative scales; zero produces the singleton at center. Uses checked T arithmetic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Scale<TInterval>(TInterval interval, T scale, T center) where TInterval : IInterval<T>
    {
        Ensure.Finite(scale);
        Ensure.Finite(center);
        if (scale == T.Zero) return Interval<T>.Singleton(center);
        if (scale == T.One) return new(interval.Min, interval.Max);
        var first = ScaleValue(interval.Min, scale, center);
        var second = ScaleValue(interval.Max, scale, center);
        return scale < T.Zero ? new(second, first) : new(first, second);
    }

    /// <summary>The smallest interval containing the input interval and a non-NaN value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> Include<TInterval>(TInterval interval, T value) where TInterval : IInterval<T>
    {
        Ensure.Number(value);
        return new(T.Min(interval.Min, value), T.Max(interval.Max, value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ReflectValue(T value, T center)
    {
        return value >= center
        ? checked(center - (value - center))
        : checked(center + (center - value));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T ScaleValue(T value, T scale, T center)
    {
        if ((value < T.Zero && center > T.Zero) || (value > T.Zero && center < T.Zero))
            return checked(scale * value + (T.One - scale) * center);
        return value >= center
            ? checked(center + scale * (value - center))
            : checked(center - scale * (center - value));
    }
}
