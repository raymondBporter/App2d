namespace App2d.Core.Geometry;

/// <summary>A closed [Min, Max] range on an axis, typically a shape projected by <see cref="Projection2D"/>.</summary>
/// <param name="Min">The smallest projected value.</param>
/// <param name="Max">The largest projected value.</param>
public readonly record struct Interval1D(float Min, float Max)
{
    /// <summary>The extent of the interval; zero for a single value.</summary>
    public float Length => Max - Min;

    /// <summary>Tests whether a value lies inside the interval, including its ends.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>True when Min &lt;= value &lt;= Max.</returns>
    public bool Contains(float value) => value >= Min && value <= Max;

    /// <summary>Tests whether two intervals share at least one value, including touching ends.</summary>
    /// <param name="other">The other interval.</param>
    /// <returns>True when the intervals overlap or touch.</returns>
    public bool Overlaps(Interval1D other) => Min <= other.Max && other.Min <= Max;
}
