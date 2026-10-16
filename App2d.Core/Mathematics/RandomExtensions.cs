using App2d.Core.Validation;

namespace App2d.Core.Mathematics;

/// <summary>Float ranges drawn from a caller-owned <see cref="Random"/> instance.</summary>
public static class RandomExtensions
{
    /// <summary>Returns a value from zero up to <paramref name="maximum"/>.</summary>
    public static float NextFloat(this Random random, float maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgGuard.NonNegative(maximum);

        return random.NextSingle() * maximum;
    }

    /// <summary>Returns a value from <paramref name="minimum"/> up to <paramref name="maximum"/>.</summary>
    public static float NextFloat(this Random random, float minimum, float maximum)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgGuard.Finite(minimum);

        if (!float.IsFinite(maximum) || maximum < minimum || !float.IsFinite(maximum - minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum must be finite and at least minimum, with a finite range.");
        }

        return minimum + random.NextSingle() * (maximum - minimum);
    }
}
