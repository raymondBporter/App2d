using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Validation;

/// <summary>
/// Numeric rules without exceptions. Sign, nonzero, and range checks require finite values.
/// Vector sign/range rules apply to every component; nonzero means the whole vector is not zero.
/// Component-wise ordering follows ordinary operators, allowing infinity but rejecting NaN.
/// </summary>
public static class NumericValidation
{
    public static bool IsFinite<T>(T value) where T : INumberBase<T> => T.IsFinite(value);
    public static bool IsFinite(Matrix3x2 value) => value.IsFinite();
    public static bool IsPositive<T>(T value) where T : INumber<T> => IsFinite(value) && value > T.Zero;
    public static bool IsNonNegative<T>(T value) where T : INumber<T> => IsFinite(value) && value >= T.Zero;
    public static bool IsNonZero<T>(T value) where T : INumberBase<T> => IsFinite(value) && value != T.Zero;

    public static bool IsInRange<T>(T value, T minimum, T maximum, RangeBounds bounds = RangeBounds.Closed) where T : INumber<T> =>
        IsFinite(value) && (bounds switch
        {
            RangeBounds.Closed => value >= minimum && value <= maximum,
            RangeBounds.Open => value > minimum && value < maximum,
            RangeBounds.ClosedOpen => value >= minimum && value < maximum,
            RangeBounds.OpenClosed => value > minimum && value <= maximum,
            _ => false
        });

    public static bool IsFinite(Vector2 value) => value.IsFinite();
    public static bool IsPositive(Vector2 value) => IsFinite(value) && value.X > 0f && value.Y > 0f;
    public static bool IsNonNegative(Vector2 value) => IsFinite(value) && value.X >= 0f && value.Y >= 0f;
    public static bool IsNonZero(Vector2 value) => IsFinite(value) && value != Vector2.Zero;
    public static bool IsComponentWiseLessThan(Vector2 value, Vector2 limit) => value.X < limit.X && value.Y < limit.Y;
    public static bool IsComponentWiseLessThanOrEqual(Vector2 value, Vector2 limit) => value.X <= limit.X && value.Y <= limit.Y;
    public static bool IsComponentWiseGreaterThan(Vector2 value, Vector2 limit) => value.X > limit.X && value.Y > limit.Y;
    public static bool IsComponentWiseGreaterThanOrEqual(Vector2 value, Vector2 limit) => value.X >= limit.X && value.Y >= limit.Y;
    public static bool IsInRange(Vector2 value, Vector2 minimum, Vector2 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds);

    public static bool IsFinite(Vector3 value) => value.IsFinite();
    public static bool IsPositive(Vector3 value) => IsFinite(value) && value.X > 0f && value.Y > 0f && value.Z > 0f;
    public static bool IsNonNegative(Vector3 value) => IsFinite(value) && value.X >= 0f && value.Y >= 0f && value.Z >= 0f;
    public static bool IsNonZero(Vector3 value) => IsFinite(value) && value != Vector3.Zero;
    public static bool IsComponentWiseLessThan(Vector3 value, Vector3 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z;
    public static bool IsComponentWiseLessThanOrEqual(Vector3 value, Vector3 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z;
    public static bool IsComponentWiseGreaterThan(Vector3 value, Vector3 limit) => value.X > limit.X && value.Y > limit.Y && value.Z > limit.Z;
    public static bool IsComponentWiseGreaterThanOrEqual(Vector3 value, Vector3 limit) => value.X >= limit.X && value.Y >= limit.Y && value.Z >= limit.Z;
    public static bool IsInRange(Vector3 value, Vector3 minimum, Vector3 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds) &&
        IsInRange(value.Z, minimum.Z, maximum.Z, bounds);

    public static bool IsFinite(Vector4 value) => value.IsFinite();
    public static bool IsPositive(Vector4 value) => IsFinite(value) && value.X > 0f && value.Y > 0f && value.Z > 0f && value.W > 0f;
    public static bool IsNonNegative(Vector4 value) => IsFinite(value) && value.X >= 0f && value.Y >= 0f && value.Z >= 0f && value.W >= 0f;
    public static bool IsNonZero(Vector4 value) => IsFinite(value) && value != Vector4.Zero;
    public static bool IsComponentWiseLessThan(Vector4 value, Vector4 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z && value.W < limit.W;
    public static bool IsComponentWiseLessThanOrEqual(Vector4 value, Vector4 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z && value.W <= limit.W;
    public static bool IsComponentWiseGreaterThan(Vector4 value, Vector4 limit) => value.X > limit.X && value.Y > limit.Y && value.Z > limit.Z && value.W > limit.W;
    public static bool IsComponentWiseGreaterThanOrEqual(Vector4 value, Vector4 limit) => value.X >= limit.X && value.Y >= limit.Y && value.Z >= limit.Z && value.W >= limit.W;
    public static bool IsInRange(Vector4 value, Vector4 minimum, Vector4 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds) &&
        IsInRange(value.Z, minimum.Z, maximum.Z, bounds) &&
        IsInRange(value.W, minimum.W, maximum.W, bounds);
}
