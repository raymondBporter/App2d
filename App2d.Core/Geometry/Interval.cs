using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>A closed [Min, Max] range on an axis.</summary>
/// <typeparam name="T">The numeric type used for the endpoints.</typeparam>
/// <param name="Min">The smallest value.</param>
/// <param name="Max">The largest value.</param>
public readonly record struct Interval<T>(T Min, T Max) where T : INumber<T>
{
    /// <summary>The extent of the interval; zero for a single value.</summary>
    public T Length => Max - Min;

    /// <summary>Tests whether a value lies inside the interval, including its ends.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when Min &lt;= value &lt;= Max.</returns>
    public bool Contains(T value) => value >= Min && value <= Max;

    /// <summary>Tests whether two intervals share at least one value, including touching ends.</summary>
    /// <param name="other">The other interval.</param>
    /// <returns>True when the intervals overlap or touch.</returns>
    public bool Overlaps(Interval<T> other) => Min <= other.Max && other.Min <= Max;
}
