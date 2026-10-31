using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A fixed-size Euclidean vector with IEEE floating-point coordinates. Generic algorithms keep
/// the concrete struct as TSelf; they do not store interface values or box coordinates.
/// </summary>
public interface IVector<TSelf, TReal> :
    IAdditionOperators<TSelf, TSelf, TSelf>,
    ISubtractionOperators<TSelf, TSelf, TSelf>,
    IMultiplyOperators<TSelf, TReal, TSelf>,
    IDivisionOperators<TSelf, TReal, TSelf>,
    IUnaryNegationOperators<TSelf, TSelf>,
    IAdditiveIdentity<TSelf, TSelf>
    where TSelf : unmanaged, IVector<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    static abstract int Dimension { get; }
    static abstract TSelf Zero { get; }
    static TSelf IAdditiveIdentity<TSelf, TSelf>.AdditiveIdentity => TSelf.Zero;

    /// <summary>Reads a coordinate in axis order. An invalid index throws.</summary>
    TReal this[int index] { get; }

    // These primitives stay concrete so implementations can use packed arithmetic.
    static abstract TReal Dot(TSelf left, TSelf right);
    static abstract TSelf Min(TSelf left, TSelf right);
    static abstract TSelf Max(TSelf left, TSelf right);
    static abstract TSelf Abs(TSelf value);
    static abstract bool IsFinite(TSelf value);
}
