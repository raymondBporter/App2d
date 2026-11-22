using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Four numeric coordinates with concrete construction.
/// Supplying X, Y, Z, W and Create is enough: the indexed-tuple statics below come for free.
/// </summary>
public interface INumberTuple4<TSelf, TReal> : INumberTuple<TSelf, TReal>
    where TSelf : struct, INumberTuple4<TSelf, TReal>
    where TReal : INumber<TReal>
{
    TReal X { get; }
    TReal Y { get; }
    TReal Z { get; }
    TReal W { get; }
    static abstract TSelf Create(TReal x, TReal y, TReal z, TReal w);

    static int INumberTuple<TSelf, TReal>.Dimension => 4;
    static TSelf INumberTuple<TSelf, TReal>.FromCoordinates(ReadOnlySpan<TReal> coordinates) => TSelf.Create(coordinates[0], coordinates[1], coordinates[2], coordinates[3]);
}
