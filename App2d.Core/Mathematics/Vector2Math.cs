using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>Shared 2D calculations on a concrete vector implementation; no interface-valued intermediates.</summary>
public static class Vector2Math<TSelf, TReal>
    where TSelf : unmanaged, IVector2<TSelf, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal Cross(TSelf first, TSelf second) => first.X * second.Y - first.Y * second.X;

    public static TReal Orientation(TSelf first, TSelf second, TSelf third) => Cross(second - first, third - first);
    public static TSelf PerpCcw(TSelf value) => TSelf.Create(-value.Y, value.X);
    public static TSelf PerpCw(TSelf value) => TSelf.Create(value.Y, -value.X);
    public static TReal AngleRadians(TSelf value) => TReal.Atan2(value.Y, value.X);

    public static TSelf FromAngle(TReal angle)
    {
        var (sin, cos) = TReal.SinCos(angle);
        return TSelf.Create(cos, sin);
    }

    public static TSelf Direction(TReal angle) => FromAngle(Ensure.Finite(angle));
    public static TSelf FromAngleDegrees(TReal degrees) => Direction(Angles.DegreesToRadians(degrees));
    public static TSelf FromPolar(TReal radius, TReal angle) => new Polar<TReal>(radius, angle).ToCartesian<TSelf>();
    public static Polar<TReal> ToPolar(TSelf value) => Polar<TReal>.FromCartesian(value);

    public static TSelf Rotate(TSelf value, TReal radians)
    {
        if (radians == TReal.Zero) return value;
        var (sin, cos) = TReal.SinCos(radians);
        return TSelf.Create(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
    }

    public static TSelf Rotate(TSelf value, TReal radians, TSelf pivot) => pivot + Rotate(value - pivot, radians);

    public static bool TryRefract(TSelf direction, TSelf normal, TReal fromIndex, TReal toIndex, out TSelf transmitted)
    {
        Ensure.Positive(fromIndex);
        Ensure.Positive(toIndex);
        var tangent = IVectorExtensions.Reject<TSelf, TReal>(direction, normal);
        if (typeof(TReal) == typeof(float) || typeof(TReal) == typeof(Half))
        {
            var result = Vector2Math<Vector2d, double>.TryRefractTangent(ToVector2d(tangent), ToVector2d(normal),
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

    public static TSelf Transform(TSelf point, Matrix3x2 matrix) => TSelf.Create(
        point.X * TReal.CreateTruncating(matrix.M11) + point.Y * TReal.CreateTruncating(matrix.M21) + TReal.CreateTruncating(matrix.M31),
        point.X * TReal.CreateTruncating(matrix.M12) + point.Y * TReal.CreateTruncating(matrix.M22) + TReal.CreateTruncating(matrix.M32));

    public static TSelf TransformNormal(TSelf direction, Matrix3x2 matrix) => TSelf.Create(
        direction.X * TReal.CreateTruncating(matrix.M11) + direction.Y * TReal.CreateTruncating(matrix.M21),
        direction.X * TReal.CreateTruncating(matrix.M12) + direction.Y * TReal.CreateTruncating(matrix.M22));

    public static Vector2 ToVector2(TSelf value) => new(float.CreateTruncating(value.X), float.CreateTruncating(value.Y));
    public static Vector2f ToVector2f(TSelf value) => new(ToVector2(value));
    public static Vector2d ToVector2d(TSelf value) => new(double.CreateTruncating(value.X), double.CreateTruncating(value.Y));
}
