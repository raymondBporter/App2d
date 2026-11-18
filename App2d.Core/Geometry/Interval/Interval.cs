using App2d.Core.Validation;
using System.Globalization;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Interval;

/// <summary>
/// A nonempty closed [Min, Max] range on an axis. Endpoints are ordered and never NaN.
/// Negative/positive infinity represent an unbounded lower/upper end; default is [0, 0].
/// </summary>
/// <typeparam name="T">The numeric type used for the endpoints.</typeparam>
public readonly record struct Interval<T> : IInterval<T> where T : INumber<T>
{
    /// <summary>Creates a range from ordered endpoints, allowing outward infinities.</summary>
    [JsonConstructor]
    public Interval(T min, T max)
    {
        Validate(min, max);
        Min = min;
        Max = max;
    }

    /// <summary>The lower endpoint, or negative infinity when unbounded below.</summary>
    public T Min { get; }

    /// <summary>The upper endpoint, or positive infinity when unbounded above.</summary>
    public T Max { get; }

    /// <summary>Validates endpoints using the same rules as the constructor.</summary>
    public static void Validate(T min, T max)
    {
        IInterval<T>.Validate(min, max);
    }

    /// <summary>Validates this interval using the shared endpoint contract.</summary>
    public void Validate()
    {
        Validate(Min, Max);
    }

    /// <summary>Whether the interval has a finite lower endpoint.</summary>
    public bool IsBoundedBelow => IInterval<T>.IsBoundedBelow(this);

    /// <summary>Whether the interval has a finite upper endpoint.</summary>
    public bool IsBoundedAbove => IInterval<T>.IsBoundedAbove(this);

    /// <summary>Whether the interval is unbounded in both directions.</summary>
    public bool IsEntire => IInterval<T>.IsEntire(this);

    /// <summary>Whether the interval contains just one value.</summary>
    public bool IsSingleton => IInterval<T>.IsSingleton(this);

    /// <summary>Creates the interval containing one finite value.</summary>
    public static Interval<T> Singleton(T value)
    {
        return new(value, value);
    }

    /// <summary>Deconstructs the range into its lower and upper endpoints.</summary>
    public void Deconstruct(out T min, out T max)
    {
        (min, max) = (Min, Max);
    }

    /// <summary>The extent of the interval; zero for a single value.</summary>
    public T Length => IInterval<T>.Length(this);

    /// <summary>Clamps a value to the range, following the shared interval arithmetic.</summary>
    public T Clamp(T value)
    {
        return IInterval<T>.Clamp(this, value);
    }

    /// <summary>Tests whether a value lies inside the interval, including its ends.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when Min &lt;= value &lt;= Max.</returns>
    public bool Contains(T value)
    {
        return IInterval<T>.Contains(this, value);
    }

    /// <summary>Tests whether two intervals share at least one value, including touching ends.</summary>
    /// <param name="other">The other interval.</param>
    /// <returns>True when the intervals overlap or touch.</returns>
    public bool Overlaps(Interval<T> other)
    {
        return IInterval<T>.Overlaps(this, other);
    }

    /// <summary>The nearest-point distance, or zero when overlapping or touching.</summary>
    public T Distance(Interval<T> other)
    {
        return IInterval<T>.Distance(this, other);
    }

    /// <summary>The greatest distance between a point in each interval.</summary>
    public T MaxDistance(Interval<T> other)
    {
        return IInterval<T>.MaxDistance(this, other);
    }

    /// <summary>The gap, or negative minimum translation to touching contact when overlapping.</summary>
    public T SignedDistance(Interval<T> other)
    {
        return IInterval<T>.SignedDistance(this, other);
    }

    /// <summary>Evaluates a finite interval at t, allowing extrapolation.</summary>
    public T Lerp(T t)
    {
        return IInterval<T>.Lerp(this, t);
    }

    /// <summary>Evaluates a finite interval after clamping t to [0, 1].</summary>
    public T LerpClamped(T t)
    {
        return IInterval<T>.LerpClamped(this, t);
    }

    /// <summary>Recovers t for a finite interval; zero for a singleton. Integer division truncates.</summary>
    public T InverseLerp(T value)
    {
        return IInterval<T>.InverseLerp(this, value);
    }

    /// <summary>Recovers t and clamps it to [0, 1]; zero for a singleton.</summary>
    public T InverseLerpClamped(T value)
    {
        return IInterval<T>.InverseLerpClamped(this, value);
    }

    /// <summary>Moves both endpoints by a finite offset.</summary>
    public Interval<T> Translate(T offset)
    {
        return IInterval<T>.Translate(this, offset);
    }

    /// <summary>Reflects around zero, producing [-Max, -Min].</summary>
    public Interval<T> Reflect()
    {
        return IInterval<T>.Reflect(this);
    }

    /// <summary>Reflects around a finite center.</summary>
    public Interval<T> Reflect(T center)
    {
        return IInterval<T>.Reflect(this, center);
    }

    /// <summary>Scales around zero, reordering negative scales and collapsing zero scale.</summary>
    public Interval<T> Scale(T scale)
    {
        return IInterval<T>.Scale(this, scale);
    }

    /// <summary>Scales around a finite center, reordering negative scales and collapsing zero scale.</summary>
    public Interval<T> Scale(T scale, T center)
    {
        return IInterval<T>.Scale(this, scale, center);
    }

    /// <summary>The smallest interval containing this interval and a non-NaN value.</summary>
    public Interval<T> Include(T value)
    {
        return IInterval<T>.Include(this, value);
    }

    /// <summary>Formats the closed interval as [Min, Max] using invariant numeric formatting.</summary>
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"[{Min}, {Max}]");
    }
}

