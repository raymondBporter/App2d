using App2d.Core.Mathematics.Vectors;
using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>
/// A two-dimensional floating-point vector. A struct supplies X, Y, Create and its operators;
/// this interface fills in what IVector requires for two coordinates and adds the 2D-only
/// operations. As with IVector, a struct replaces any of them by declaring the same public static.
/// </summary>
public interface IVector2<TSelf, TReal> :
    IVector<TSelf, TReal>,
    INumberTuple2<TSelf, TReal>
    where TSelf : unmanaged, IVector2<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    // ---- What IVector requires, written once for two coordinates. ----

    static TSelf IVector<TSelf, TReal>.Zero { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => TSelf.Create(TReal.Zero, TReal.Zero); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TReal IVector<TSelf, TReal>.Dot(TSelf left, TSelf right) => left.X * right.X + left.Y * right.Y;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Min(TSelf left, TSelf right) => TSelf.Create(TReal.Min(left.X, right.X), TReal.Min(left.Y, right.Y));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Max(TSelf left, TSelf right) => TSelf.Create(TReal.Max(left.X, right.X), TReal.Max(left.Y, right.Y));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static TSelf IVector<TSelf, TReal>.Abs(TSelf value) => TSelf.Create(TReal.Abs(value.X), TReal.Abs(value.Y));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool IVector<TSelf, TReal>.IsFinite(TSelf value) => TReal.IsFinite(value.X) && TReal.IsFinite(value.Y);

    // ---- 2D only. ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal Cross(TSelf first, TSelf second) => first.X * second.Y - first.Y * second.X;

    /// <summary>Twice the signed area of the triangle: positive when the three points turn counter-clockwise.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal Orientation(TSelf first, TSelf second, TSelf third) => TSelf.Cross(second - first, third - first);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf PerpCcw(TSelf value) => TSelf.Create(-value.Y, value.X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf PerpCw(TSelf value) => TSelf.Create(value.Y, -value.X);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TReal AngleRadians(TSelf value) => TReal.Atan2(value.Y, value.X);

    /// <summary>The unit vector at a finite angle, counter-clockwise from +X.</summary>
    static virtual TSelf Direction(TReal radians)
    {
        var (sin, cos) = TReal.SinCos(Ensure.Finite(radians));
        return TSelf.Create(cos, sin);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf FromAngle(TReal radians) => TSelf.Direction(radians);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf FromAngleDegrees(TReal degrees) => TSelf.Direction(Angles.DegreesToRadians(degrees));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf FromPolar(TReal radius, TReal radians) => new Polar<TReal>(radius, radians).ToCartesian<TSelf>();
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual Polar<TReal> ToPolar(TSelf value) => Polar<TReal>.FromCartesian(value);

    static virtual TSelf Rotate(TSelf value, TReal radians)
    {
        if (radians == TReal.Zero) return value;
        var (sin, cos) = TReal.SinCos(radians);
        return TSelf.Create(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Rotate(TSelf value, TReal radians, TSelf pivot) => pivot + TSelf.Rotate(value - pivot, radians);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf Transform(TSelf point, Matrix3x2 matrix) => TSelf.Create(
        point.X * TReal.CreateTruncating(matrix.M11) + point.Y * TReal.CreateTruncating(matrix.M21) + TReal.CreateTruncating(matrix.M31),
        point.X * TReal.CreateTruncating(matrix.M12) + point.Y * TReal.CreateTruncating(matrix.M22) + TReal.CreateTruncating(matrix.M32));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual TSelf TransformNormal(TSelf direction, Matrix3x2 matrix) => TSelf.Create(
        direction.X * TReal.CreateTruncating(matrix.M11) + direction.Y * TReal.CreateTruncating(matrix.M21),
        direction.X * TReal.CreateTruncating(matrix.M12) + direction.Y * TReal.CreateTruncating(matrix.M22));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual Vector2 ToVector2(TSelf value) => new(float.CreateTruncating(value.X), float.CreateTruncating(value.Y));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual Vector2f ToVector2f(TSelf value) => new(TSelf.ToVector2(value));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static virtual Vector2d ToVector2d(TSelf value) => new(double.CreateTruncating(value.X), double.CreateTruncating(value.Y));

    /// <summary>
    /// Refracts a unit direction through a surface with a unit normal, from one refractive index into another.
    /// Returns false on total internal reflection. Narrow coordinate types are solved in double precision.
    /// </summary>
    static virtual bool TryRefract(TSelf direction, TSelf normal, TReal fromIndex, TReal toIndex, out TSelf transmitted)
    {
        Ensure.Positive(fromIndex);
        Ensure.Positive(toIndex);
        var tangent = TSelf.Reject(direction, normal);
        if (typeof(TReal) == typeof(float) || typeof(TReal) == typeof(Half))
        {
            var result = IVector2<Vector2d, double>.TryRefractTangent(TSelf.ToVector2d(tangent), TSelf.ToVector2d(normal),
                double.CreateTruncating(fromIndex), double.CreateTruncating(toIndex), out var wide);
            transmitted = TSelf.Create(TReal.CreateTruncating(wide.X), TReal.CreateTruncating(wide.Y));
            return result;
        }
        return TryRefractTangent(tangent, normal, fromIndex, toIndex, out transmitted);
    }

    private static bool TryRefractTangent(TSelf tangent, TSelf normal, TReal fromIndex, TReal toIndex, out TSelf transmitted)
    {
        var ratio = fromIndex / toIndex;
        if (TReal.IsPositiveInfinity(ratio))
        {
            var fromExponent = TReal.ILogB(fromIndex);
            var toExponent = TReal.ILogB(toIndex);
            ratio = TReal.ScaleB(fromIndex, -fromExponent) / TReal.ScaleB(toIndex, -toExponent);
            var exponent = fromExponent - toExponent;
            tangent = TSelf.Create(TReal.ScaleB(tangent.X, exponent), TReal.ScaleB(tangent.Y, exponent));
        }
        tangent *= ratio;
        var normalSquared = TReal.One - TSelf.Dot(tangent, tangent);
        if (normalSquared < TReal.Zero)
        {
            transmitted = TSelf.Zero;
            return false;
        }
        transmitted = tangent - normal * TReal.Sqrt(normalSquared);
        return true;
    }
}
