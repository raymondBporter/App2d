using System.Numerics;

namespace App2d.Core.Mathematics.Vectors;

/// <summary>
/// A fixed-size tuple of numbers. Algorithms retain the concrete TSelf instead of storing
/// interface values. Coordinates need numeric arithmetic, not IEEE floating-point behavior.
/// </summary>
public interface INumberTuple<TSelf, TReal>
    where TSelf : struct, INumberTuple<TSelf, TReal>
    where TReal : INumber<TReal>
{
    static abstract int Dimension { get; }
    static abstract TSelf FromCoordinates(ReadOnlySpan<TReal> coordinates);
    TReal this[int index] { get; }
}
