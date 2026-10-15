using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Convenience queries whose arguments supply the scalar type for inference.
/// Generic receivers avoid boxing; endpoint-only or arbitrary mixed-interval queries use IntervalMath&lt;T&gt;.
/// </summary>
public static class IntervalExtensions
{
    /// <summary>Tests containment, including the endpoints. The value supplies the scalar type.</summary>
    public static bool Contains<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T> => IntervalMath<T>.Contains(interval, value);

    /// <summary>Clamps a value to the interval. The value supplies the scalar type.</summary>
    public static T Clamp<TInterval, T>(this TInterval interval, T value)
        where TInterval : IInterval<T>
        where T : INumber<T> => IntervalMath<T>.Clamp(interval, value);

    /// <summary>Tests overlap with a concrete interval, including touching endpoints.</summary>
    public static bool Overlaps<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T> => IntervalMath<T>.Overlaps(interval, other);

    /// <summary>Tests whether the whole other interval lies inside this one.</summary>
    public static bool ContainsInterval<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T> => IntervalMath<T>.ContainsInterval(interval, other);

    /// <summary>Finds the shared interval; touching endpoints count as an intersection.</summary>
    public static bool TryIntersect<TInterval, T>(this TInterval interval, Interval<T> other, out Interval<T> intersection)
        where TInterval : IInterval<T>
        where T : INumber<T> => IntervalMath<T>.TryIntersect(interval, other, out intersection);

    /// <summary>The smallest interval containing this interval and the other one.</summary>
    public static Interval<T> Hull<TInterval, T>(this TInterval interval, Interval<T> other)
        where TInterval : IInterval<T>
        where T : INumber<T> => IntervalMath<T>.Hull(interval, other);
}
