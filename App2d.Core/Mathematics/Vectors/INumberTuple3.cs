using System.Numerics;

namespace App2d.Core.Mathematics.Vectors;

/// <summary>
/// Three numeric coordinates with concrete construction; suitable for points or vectors.
/// Supplying X, Y, Z and Create is enough: the indexed-tuple statics below come for free.
/// </summary>
public interface INumberTuple3<TSelf, TReal> : INumberTuple<TSelf, TReal>
    where TSelf : struct, INumberTuple3<TSelf, TReal>
    where TReal : INumber<TReal>
{
    TReal X { get; }
    TReal Y { get; }
    TReal Z { get; }
    static abstract TSelf Create(TReal x, TReal y, TReal z);

    static int INumberTuple<TSelf, TReal>.Dimension => 3;
    static TSelf INumberTuple<TSelf, TReal>.FromCoordinates(ReadOnlySpan<TReal> coordinates) => TSelf.Create(coordinates[0], coordinates[1], coordinates[2]);
}
