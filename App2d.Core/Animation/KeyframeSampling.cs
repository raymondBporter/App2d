using System.Diagnostics.CodeAnalysis;

namespace App2d.Core.Animation;

/// <summary>A key's position on a timeline, independent of the value or interpolation policy.</summary>
public interface ITimedKey
{
    float Time { get; }
}

/// <summary>Reads a key's value and interpolates one segment. Timing and endpoint handling belong to the sampler.</summary>
public interface IKeyframeInterpolation<TKey, TValue>
{
    TValue GetValue(TKey key);
    TValue Interpolate(TKey start, TKey end, float phase);
}

/// <summary>Finds a segment in an ordered timeline, holding the first and last keys outside its range.</summary>
public static class KeyframeSampling
{
    /// <summary>
    /// Samples ordered keys with a caller-selected empty value and interpolation rule. Exact keys return their own
    /// values. A generic struct policy avoids boxing or allocating a callback; it may carry interpolation settings.
    /// The sampler neither eases nor clamps values. Keys must be ordered by increasing time.
    /// </summary>
    public static TValue Sample<TKey, TValue, TInterpolation>(IReadOnlyList<TKey> keys, float time, TValue emptyValue, TInterpolation interpolation)
        where TKey : class, ITimedKey
        where TInterpolation : struct, IKeyframeInterpolation<TKey, TValue>
    {
        if (!TrySegment(keys, time, out var start, out var end, out var phase)) return emptyValue;
        return ReferenceEquals(start, end) ? interpolation.GetValue(start) : interpolation.Interpolate(start, end, phase);
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

        // Lower bound: the first key whose time is at or after the query. Starting at 1 retains the first-key hold.
        var low = 1;
        var high = keys.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (time > keys[middle].Time) low = middle + 1;
            else high = middle;
        }
        if (low < keys.Count)
        {
            end = keys[low];
            start = time == end.Time ? end : keys[low - 1];
            phase = ReferenceEquals(start, end) ? 0 : (time - start.Time) / (end.Time - start.Time);
            return true;
        }

        start = end = keys[^1];
        return true;
    }
}
