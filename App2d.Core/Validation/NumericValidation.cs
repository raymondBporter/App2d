using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Validation;

/// <summary>
/// Numeric predicates shared by throwing checks and nonthrowing consumers.
/// Sign, nonzero and range checks require finite values unless infinity is explicitly allowed.
/// Vector comparisons apply to every component; nonzero means the whole vector is not zero.
/// Ordering uses ordinary operators: infinity is allowed and NaN never compares successfully.
/// </summary>
public static class NumericValidation
{
    public static bool IsNumber<T>(T value) where T : INumberBase<T> => !T.IsNaN(value);
    public static bool IsFinite<T>(T value) where T : INumberBase<T> => T.IsFinite(value);
    public static bool IsPositive<T>(T value) where T : INumber<T> => IsFinite(value) && value > T.Zero;
    public static bool IsNegative<T>(T value, bool allowNegativeInfinity = false) where T : INumber<T> =>
        (allowNegativeInfinity || IsFinite(value)) && value < T.Zero;
    public static bool IsNonNegative<T>(T value, bool allowInfinity = false) where T : INumber<T> =>
        (allowInfinity || IsFinite(value)) && value >= T.Zero;
    public static bool IsNonPositive<T>(T value) where T : INumber<T> => IsFinite(value) && value <= T.Zero;
    public static bool IsNonZero<T>(T value) where T : INumberBase<T> => IsFinite(value) && value != T.Zero;
    public static bool IsEqual<T>(T value, T expected) where T : INumberBase<T> => value == expected;
    public static bool IsNotEqual<T>(T value, T other) where T : INumberBase<T> => value != other;
    public static bool IsLessThan<T>(T value, T limit) where T : INumber<T> => value < limit;
    public static bool IsLessThanOrEqual<T>(T value, T limit) where T : INumber<T> => value <= limit;
    public static bool IsGreaterThan<T>(T value, T limit) where T : INumber<T> => value > limit;
    public static bool IsGreaterThanOrEqual<T>(T value, T limit) where T : INumber<T> => value >= limit;
    public static bool IsInRange<T>(T value, T minimum, T maximum, RangeBounds bounds = RangeBounds.Closed) where T : INumber<T> =>
        IsFinite(value) && (bounds switch
        {
            RangeBounds.Closed => value >= minimum && value <= maximum,
            RangeBounds.Open => value > minimum && value < maximum,
            RangeBounds.ClosedOpen => value >= minimum && value < maximum,
            RangeBounds.OpenClosed => value > minimum && value <= maximum,
            _ => false
        });

    public static bool IsNumber(Vector2 value) => IsNumber(value.X) && IsNumber(value.Y);
    public static bool IsFinite(Vector2 value) => IsFinite(value.X) && IsFinite(value.Y);
    public static bool IsPositive(Vector2 value) => IsPositive(value.X) && IsPositive(value.Y);
    public static bool IsNegative(Vector2 value, bool allowNegativeInfinity = false) => IsNegative(value.X, allowNegativeInfinity) && IsNegative(value.Y, allowNegativeInfinity);
    public static bool IsNonNegative(Vector2 value, bool allowInfinity = false) => IsNonNegative(value.X, allowInfinity) && IsNonNegative(value.Y, allowInfinity);
    public static bool IsNonPositive(Vector2 value) => IsNonPositive(value.X) && IsNonPositive(value.Y);
    public static bool IsNonZero(Vector2 value) => IsFinite(value) && value != Vector2.Zero;
    public static bool IsLessThan(Vector2 value, Vector2 limit) => value.X < limit.X && value.Y < limit.Y;
    public static bool IsLessThanOrEqual(Vector2 value, Vector2 limit) => value.X <= limit.X && value.Y <= limit.Y;
    public static bool IsGreaterThan(Vector2 value, Vector2 limit) => value.X > limit.X && value.Y > limit.Y;
    public static bool IsGreaterThanOrEqual(Vector2 value, Vector2 limit) => value.X >= limit.X && value.Y >= limit.Y;
    public static bool IsInRange(Vector2 value, Vector2 minimum, Vector2 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds);

