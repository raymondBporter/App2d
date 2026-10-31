using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Primitives;

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
        ArgGuard.Number(min);
        ArgGuard.Number(max);
        if (T.IsPositiveInfinity(min))
            ArgGuard.ThrowOutOfRange(min, "The minimum must be finite or negative infinity.");
        if (T.IsNegativeInfinity(max))
            ArgGuard.ThrowOutOfRange(max, "The maximum must be finite or positive infinity.");
        ArgGuard.ThrowIf(min > max, "The minimum cannot exceed the maximum.", nameof(max));
        Min = min;
        Max = max;
    }

    /// <summary>The lower endpoint, or negative infinity when unbounded below.</summary>
    public T Min { get; }

    /// <summary>The upper endpoint, or positive infinity when unbounded above.</summary>
    public T Max { get; }

    /// <summary>Whether the interval has a finite lower endpoint.</summary>
    public bool IsBoundedBelow => T.IsFinite(Min);

    /// <summary>Whether the interval has a finite upper endpoint.</summary>
    public bool IsBoundedAbove => T.IsFinite(Max);

    /// <summary>Whether the interval is unbounded in both directions.</summary>
    public bool IsEntire => !IsBoundedBelow && !IsBoundedAbove;

    /// <summary>Whether the interval contains just one value.</summary>
    public bool IsSingleton => Min == Max;

    /// <summary>Creates the interval containing one finite value.</summary>
    public static Interval<T> Singleton(T value) => new(value, value);

    /// <summary>Deconstructs the range into its lower and upper endpoints.</summary>
    public void Deconstruct(out T min, out T max) => (min, max) = (Min, Max);

    /// <summary>The extent of the interval; zero for a single value.</summary>
    public T Length => IntervalMath<T>.Length(this);

    /// <summary>Clamps a value to the range, following the shared interval arithmetic.</summary>
    public T Clamp(T value) => IntervalMath<T>.Clamp(this, value);

    /// <summary>Tests whether a value lies inside the interval, including its ends.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when Min &lt;= value &lt;= Max.</returns>
    public bool Contains(T value) => IntervalMath<T>.Contains(this, value);

    /// <summary>Tests whether two intervals share at least one value, including touching ends.</summary>
    /// <param name="other">The other interval.</param>
    /// <returns>True when the intervals overlap or touch.</returns>
    public bool Overlaps(Interval<T> other) => IntervalMath<T>.Overlaps(this, other);
}

/// <summary>Factories for intervals whose endpoint type supports infinity.</summary>
public static class Interval
{
    /// <summary>The interval unbounded in both directions.</summary>
    public static Interval<T> Entire<T>() where T : IFloatingPointIeee754<T> =>
        new(T.NegativeInfinity, T.PositiveInfinity);

    /// <summary>The interval from a finite lower endpoint to positive infinity.</summary>
    public static Interval<T> AtLeast<T>(T min) where T : IFloatingPointIeee754<T> =>
        new(ArgGuard.Finite(min), T.PositiveInfinity);

    /// <summary>The interval from negative infinity to a finite upper endpoint.</summary>
    public static Interval<T> AtMost<T>(T max) where T : IFloatingPointIeee754<T> =>
        new(T.NegativeInfinity, ArgGuard.Finite(max));
}
