using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics.Vectors;

/// <summary>
/// A four-dimensional floating-point vector. A struct supplies X, Y, Z, W, Create and its operators;
/// this interface fills in what IVector requires for four coordinates.
/// As with IVector, a struct replaces any of them by declaring the same public static.
/// </summary>
public interface IVector4<TSelf, TReal> :
    IVector<TSelf, TReal>,
    INumberTuple4<TSelf, TReal>
    where TSelf : unmanaged, IVector4<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    static TSelf IVector<TSelf, TReal>.Zero { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => TSelf.Create(TReal.Zero, TReal.Zero, TReal.Zero, TReal.Zero); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TReal IVector<TSelf, TReal>.Dot(TSelf left, TSelf right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z + left.W * right.W;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Min(TSelf left, TSelf right) => TSelf.Create(TReal.Min(left.X, right.X), TReal.Min(left.Y, right.Y), TReal.Min(left.Z, right.Z), TReal.Min(left.W, right.W));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Max(TSelf left, TSelf right) => TSelf.Create(TReal.Max(left.X, right.X), TReal.Max(left.Y, right.Y), TReal.Max(left.Z, right.Z), TReal.Max(left.W, right.W));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Abs(TSelf value) => TSelf.Create(TReal.Abs(value.X), TReal.Abs(value.Y), TReal.Abs(value.Z), TReal.Abs(value.W));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IVector<TSelf, TReal>.IsFinite(TSelf value) => TReal.IsFinite(value.X) && TReal.IsFinite(value.Y) && TReal.IsFinite(value.Z) && TReal.IsFinite(value.W);
}