    public static bool IsNumber(Vector3 value) => IsNumber(value.X) && IsNumber(value.Y) && IsNumber(value.Z);
    public static bool IsFinite(Vector3 value) => IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
    public static bool IsPositive(Vector3 value) => IsPositive(value.X) && IsPositive(value.Y) && IsPositive(value.Z);
    public static bool IsNegative(Vector3 value, bool allowNegativeInfinity = false) => IsNegative(value.X, allowNegativeInfinity) && IsNegative(value.Y, allowNegativeInfinity) && IsNegative(value.Z, allowNegativeInfinity);
    public static bool IsNonNegative(Vector3 value, bool allowInfinity = false) => IsNonNegative(value.X, allowInfinity) && IsNonNegative(value.Y, allowInfinity) && IsNonNegative(value.Z, allowInfinity);
    public static bool IsNonPositive(Vector3 value) => IsNonPositive(value.X) && IsNonPositive(value.Y) && IsNonPositive(value.Z);
    public static bool IsNonZero(Vector3 value) => IsFinite(value) && value != Vector3.Zero;
    public static bool IsLessThan(Vector3 value, Vector3 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z;
    public static bool IsLessThanOrEqual(Vector3 value, Vector3 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z;
    public static bool IsGreaterThan(Vector3 value, Vector3 limit) => value.X > limit.X && value.Y > limit.Y && value.Z > limit.Z;
    public static bool IsGreaterThanOrEqual(Vector3 value, Vector3 limit) => value.X >= limit.X && value.Y >= limit.Y && value.Z >= limit.Z;
    public static bool IsInRange(Vector3 value, Vector3 minimum, Vector3 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds) &&
        IsInRange(value.Z, minimum.Z, maximum.Z, bounds);

    public static bool IsNumber(Vector4 value) => IsNumber(value.X) && IsNumber(value.Y) && IsNumber(value.Z) && IsNumber(value.W);
    public static bool IsFinite(Vector4 value) => IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z) && IsFinite(value.W);
    public static bool IsPositive(Vector4 value) => IsPositive(value.X) && IsPositive(value.Y) && IsPositive(value.Z) && IsPositive(value.W);
    public static bool IsNegative(Vector4 value, bool allowNegativeInfinity = false) => IsNegative(value.X, allowNegativeInfinity) && IsNegative(value.Y, allowNegativeInfinity) && IsNegative(value.Z, allowNegativeInfinity) && IsNegative(value.W, allowNegativeInfinity);
    public static bool IsNonNegative(Vector4 value, bool allowInfinity = false) => IsNonNegative(value.X, allowInfinity) && IsNonNegative(value.Y, allowInfinity) && IsNonNegative(value.Z, allowInfinity) && IsNonNegative(value.W, allowInfinity);
    public static bool IsNonPositive(Vector4 value) => IsNonPositive(value.X) && IsNonPositive(value.Y) && IsNonPositive(value.Z) && IsNonPositive(value.W);
    public static bool IsNonZero(Vector4 value) => IsFinite(value) && value != Vector4.Zero;
    public static bool IsLessThan(Vector4 value, Vector4 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z && value.W < limit.W;
    public static bool IsLessThanOrEqual(Vector4 value, Vector4 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z && value.W <= limit.W;
    public static bool IsGreaterThan(Vector4 value, Vector4 limit) => value.X > limit.X && value.Y > limit.Y && value.Z > limit.Z && value.W > limit.W;
    public static bool IsGreaterThanOrEqual(Vector4 value, Vector4 limit) => value.X >= limit.X && value.Y >= limit.Y && value.Z >= limit.Z && value.W >= limit.W;
    public static bool IsInRange(Vector4 value, Vector4 minimum, Vector4 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds) &&
        IsInRange(value.Z, minimum.Z, maximum.Z, bounds) &&
        IsInRange(value.W, minimum.W, maximum.W, bounds);

    public static bool IsNumber(Vector2f value) => IsNumber(value.X) && IsNumber(value.Y);
    public static bool IsFinite(Vector2f value) => Vector2f.IsFinite(value);
    public static bool IsNonZero(Vector2f value) => IsFinite(value) && value != Vector2f.Zero;

    public static bool IsNumber(Vector3f value) => IsNumber(value.X) && IsNumber(value.Y) && IsNumber(value.Z);
    public static bool IsFinite(Vector3f value) => Vector3f.IsFinite(value);
    public static bool IsNonZero(Vector3f value) => IsFinite(value) && value != Vector3f.Zero;

    public static bool IsNumber(Vector4f value) => IsNumber(value.X) && IsNumber(value.Y) && IsNumber(value.Z) && IsNumber(value.W);
    public static bool IsFinite(Vector4f value) => Vector4f.IsFinite(value);
    public static bool IsNonZero(Vector4f value) => IsFinite(value) && value != Vector4f.Zero;

    public static bool IsNumber(Vector2d value) => IsNumber(value.X) && IsNumber(value.Y);
    public static bool IsFinite(Vector2d value) => Vector2d.IsFinite(value);
    public static bool IsNonZero(Vector2d value) => IsFinite(value) && value != Vector2d.Zero;

    public static bool IsNumber(Vector3d value) => IsNumber(value.X) && IsNumber(value.Y) && IsNumber(value.Z);
    public static bool IsFinite(Vector3d value) => Vector3d.IsFinite(value);
    public static bool IsNonZero(Vector3d value) => IsFinite(value) && value != Vector3d.Zero;

    public static bool IsNumber(Vector4d value) => IsNumber(value.X) && IsNumber(value.Y) && IsNumber(value.Z) && IsNumber(value.W);
    public static bool IsFinite(Vector4d value) => Vector4d.IsFinite(value);
    public static bool IsNonZero(Vector4d value) => IsFinite(value) && value != Vector4d.Zero;

    public static bool IsNumber(Matrix3x2 value) =>
        IsNumber(value.M11) && IsNumber(value.M12) &&
        IsNumber(value.M21) && IsNumber(value.M22) &&
        IsNumber(value.M31) && IsNumber(value.M32);
    public static bool IsFinite(Matrix3x2 value) => value.IsFinite();
    public static bool IsInvertible(Matrix3x2 value) => IsInvertible(value, out _);
    public static bool IsInvertible(Matrix3x2 value, out Matrix3x2 inverse)
    {
        inverse = default;
        return IsFinite(value) && Matrix3x2.Invert(value, out inverse) && IsFinite(inverse);
    }

    public static bool IsNumber(Matrix4x4 value) =>
        IsNumber(value.M11) && IsNumber(value.M12) && IsNumber(value.M13) && IsNumber(value.M14) &&
        IsNumber(value.M21) && IsNumber(value.M22) && IsNumber(value.M23) && IsNumber(value.M24) &&
        IsNumber(value.M31) && IsNumber(value.M32) && IsNumber(value.M33) && IsNumber(value.M34) &&
        IsNumber(value.M41) && IsNumber(value.M42) && IsNumber(value.M43) && IsNumber(value.M44);
    public static bool IsFinite(Matrix4x4 value) => value.IsFinite();
    public static bool IsInvertible(Matrix4x4 value) => IsInvertible(value, out _);
    public static bool IsInvertible(Matrix4x4 value, out Matrix4x4 inverse)
    {
        inverse = default;
        return IsFinite(value) && Matrix4x4.Invert(value, out inverse) && IsFinite(inverse);
    }
}
