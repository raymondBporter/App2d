using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// A closed [Min, Max] scalar range. Endpoints are ordered and never NaN; infinities are allowed.
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
