namespace App2d.Core.Mathematics;

/// <summary>Float ranges drawn from a caller-owned <see cref="Random"/> instance.</summary>
public static class RandomExtensions
{
    /// <summary>Returns a value from zero up to <paramref name="maximum"/>.</summary>
    public static float NextFloat(this Random random, float maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        if (!float.IsFinite(maximum))
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be finite.");

        return random.NextSingle() * maximum;
    }

    /// <summary>Returns a value from <paramref name="minimum"/> up to <paramref name="maximum"/>.</summary>
    public static float NextFloat(this Random random, float minimum, float maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (!float.IsFinite(minimum))
            throw new ArgumentOutOfRangeException(nameof(minimum), "Minimum must be finite.");
        if (!float.IsFinite(maximum) || maximum < minimum || !float.IsFinite(maximum - minimum))
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be finite and at least minimum, with a finite range.");

        return minimum + random.NextSingle() * (maximum - minimum);
    }
}