/// <summary>
/// Interval factories and extension queries whose arguments supply the scalar type for inference.
/// Generic receivers avoid boxing; endpoint-only or arbitrary mixed-interval queries use IInterval&lt;T&gt;.
/// Unbounded factories require an endpoint type that supports infinity.
/// </summary>
public static class Interval
{
    /// <summary>The interval unbounded in both directions.</summary>
    public static Interval<T> Entire<T>() where T : IFloatingPointIeee754<T>
    {
        return new(T.NegativeInfinity, T.PositiveInfinity);
    }

    /// <summary>The interval from a finite lower endpoint to positive infinity.</summary>
    public static Interval<T> AtLeast<T>(T min) where T : IFloatingPointIeee754<T>
    {
        return new(Ensure.Finite(min), T.PositiveInfinity);
    }

    /// <summary>The interval from negative infinity to a finite upper endpoint.</summary>
    public static Interval<T> AtMost<T>(T max) where T : IFloatingPointIeee754<T>
    {
        return new(T.NegativeInfinity, Ensure.Finite(max));
    }

    /// <summary>Tests containment, including the endpoints. The value supplies the scalar type.</summary>
    public static bool Contains<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Contains(interval, value);
    }

    /// <summary>Clamps a value to the interval. The value supplies the scalar type.</summary>
    public static T Clamp<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Clamp(interval, value);
    }

    /// <summary>Tests overlap with a concrete interval, including touching endpoints.</summary>
    public static bool Overlaps<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Overlaps(interval, other);
    }

    /// <summary>Tests whether the whole other interval lies inside this one.</summary>
    public static bool ContainsInterval<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.ContainsInterval(interval, other);
    }

    /// <summary>Finds the shared interval; touching endpoints count as an intersection.</summary>
    public static bool TryIntersect<TInterval, T>(this TInterval interval, Interval<T> other, out Interval<T> intersection)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.TryIntersect(interval, other, out intersection);
    }

    /// <summary>The smallest interval containing this interval and the other one.</summary>
    public static Interval<T> Hull<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Hull(interval, other);
    }

    /// <summary>The nearest-point distance, or zero when overlapping or touching.</summary>
    public static T Distance<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Distance(interval, other);
    }

    /// <summary>The greatest distance between a point in each interval.</summary>
    public static T MaxDistance<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.MaxDistance(interval, other);
    }

    /// <summary>The gap, or negative minimum translation to touching contact when overlapping.</summary>
    public static T SignedDistance<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.SignedDistance(interval, other);
    }

    /// <summary>Evaluates a finite interval at t, allowing extrapolation.</summary>
    public static T Lerp<TInterval, T>(this TInterval interval, T t)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Lerp(interval, t);
    }

    /// <summary>Evaluates a finite interval after clamping t to [0, 1].</summary>
    public static T LerpClamped<TInterval, T>(this TInterval interval, T t)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.LerpClamped(interval, t);
    }

    /// <summary>Recovers t for a finite interval; zero for a singleton. Integer division truncates.</summary>
    public static T InverseLerp<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.InverseLerp(interval, value);
    }

    /// <summary>Recovers t and clamps it to [0, 1]; zero for a singleton.</summary>
    public static T InverseLerpClamped<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.InverseLerpClamped(interval, value);
    }

    /// <summary>Moves both endpoints by a finite offset.</summary>
    public static Interval<T> Translate<TInterval, T>(this TInterval interval, T offset)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Translate(interval, offset);
    }

    /// <summary>Reflects around a finite center; use IInterval&lt;T&gt;.Reflect(interval) for zero without a scalar argument.</summary>
    public static Interval<T> Reflect<TInterval, T>(this TInterval interval, T center)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Reflect(interval, center);
    }

    /// <summary>Scales around zero, reordering negative scales and collapsing zero scale.</summary>
    public static Interval<T> Scale<TInterval, T>(this TInterval interval, T scale)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Scale(interval, scale);
    }

    /// <summary>Scales around a finite center, reordering negative scales and collapsing zero scale.</summary>
    public static Interval<T> Scale<TInterval, T>(this TInterval interval, T scale, T center)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Scale(interval, scale, center);
    }

    /// <summary>The smallest interval containing the interval and a non-NaN value.</summary>
    public static Interval<T> Include<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T>
    {
        return IInterval<T>.Include(interval, value);
    }
}
