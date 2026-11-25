using App2d.Core.Validation;

namespace App2d.Core.Mathematics;

/// <summary>Binary searches over ascending, indexable values. Inputs must already be ordered; duplicates are allowed.</summary>
public static class OrderedSearch
{
    /// <summary>The first index whose value is at least the query, or Count if there is none.</summary>
    public static int LowerBound<T>(IReadOnlyList<T> values, T query) where T : IComparable<T> =>
        LowerBound(values, query, static value => value);

    /// <summary>The first index whose value is greater than the query, or Count if there is none.</summary>
    public static int UpperBound<T>(IReadOnlyList<T> values, T query) where T : IComparable<T> =>
        UpperBound(values, query, static value => value);

    /// <summary>Lower bound of ascending keys read from the existing elements.</summary>
    public static int LowerBound<T, TKey>(IReadOnlyList<T> values, TKey query, Func<T, TKey> key)
        where TKey : IComparable<TKey> => Bound(values, query, key, upper: false);

    /// <summary>Upper bound of ascending keys read from the existing elements.</summary>
    public static int UpperBound<T, TKey>(IReadOnlyList<T> values, TKey query, Func<T, TKey> key)
        where TKey : IComparable<TKey> => Bound(values, query, key, upper: true);

    /// <summary>Lower bound of ascending keys read from a span, for value arrays that should not be boxed into a list.</summary>
    public static int LowerBound<T, TKey>(ReadOnlySpan<T> values, TKey query, Func<T, TKey> key)
        where TKey : IComparable<TKey> => Bound(values, query, key, upper: false);

    /// <summary>Upper bound of ascending keys read from a span.</summary>
    public static int UpperBound<T, TKey>(ReadOnlySpan<T> values, TKey query, Func<T, TKey> key)
        where TKey : IComparable<TKey> => Bound(values, query, key, upper: true);

    private static int Bound<T, TKey>(ReadOnlySpan<T> values, TKey query, Func<T, TKey> key, bool upper)
        where TKey : IComparable<TKey>
    {
        Ensure.ThrowIfNull(key);
        var low = 0;
        var high = values.Length;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var order = key(values[middle]).CompareTo(query);
            if (order < 0 || upper && order == 0) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private static int Bound<T, TKey>(IReadOnlyList<T> values, TKey query, Func<T, TKey> key, bool upper)
        where TKey : IComparable<TKey>
    {
        Ensure.ThrowIfNull(values);
        Ensure.ThrowIfNull(key);
        var low = 0;
        var high = values.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            var comparison = key(values[middle]).CompareTo(query);
            if (comparison < 0 || upper && comparison == 0) low = middle + 1;
            else high = middle;
        }
        return low;
    }
}
