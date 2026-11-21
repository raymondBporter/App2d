using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Directed endpoint arithmetic. Begin and end need not be ordered; no interval value is required.</summary>
public static partial class Interval
{
    /// <summary>begin + amount * (end - begin), including extrapolation. Equal endpoints give a constant.</summary>
    public static T Lerp<T>(T begin, T end, T amount) where T : INumber<T>
    {
        if (amount == T.Zero || begin == end) return begin;
        if (amount == T.One) return end;
        // Opposite signs can overflow the difference even when the result is representable.
        return begin < T.Zero && end > T.Zero || begin > T.Zero && end < T.Zero
            ? checked((T.One - amount) * begin + amount * end)
            : checked(begin + amount * (end - begin));
    }

    public static T LerpClamped<T>(T begin, T end, T amount) where T : INumber<T> =>
        Lerp(begin, end, T.Clamp(amount, T.Zero, T.One));

    /// <summary>Maps begin to zero and end to one, preserving direction. A collapsed source returns zero.</summary>
    public static T InverseLerp<T>(T begin, T end, T value) where T : INumber<T>
    {
        if (begin == end || value == begin) return T.Zero;
        if (value == end) return T.One;
        var width = checked(end - begin);
        var offset = checked(value - begin);
        if (T.IsFinite(width) && T.IsFinite(offset)) return offset / width;
        var two = T.One + T.One;
        return (value / two - begin / two) / (end / two - begin / two);
    }

    public static T InverseLerpClamped<T>(T begin, T end, T value) where T : INumber<T> =>
        InverseLerp(begin, end, T.Clamp(value, T.Min(begin, end), T.Max(begin, end)));

    /// <summary>Maps a directed source span to a directed destination span, including extrapolation.</summary>
    public static T Remap<T>(T value, T sourceBegin, T sourceEnd, T destinationBegin, T destinationEnd) where T : INumber<T> =>
        Lerp(destinationBegin, destinationEnd, InverseLerp(sourceBegin, sourceEnd, value));

    public static T RemapClamped<T>(T value, T sourceBegin, T sourceEnd, T destinationBegin, T destinationEnd) where T : INumber<T> =>
        Lerp(destinationBegin, destinationEnd, InverseLerpClamped(sourceBegin, sourceEnd, value));
}
