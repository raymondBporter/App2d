using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>
/// A fixed-size Euclidean vector with IEEE floating-point coordinates.
/// <para>
/// A vector struct supplies its storage, its operators and the few "must supply" statics below.
/// Everything under "free" is inherited: generic code calls it as TSelf.Length(v), and the
/// float/double extension blocks in VectorExtensions expose it as v.Length() and Vector3d.Length(v).
/// A struct that can do one faster (Vector2f with System.Numerics) declares a public static with
/// the same signature, and that version is then used everywhere instead.
/// </para>
/// Calls go through TSelf, so they are resolved per struct at JIT time: no boxing, no virtual dispatch.
/// Never store or pass a vector as the interface type itself.
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
    // ---- Must supply. These depend on the storage, so each struct can use packed arithmetic. ----
    // IVector2 supplies all of them from X, Y and Create.

    static abstract TSelf Zero { get; }
    static TSelf IAdditiveIdentity<TSelf, TSelf>.AdditiveIdentity => TSelf.Zero;

    // The inherited IMultiplyOperators contract covers vector * scalar. Declare the reverse directly:
    // IMultiplyOperators<TReal, TSelf, TSelf> would require the scalar itself to implement that interface.
    static abstract TSelf operator *(TReal scale, TSelf value);

    static abstract TReal Dot(TSelf left, TSelf right);
    static abstract TSelf Min(TSelf left, TSelf right);
    static abstract TSelf Max(TSelf left, TSelf right);
    static abstract TSelf Abs(TSelf value);
    static abstract bool IsFinite(TSelf value);

    // ---- Free. Arithmetic follows IEEE 754; nothing here validates its inputs. ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal LengthSquared(TSelf value) => TSelf.Dot(value, value);

    /// <summary>The square root of the dot product. Intermediate squares may overflow or underflow.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal Length(TSelf value) => TReal.Sqrt(TSelf.LengthSquared(value));

    /// <summary>Divides by the length. Zero or nonfinite inputs follow ordinary IEEE arithmetic.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Normalize(TSelf value) => value / TSelf.Length(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal DistanceSquared(TSelf first, TSelf second) => TSelf.LengthSquared(first - second);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal Distance(TSelf first, TSelf second) => TSelf.Length(first - second);

    /// <summary>Linear interpolation, or extrapolation when amount lies outside [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Lerp(TSelf first, TSelf second, TReal amount) => first * (TReal.One - amount) + second * amount;

    /// <summary>Computes the midpoint without overflowing on finite coordinates.</summary>
    static virtual TSelf Midpoint(TSelf first, TSelf second)
    {
        var sum = first + second;
        if (TSelf.IsFinite(sum)) return sum / Real<TReal>.Two;

        // Only halve the inputs first on axes whose sum is infinite. Other axes can contain subnormals.
        var dimension = TSelf.Dimension;
        Span<TReal> coordinates = dimension <= 16 ? stackalloc TReal[dimension] : new TReal[dimension];
        for (var axis = 0; axis < dimension; axis++)
        {
            var coordinate = sum[axis];
            coordinates[axis] = TReal.IsInfinity(coordinate)
                ? first[axis] / Real<TReal>.Two + second[axis] / Real<TReal>.Two
                : coordinate / Real<TReal>.Two;
        }
        return TSelf.FromCoordinates(coordinates);
    }

    /// <summary>The component parallel to a unit normal. The normal must already be normalized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Project(TSelf value, TSelf unitNormal) => unitNormal * TSelf.Dot(value, unitNormal);

    /// <summary>Removes the component parallel to a unit normal. The normal must already be normalized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Reject(TSelf value, TSelf unitNormal) => value - TSelf.Project(value, unitNormal);

    /// <summary>Reflects in a plane with a unit normal. The normal must already be normalized.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Reflect(TSelf value, TSelf unitNormal) => value - unitNormal * (Real<TReal>.Two * TSelf.Dot(value, unitNormal));

    /// <summary>Clamps each component using the implementation's Min and Max behavior.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Clamp(TSelf value, TSelf minimum, TSelf maximum) => TSelf.Min(TSelf.Max(value, minimum), maximum);
}
