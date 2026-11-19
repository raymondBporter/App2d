using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Anything that has a closed [Min, Max] scalar range, such as an authored limit that carries extra data.
/// The interface is only the two endpoints. All interval math lives on <see cref="Interval{T}"/>;
/// call <see cref="Interval{T}.From"/> to use it with another implementation.
/// </summary>
/// <typeparam name="T">The numeric type of the endpoints.</typeparam>
public interface IInterval<T> where T : INumber<T>
{
    /// <summary>The smallest value in the range.</summary>
    T Min { get; }

    /// <summary>The largest value in the range.</summary>
    T Max { get; }
}
