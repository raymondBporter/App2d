using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Two numeric coordinates with concrete construction; suitable for points or vectors.
/// Supplying X, Y and Create is enough: the indexed-tuple statics below come for free.
/// </summary>
public interface INumberTuple2<TSelf, TReal> : INumberTuple<TSelf, TReal>
    where TSelf : struct, INumberTuple2<TSelf, TReal>
    where TReal : INumber<TReal>
{
    TReal X { get; }
    TReal Y { get; }
    static abstract TSelf Create(TReal x, TReal y);

    static int INumberTuple<TSelf, TReal>.Dimension => 2;
    static TSelf INumberTuple<TSelf, TReal>.FromCoordinates(ReadOnlySpan<TReal> coordinates) => TSelf.Create(coordinates[0], coordinates[1]);
}
