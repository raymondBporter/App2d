using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Reusable scalar clamping with the numeric type's standard behavior.</summary>
public static class Clamping
{
    /// <summary>Clamps a value to the inclusive [minimum, maximum] range.</summary>
    public static T Clamp<T>(T value, T minimum, T maximum) where T : INumber<T> =>
        T.Clamp(value, minimum, maximum);

    /// <summary>Clamps a value to the inclusive [0, 1] range.</summary>
    public static T Clamp01<T>(T value) where T : INumber<T> => Clamp(value, T.Zero, T.One);
}
