using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A fixed-size Euclidean vector with IEEE floating-point coordinates. Generic algorithms keep
/// the concrete struct as TSelf; they do not store interface values or box coordinates.
/// </summary>
public interface IVector<TSelf, TReal> :
    INumberTuple<TSelf, TReal>,
    IAdditionOperators<TSelf, TSelf, TSelf>,
    ISubtractionOperators<TSelf, TSelf, TSelf>,
    IMultiplyOperators<TSelf, TReal, TSelf>,
    IDivisionOperators<TSelf, TReal, TSelf>,
    IUnaryNegationOperators<TSelf, TSelf>,
    IAdditiveIdentity<TSelf, TSelf>
    where TSelf : unmanaged, IVector<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    static abstract TSelf Zero { get; }
    static TSelf IAdditiveIdentity<TSelf, TSelf>.AdditiveIdentity => TSelf.Zero;

    // These primitives stay concrete so implementations can use packed arithmetic.
    static abstract TReal Dot(TSelf left, TSelf right);
    static abstract TSelf Min(TSelf left, TSelf right);
    static abstract TSelf Max(TSelf left, TSelf right);
    static abstract TSelf Abs(TSelf value);
    static abstract bool IsFinite(TSelf value);

    // Concrete instance implementations make the API available on constrained generic values
    // without the receiver boxing required by default instance interface implementations.
    TReal Dot(TSelf other);
    TSelf Min(TSelf other);
    TSelf Max(TSelf other);
    TSelf Abs();
    bool IsFinite();
    TReal LengthSquared();
    TReal Length();
    TSelf Normalized();
    TReal DistanceSquared(TSelf other);
    TReal Distance(TSelf other);
    TSelf Lerp(TSelf other, TReal amount);
    TSelf Midpoint(TSelf other);
    TSelf Project(TSelf unitNormal);
    TSelf Reject(TSelf unitNormal);
    TSelf Reflect(TSelf unitNormal);
    TSelf Clamp(TSelf minimum, TSelf maximum);
}
