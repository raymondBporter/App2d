using App2d.Core.Validation;
using System.Collections.Immutable;

namespace App2d.Core.Animation;

/// <summary>A value at a time, and how to move from it to the next key. A null <see cref="Next"/> uses the track's rule.</summary>
public readonly record struct Key<T>(float Time, T Value, Interpolator<T, float>? Next = null);

/// <summary>
/// Keys in increasing time order. Sampling between two keys interpolates from the earlier one; before the first key and
/// after the last, their values hold.
/// </summary>
public sealed class Track<T>
{
    public Track(Interpolator<T, float> rule, params ReadOnlySpan<Key<T>> keys)
    {
        Ensure.ThrowIf(keys.IsEmpty, "A track needs at least one key.", nameof(keys));
        for (var i = 1; i < keys.Length; i++)
        {
            Ensure.ThrowIf(!(keys[i].Time > keys[i - 1].Time), "Keys must be in increasing time order.", nameof(keys));
        }
        Rule = rule;
        Keys = [.. keys];
    }

    /// <summary>How to move between two keys when the earlier one does not say.</summary>
    public Interpolator<T, float> Rule { get; }
    public ImmutableArray<Key<T>> Keys { get; }

    public T Sample(float time)
    {
        if (time <= Keys[0].Time) return Keys[0].Value;
        if (time >= Keys[^1].Time) return Keys[^1].Value;
        // The first key after the time; the one before it starts the segment.
        int low = 1, high = Keys.Length - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (Keys[middle].Time <= time) low = middle + 1;
            else high = middle;
        }
        var from = Keys[low - 1];
        var to = Keys[low];
        return time == from.Time ? from.Value : (from.Next ?? Rule).Evaluate(from.Value, to.Value, (time - from.Time) / (to.Time - from.Time));
    }
}

/// <summary>Tracks with the usual rule for their value.</summary>
public static class Track
{
    /// <summary>Interpolates by the value type's own rule.</summary>
    public static Track<T> Of<T>(params ReadOnlySpan<Key<T>> keys) where T : IInterpolatable<T, float> => new(Interpolation.Default<T, float>(), keys);
    /// <summary>Interpolates numbers in a straight line.</summary>
    public static Track<float> Linear(params ReadOnlySpan<Key<float>> keys) => new(Interpolation.Linear<float>(), keys);
    /// <summary>One value for the whole clip.</summary>
    public static Track<T> Constant<T>(T value) => new(Interpolation.Hold<T, float>(), [new(0, value)]);
    /// <summary>Holds each key's value until the next key.</summary>
    public static Track<T> Held<T>(params ReadOnlySpan<Key<T>> keys) => new(Interpolation.Hold<T, float>(), keys);
}
