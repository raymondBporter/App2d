using System.Numerics;

namespace App2d.Core.Validation;

/// <summary>
/// Reusable numeric predicates, independent of exceptions. Plain comparisons use numeric operators;
/// use the finite predicates when NaN and infinity are invalid. Vector comparisons are component-wise.
/// Positive means strictly greater than zero; nonzero vectors may have zero components (e.g. UnitX).
/// </summary>
public static class NumericValidation
{
    public static bool IsFinite<T>(T value) where T : INumberBase<T> => T.IsFinite(value);
    public static bool IsZero<T>(T value) where T : INumberBase<T> => value == T.Zero;
    public static bool IsPositive<T>(T value) where T : INumber<T> => value > T.Zero;
    public static bool IsNegative<T>(T value) where T : INumber<T> => value < T.Zero;
    public static bool IsNonNegative<T>(T value) where T : INumber<T> => value >= T.Zero;
    public static bool IsFiniteAndNonZero<T>(T value) where T : INumberBase<T> => IsFinite(value) && !IsZero(value);
    public static bool IsFiniteAndPositive<T>(T value) where T : INumber<T> => IsFinite(value) && IsPositive(value);
    public static bool IsFiniteAndNonNegative<T>(T value) where T : INumber<T> => IsFinite(value) && IsNonNegative(value);
    public static bool IsLessThan<T>(T value, T limit) where T : INumber<T> => value < limit;
    public static bool IsLessThanOrEqual<T>(T value, T limit) where T : INumber<T> => value <= limit;
    public static bool IsGreaterThan<T>(T value, T limit) where T : INumber<T> => value > limit;
    public static bool IsGreaterThanOrEqual<T>(T value, T limit) where T : INumber<T> => value >= limit;
    public static bool IsInClosedRange<T>(T value, T minimum, T maximum) where T : INumber<T> => value >= minimum && value <= maximum;
    public static bool IsInOpenRange<T>(T value, T minimum, T maximum) where T : INumber<T> => value > minimum && value < maximum;

    public static bool IsFinite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    public static bool IsZero(Vector2 value) => value == Vector2.Zero;
    public static bool IsPositive(Vector2 value) => value.X > 0f && value.Y > 0f;
    public static bool HasNegativeComponent(Vector2 value) => value.X < 0f || value.Y < 0f;
    public static bool IsNonNegative(Vector2 value) => value.X >= 0f && value.Y >= 0f;
    public static bool IsFiniteAndNonZero(Vector2 value) => IsFinite(value) && !IsZero(value);
    public static bool IsFiniteAndPositive(Vector2 value) => IsFinite(value) && IsPositive(value);
    public static bool IsFiniteAndNonNegative(Vector2 value) => IsFinite(value) && IsNonNegative(value);
    public static bool IsComponentWiseLessThan(Vector2 value, Vector2 limit) => value.X < limit.X && value.Y < limit.Y;
    public static bool IsComponentWiseLessThanOrEqual(Vector2 value, Vector2 limit) => value.X <= limit.X && value.Y <= limit.Y;
    public static bool IsComponentWiseGreaterThan(Vector2 value, Vector2 limit) => value.X > limit.X && value.Y > limit.Y;
    public static bool IsComponentWiseGreaterThanOrEqual(Vector2 value, Vector2 limit) => value.X >= limit.X && value.Y >= limit.Y;
    public static bool IsInClosedRange(Vector2 value, Vector2 minimum, Vector2 maximum) =>
        IsComponentWiseGreaterThanOrEqual(value, minimum) && IsComponentWiseLessThanOrEqual(value, maximum);
    public static bool IsInOpenRange(Vector2 value, Vector2 minimum, Vector2 maximum) =>
        IsComponentWiseGreaterThan(value, minimum) && IsComponentWiseLessThan(value, maximum);

    public static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    public static bool IsZero(Vector3 value) => value == Vector3.Zero;
    public static bool IsPositive(Vector3 value) => value.X > 0f && value.Y > 0f && value.Z > 0f;
    public static bool HasNegativeComponent(Vector3 value) => value.X < 0f || value.Y < 0f || value.Z < 0f;
    public static bool IsNonNegative(Vector3 value) => value.X >= 0f && value.Y >= 0f && value.Z >= 0f;
    public static bool IsFiniteAndNonZero(Vector3 value) => IsFinite(value) && !IsZero(value);
    public static bool IsFiniteAndPositive(Vector3 value) => IsFinite(value) && IsPositive(value);
    public static bool IsFiniteAndNonNegative(Vector3 value) => IsFinite(value) && IsNonNegative(value);
    public static bool IsComponentWiseLessThan(Vector3 value, Vector3 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z;
    public static bool IsComponentWiseLessThanOrEqual(Vector3 value, Vector3 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z;
    public static bool IsComponentWiseGreaterThan(Vector3 value, Vector3 limit) => value.X > limit.X && value.Y > limit.Y && value.Z > limit.Z;
    public static bool IsComponentWiseGreaterThanOrEqual(Vector3 value, Vector3 limit) => value.X >= limit.X && value.Y >= limit.Y && value.Z >= limit.Z;
    public static bool IsInClosedRange(Vector3 value, Vector3 minimum, Vector3 maximum) =>
        IsComponentWiseGreaterThanOrEqual(value, minimum) && IsComponentWiseLessThanOrEqual(value, maximum);
    public static bool IsInOpenRange(Vector3 value, Vector3 minimum, Vector3 maximum) =>
        IsComponentWiseGreaterThan(value, minimum) && IsComponentWiseLessThan(value, maximum);

    public static bool IsFinite(Vector4 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
    public static bool IsZero(Vector4 value) => value == Vector4.Zero;
    public static bool IsPositive(Vector4 value) => value.X > 0f && value.Y > 0f && value.Z > 0f && value.W > 0f;
    public static bool HasNegativeComponent(Vector4 value) => value.X < 0f || value.Y < 0f || value.Z < 0f || value.W < 0f;
    public static bool IsNonNegative(Vector4 value) => value.X >= 0f && value.Y >= 0f && value.Z >= 0f && value.W >= 0f;
    public static bool IsFiniteAndNonZero(Vector4 value) => IsFinite(value) && !IsZero(value);
    public static bool IsFiniteAndPositive(Vector4 value) => IsFinite(value) && IsPositive(value);
    public static bool IsFiniteAndNonNegative(Vector4 value) => IsFinite(value) && IsNonNegative(value);
    public static bool IsComponentWiseLessThan(Vector4 value, Vector4 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z && value.W < limit.W;
    public static bool IsComponentWiseLessThanOrEqual(Vector4 value, Vector4 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z && value.W <= limit.W;
    public static bool IsComponentWiseGreaterThan(Vector4 value, Vector4 limit) => value.X > limit.X && value.Y > limit.Y && value.Z > limit.Z && value.W > limit.W;
    public static bool IsComponentWiseGreaterThanOrEqual(Vector4 value, Vector4 limit) => value.X >= limit.X && value.Y >= limit.Y && value.Z >= limit.Z && value.W >= limit.W;
    public static bool IsInClosedRange(Vector4 value, Vector4 minimum, Vector4 maximum) =>
        IsComponentWiseGreaterThanOrEqual(value, minimum) && IsComponentWiseLessThanOrEqual(value, maximum);
    public static bool IsInOpenRange(Vector4 value, Vector4 minimum, Vector4 maximum) =>
        IsComponentWiseGreaterThan(value, minimum) && IsComponentWiseLessThan(value, maximum);
}
