using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>
/// A three-dimensional floating-point vector. A struct supplies X, Y, Z, Create and its operators;
/// this interface fills in what IVector requires for three coordinates and adds the cross product.
/// As with IVector, a struct replaces any of them by declaring the same public static.
/// </summary>
public interface IVector3<TSelf, TReal> :
    IVector<TSelf, TReal>,
    INumberTuple3<TSelf, TReal>
    where TSelf : unmanaged, IVector3<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    // ---- What IVector requires, written once for three coordinates. ----

    static TSelf IVector<TSelf, TReal>.Zero { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => TSelf.Create(TReal.Zero, TReal.Zero, TReal.Zero); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TReal IVector<TSelf, TReal>.Dot(TSelf left, TSelf right) => left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Min(TSelf left, TSelf right) => TSelf.Create(TReal.Min(left.X, right.X), TReal.Min(left.Y, right.Y), TReal.Min(left.Z, right.Z));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Max(TSelf left, TSelf right) => TSelf.Create(TReal.Max(left.X, right.X), TReal.Max(left.Y, right.Y), TReal.Max(left.Z, right.Z));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Abs(TSelf value) => TSelf.Create(TReal.Abs(value.X), TReal.Abs(value.Y), TReal.Abs(value.Z));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IVector<TSelf, TReal>.IsFinite(TSelf value) => TReal.IsFinite(value.X) && TReal.IsFinite(value.Y) && TReal.IsFinite(value.Z);

    // ---- 3D only. ----

    /// <summary>The right-handed cross product, perpendicular to both input vectors.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Cross(TSelf first, TSelf second) => TSelf.Create(
        first.Y * second.Z - first.Z * second.Y,
        first.Z * second.X - first.X * second.Z,
        first.X * second.Y - first.Y * second.X);
}
