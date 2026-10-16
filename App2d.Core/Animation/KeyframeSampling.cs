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
    public static bool TrySegment<T>(IReadOnlyList<T> keys, float time,
        [NotNullWhen(true)] out T? start, [NotNullWhen(true)] out T? end, out float phase) where T : class, ITimedKey
    {
        phase = 0;
        start = end = null;
        if (keys.Count == 0)
            return false;

        start = end = keys[0];
        if (time <= start.Time)
            return true;

        for (var i = 1; i < keys.Count; i++)
        {
            end = keys[i];
            if (time > end.Time)
                continue;
            start = time == end.Time ? end : keys[i - 1];
            phase = ReferenceEquals(start, end) ? 0 : (time - start.Time) / (end.Time - start.Time);
            return true;
        }

        start = end = keys[^1];
        return true;
    }
}
