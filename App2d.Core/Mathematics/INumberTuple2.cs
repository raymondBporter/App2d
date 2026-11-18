using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Two numeric coordinates with concrete construction; suitable for points or vectors.</summary>
public interface INumberTuple2<TSelf, TReal> : INumberTuple<TSelf, TReal>
    where TSelf : struct, INumberTuple2<TSelf, TReal>
    where TReal : INumber<TReal>
{
    TReal X { get; }
    TReal Y { get; }
    static abstract TSelf Create(TReal x, TReal y);
}
