using System.Diagnostics.CodeAnalysis;

namespace App2d.Core.Animation;

/// <summary>A key's position on a timeline, independent of the value or interpolation policy.</summary>
public interface ITimedKey
{
    float Time { get; }
}

/// <summary>Finds a segment in an ordered timeline, holding the first and last keys outside its range.</summary>
public static class KeyframeSampling
{
    /// <summary>Holds a discrete key until the next. Disable the first-key hold to use setup state before the timeline starts.</summary>
    public static T? Held<T>(IReadOnlyList<T> keys, float time, bool holdBeforeFirst = true) where T : class, ITimedKey =>
        TrySegment(keys, time, out var start, out _, out _) && (holdBeforeFirst || time >= start.Time) ? start : null;

    /// <summary>
    /// Samples ordered keys with a caller-selected empty value and interpolation rule. Exact keys return their own
    /// values. The callbacks read the authored value and resolve a segment using core interpolation strategies.
    /// Static callbacks are reusable without per-sample allocation. Keys must be ordered by increasing time.
    /// </summary>
    public static TValue Sample<TKey, TValue>(IReadOnlyList<TKey> keys, float time, TValue emptyValue,
        Func<TKey, TValue> getValue, Func<TKey, TKey, float, TValue> sampleSegment)
        where TKey : class, ITimedKey
    {
        ArgumentNullException.ThrowIfNull(getValue);
        ArgumentNullException.ThrowIfNull(sampleSegment);
        if (!TrySegment(keys, time, out var start, out var end, out var phase)) return emptyValue;
        return ReferenceEquals(start, end) ? getValue(start) : sampleSegment(start, end, phase);
    }

    public static bool TrySegment<T>(IReadOnlyList<T> keys, float time, [NotNullWhen(true)] out T? start, [NotNullWhen(true)] out T? end, out float phase)
        where T : class, ITimedKey
    {
        phase = 0;
        start = end = null;
        if (keys.Count == 0)
            return false;

        start = end = keys[0];
        if (time <= start.Time)
            return true;

        var next = OrderedSearch.LowerBound(keys, time, static key => key.Time);
        if (next == 0) return true;
        if (next < keys.Count)
        {
            end = keys[next];
            start = time == end.Time ? end : keys[next - 1];
            phase = ReferenceEquals(start, end) ? 0 : (time - start.Time) / (end.Time - start.Time);
            return true;
        }

        start = end = keys[^1];
        return true;
    }
}
