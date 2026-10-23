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

        var range = ArgGuard.NonNegative(maximum - minimum, nameof(maximum));

        return minimum + random.NextSingle() * range;
    }
}
