using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// Shared closed-range operations. Generic interval parameters keep structs unboxed.
/// The scalar type is selected explicitly because C# cannot infer it from an interface constraint.
/// Inputs follow the endpoint contract of <see cref="IInterval{T}"/>; arithmetic uses T's overflow rules.
/// </summary>
/// <typeparam name="T">The numeric type of the endpoints.</typeparam>
public static class IntervalMath<T> where T : INumber<T>
{
    /// <summary>The endpoint difference; zero for a single value.</summary>
    public static T Length<TInterval>(TInterval interval) where TInterval : IInterval<T> =>
        interval.Max - interval.Min;

    /// <summary>Tests containment of a value, including both endpoints.</summary>
    public static bool Contains<TInterval>(TInterval interval, T value) where TInterval : IInterval<T> =>
        value >= interval.Min && value <= interval.Max;

    /// <summary>Tests whether the entire inner interval lies in the outer interval.</summary>
    public static bool ContainsInterval<TOuter, TInner>(TOuter outer, TInner inner)
        where TOuter : IInterval<T>
        where TInner : IInterval<T> =>
        inner.Min >= outer.Min && inner.Max <= outer.Max;

    /// <summary>Tests overlap, including a single shared endpoint.</summary>
    public static bool Overlaps<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T> =>
        first.Min <= second.Max && second.Min <= first.Max;

    /// <summary>Clamps a value to the closed interval.</summary>
    public static T Clamp<TInterval>(TInterval interval, T value) where TInterval : IInterval<T> =>
        T.Clamp(value, interval.Min, interval.Max);

    /// <summary>
    /// Finds the shared interval. Touching endpoints produce a zero-length intersection.
    /// Returns false and sets intersection to default when disjoint; default itself is the valid range [0, 0].
    /// </summary>
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
    public static Interval<T> Hull<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : IInterval<T>
        where TSecond : IInterval<T> =>
        new(T.Min(first.Min, second.Min), T.Max(first.Max, second.Max));
}
