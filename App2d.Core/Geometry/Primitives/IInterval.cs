using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// A nonempty closed [Min, Max] scalar range. Endpoints are ordered and never NaN.
/// Negative/positive infinity may represent an unbounded lower/upper end.
/// Implementations retain their own storage, validation and any additional domain-specific data.
/// </summary>
/// <typeparam name="T">The numeric type of the endpoints.</typeparam>
public interface IInterval<T> where T : INumber<T>
{
    /// <summary>The smallest value in the range.</summary>
    T Min { get; }

    /// <summary>The largest value in the range.</summary>
    T Max { get; }
}
