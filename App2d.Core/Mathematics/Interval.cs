using App2d.Core.Validation;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>
/// A nonempty closed [Min, Max] range on an axis. Endpoints are ordered and never NaN.
/// Negative/positive infinity represent an unbounded lower/upper end; default is [0, 0].
/// Ordered range queries live here; parameterization shares the directed endpoint helpers on Interval.
/// </summary>
/// <typeparam name="T">The numeric type used for the endpoints.</typeparam>
public readonly record struct Interval<T> : IInterval<T> where T : INumber<T>
{
    /// <summary>Stores endpoints for deserialization. Use Builder.Create for validated finite endpoints.</summary>
    [JsonConstructor]
    public Interval(T min, T max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The lower endpoint, or negative infinity when unbounded below.</summary>
    public T Min { get; }

    /// <summary>The upper endpoint, or positive infinity when unbounded above.</summary>
    public T Max { get; }

    // ---- Construction and validation ----

    public static class Builder
    {
        public static Interval<T> Create(T min, T max)
        {
            Validate(min, max);
            return new(min, max);
        }

        /// <summary>Allows a negative-infinite minimum or a positive-infinite maximum.</summary>
        public static Interval<T> CreateUnbounded(T min, T max)
        {
            Validate(min, max, allowUnbounded: true);
            return new(min, max);
        }
    }

    /// <summary>Creates the interval containing one finite value.</summary>
    public static Interval<T> Singleton(T value) => Builder.Create(value, value);

    /// <summary>Copies and validates the endpoints of any type that has Min and Max. A struct is not boxed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Interval<T> From<TOther>(TOther other, bool allowUnbounded = false) where TOther : IInterval<T> =>
        allowUnbounded ? Builder.CreateUnbounded(other.Min, other.Max) : Builder.Create(other.Min, other.Max);

    /// <summary>Checks endpoint order and finiteness. Unbounded ranges allow only outward infinities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValid(T min, T max, bool allowUnbounded = false) =>
        (allowUnbounded ? !T.IsPositiveInfinity(min) && !T.IsNegativeInfinity(max) : T.IsFinite(min) && T.IsFinite(max))
        && min <= max;

    public static void Validate(T min, T max, bool allowUnbounded = false)
    {
        if (!IsValid(min, max, allowUnbounded)) ThrowInvalidEndpoints(min, max, allowUnbounded);
    }

    // Keep detailed exceptions out of the machine code for ordinary interval arithmetic.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidEndpoints(T min, T max, bool allowUnbounded)
    {
        Ensure.LessThanOrEqual(min, max, allowInfinity: allowUnbounded);
        if (allowUnbounded)
        {
            Ensure.NotPositiveInfinity(min);
            Ensure.NotNegativeInfinity(max);
        }
    }

    public void Validate(bool allowUnbounded = false) => Validate(Min, Max, allowUnbounded);

    /// <summary>Deconstructs the range into its lower and upper endpoints.</summary>
    public void Deconstruct(out T min, out T max) => (min, max) = (Min, Max);

    // ---- Shape of the interval ----

    /// <summary>Whether the interval has a finite lower endpoint.</summary>
    public bool IsBoundedBelow { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => T.IsFinite(Min); }

    /// <summary>Whether the interval has a finite upper endpoint.</summary>
    public bool IsBoundedAbove { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => T.IsFinite(Max); }

    /// <summary>Whether the interval is unbounded in both directions.</summary>
    public bool IsEntire { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => !IsBoundedBelow && !IsBoundedAbove; }

    /// <summary>Whether the interval contains just one value.</summary>
    public bool IsSingleton { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Min == Max; }

    /// <summary>The endpoint difference; zero for a single value. Arithmetic uses T's overflow rules.</summary>
    public T Length { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => Min == Max ? T.Zero : Max - Min; }

    // ---- Values against the interval ----

    /// <summary>Tests whether a value lies inside the interval, including both endpoints.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T value) => value >= Min && value <= Max;

    /// <summary>Clamps a value to the closed interval, using T's standard clamping behavior (NaN stays NaN).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Clamp(T value) => T.Clamp(value, Min, Max);

    /// <summary>The smallest interval containing this interval and a non-NaN value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Include(T value)
    {
        Ensure.Number(value);
        return Builder.CreateUnbounded(T.Min(Min, value), T.Max(Max, value));
    }

    // ---- Two intervals ----

    /// <summary>Tests whether the entire other interval lies inside this one.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsInterval(Interval<T> other) => other.Min >= Min && other.Max <= Max;

    /// <summary>Tests whether two intervals share at least one value, including a single shared endpoint.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Overlaps(Interval<T> other) => Min <= other.Max && other.Min <= Max;

    /// <summary>
    /// Finds the shared interval. Touching endpoints produce a zero-length intersection.
    /// Returns false and sets intersection to default when disjoint; default itself is the valid range [0, 0].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryIntersect(Interval<T> other, out Interval<T> intersection)
    {
        var min = T.Max(Min, other.Min);
        var max = T.Min(Max, other.Max);
        if (min > max)
        {
            intersection = default;
            return false;
        }
        intersection = Builder.CreateUnbounded(min, max);
        return true;
    }

    /// <summary>The smallest interval containing both inputs, including any gap between them.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Hull(Interval<T> other) => Builder.CreateUnbounded(T.Min(Min, other.Min), T.Max(Max, other.Max));

    /// <summary>The nearest-point distance; zero for overlapping or touching intervals. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Distance(Interval<T> other)
    {
        if (Max < other.Min) return checked(other.Min - Max);
        if (other.Max < Min) return checked(Min - other.Max);
        return T.Zero;
    }

    /// <summary>The greatest distance between a point in each interval. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T MaxDistance(Interval<T> other)
    {
        var forward = Min <= other.Max ? checked(other.Max - Min) : checked(Min - other.Max);
        var backward = other.Min <= Max ? checked(Max - other.Min) : checked(other.Min - Max);
        return T.Max(forward, backward);
    }

    /// <summary>
    /// Positive for a gap, zero at contact, or negative by the smallest translation to touching contact.
    /// Containment uses the distance to escape, not the intersection's length. Uses checked T arithmetic;
    /// unsigned types cannot represent a negative penetration distance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T SignedDistance(Interval<T> other)
    {
        if (Max <= other.Min) return checked(other.Min - Max);
        if (other.Max <= Min) return checked(Min - other.Max);
        var depth = T.Min(checked(Max - other.Min), checked(other.Max - Min));
        return checked(T.Zero - depth);
    }

    // ---- Parameterization ----

    /// <summary>
    /// Evaluates a finite interval at t, including extrapolation. Exact t=0/1 returns the endpoints;
    /// a singleton always returns its endpoint. Arithmetic, including integer arithmetic, uses T.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Lerp(T t)
    {
        return Interval.Lerp(Ensure.Finite(Min), Ensure.Finite(Max), t);
    }

    /// <summary>Evaluates a finite interval after clamping t to [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T LerpClamped(T t) => Lerp(T.Clamp(t, T.Zero, T.One));

    /// <summary>
    /// Recovers t from a value in a finite interval, allowing extrapolation; returns zero for a singleton.
    /// Division uses T, so integer parameters truncate. Checked intermediate arithmetic may overflow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T InverseLerp(T value)
    {
        return Interval.InverseLerp(Ensure.Finite(Min), Ensure.Finite(Max), value);
    }

    /// <summary>Recovers t for a finite interval and clamps it to [0, 1]; returns zero for a singleton.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T InverseLerpClamped(T value) => InverseLerp(T.Clamp(value, Min, Max));

    // ---- Transforms ----

    /// <summary>Moves both endpoints by a finite offset. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Translate(T offset)
    {
        Ensure.Finite(offset);
        return Builder.CreateUnbounded(checked(Min + offset), checked(Max + offset));
    }

    /// <summary>Reflects around zero, producing [-Max, -Min]. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Reflect() => Builder.CreateUnbounded(checked(-Max), checked(-Min));

    /// <summary>Reflects around a finite center. Uses checked T arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Reflect(T center)
    {
        Ensure.Finite(center);
        return Builder.CreateUnbounded(ReflectValue(Max, center), ReflectValue(Min, center));
    }

    /// <summary>Scales around zero, reordering negative scales. Zero collapses even an unbounded interval to [0, 0].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Scale(T scale) => Scale(scale, T.Zero);

    /// <summary>
    /// Maps each endpoint v to center + scale*(v-center), with finite scale and center.
    /// Reorders negative scales; zero produces the singleton at center. Uses checked T arithmetic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Interval<T> Scale(T scale, T center)
    {
        Ensure.Finite(scale);
        Ensure.Finite(center);
        if (scale == T.Zero) return Singleton(center);
        if (scale == T.One) return this;
        var first = ScaleValue(Min, scale, center);
        var second = ScaleValue(Max, scale, center);
        return scale < T.Zero ? Builder.CreateUnbounded(second, first) : Builder.CreateUnbounded(first, second);
    }

    /// <summary>Formats the closed interval as [Min, Max] using invariant numeric formatting.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"[{Min}, {Max}]");

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

/// <summary>Interval factories and directed endpoint arithmetic without constructing an interval value.</summary>
public static partial class Interval
{
    /// <summary>The unit interval [0, 1]: the domain of a normalized curve.</summary>
    public static Interval<T> UnitInterval<T>() where T : IFloatingPointIeee754<T> =>
#pragma warning disable APP2D001
        new(T.Zero, T.One);

    /// <summary>The interval unbounded in both directions.</summary>
    public static Interval<T> Entire<T>() where T : IFloatingPointIeee754<T> => Interval<T>.Builder.CreateUnbounded(T.NegativeInfinity, T.PositiveInfinity);

    /// <summary>The interval from a finite lower endpoint to positive infinity.</summary>
    public static Interval<T> AtLeast<T>(T min) where T : IFloatingPointIeee754<T> => Interval<T>.Builder.CreateUnbounded(Ensure.Finite(min), T.PositiveInfinity);

    /// <summary>The interval from negative infinity to a finite upper endpoint.</summary>
    public static Interval<T> AtMost<T>(T max) where T : IFloatingPointIeee754<T> => Interval<T>.Builder.CreateUnbounded(T.NegativeInfinity, Ensure.Finite(max));
}
