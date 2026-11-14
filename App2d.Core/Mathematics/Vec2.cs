using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>A two-dimensional vector with IEEE floating-point coordinates.</summary>
[StructLayout(LayoutKind.Sequential)]
[method: JsonConstructor]
public readonly record struct Vec2<TReal>(TReal X, TReal Y) : IVector<Vec2<TReal>, TReal>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public Vec2(TReal value) : this(value, value) { }

    public Vec2(Vec2<float> value) : this(TReal.CreateTruncating(value.X), TReal.CreateTruncating(value.Y)) { }

    public Vec2(Vector2 value) : this(TReal.CreateTruncating(value.X), TReal.CreateTruncating(value.Y)) { }

    public static int Dimension => 2;
    public static Vec2<TReal> Zero => new(TReal.Zero, TReal.Zero);
    public static Vec2<TReal> One => new(TReal.One, TReal.One);
    public static Vec2<TReal> UnitX => new(TReal.One, TReal.Zero);
    public static Vec2<TReal> UnitY => new(TReal.Zero, TReal.One);
    public static Vec2<TReal> FromCoordinates(ReadOnlySpan<TReal> coordinates) => new(coordinates[0], coordinates[1]);

    public TReal this[int index] => index switch
    {
        0 => X,
        1 => Y,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    // Keep chained arithmetic in coordinates. Bit-casting between scalar fields and
    // Vector2 at each operator makes the JIT spill intermediate results to the stack.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2<TReal> operator +(Vec2<TReal> left, Vec2<TReal> right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Vec2<TReal> operator -(Vec2<TReal> left, Vec2<TReal> right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static Vec2<TReal> operator -(Vec2<TReal> value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2<TReal> operator *(Vec2<TReal> value, TReal scale) =>
        new(value.X * scale, value.Y * scale);

    public static Vec2<TReal> operator *(TReal scale, Vec2<TReal> value) => value * scale;

    /// <summary>Multiplies the corresponding coordinates.</summary>
    public static Vec2<TReal> operator *(Vec2<TReal> left, Vec2<TReal> right) =>
        new(left.X * right.X, left.Y * right.Y);

    /// <summary>Divides the corresponding coordinates.</summary>
    public static Vec2<TReal> operator /(Vec2<TReal> left, Vec2<TReal> right) =>
        new(left.X / right.X, left.Y / right.Y);

    public static Vec2<TReal> operator /(Vec2<TReal> value, TReal scale)
    {
        if (typeof(TReal) == typeof(double))
        {
            var quotient = Vector128.Create(double.CreateTruncating(value.X), double.CreateTruncating(value.Y))
                / Vector128.Create(double.CreateTruncating(scale));
            return new(TReal.CreateTruncating(quotient.GetElement(0)), TReal.CreateTruncating(quotient.GetElement(1)));
        }

        return new(value.X / scale, value.Y / scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal Dot(Vec2<TReal> left, Vec2<TReal> right) =>
        left.X * right.X + left.Y * right.Y;

    public static Vec2<TReal> Min(Vec2<TReal> left, Vec2<TReal> right) => typeof(TReal) == typeof(float)
        ? FromPacked(Vector2.Min(left.ToVector2(), right.ToVector2()))
        : new(TReal.Min(left.X, right.X), TReal.Min(left.Y, right.Y));

    public static Vec2<TReal> Max(Vec2<TReal> left, Vec2<TReal> right) => typeof(TReal) == typeof(float)
        ? FromPacked(Vector2.Max(left.ToVector2(), right.ToVector2()))
        : new(TReal.Max(left.X, right.X), TReal.Max(left.Y, right.Y));

    public static Vec2<TReal> Abs(Vec2<TReal> value) => typeof(TReal) == typeof(float)
        ? FromPacked(Vector2.Abs(value.ToVector2()))
        : new(TReal.Abs(value.X), TReal.Abs(value.Y));

    public static bool IsFinite(Vec2<TReal> value) => TReal.IsFinite(value.X) && TReal.IsFinite(value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TReal LengthSquared() => IVectorExtensions.LengthSquared<Vec2<TReal>, TReal>(this);
    public TReal Length() => IVectorExtensions.Length<Vec2<TReal>, TReal>(this);
    public static TReal Distance(Vec2<TReal> first, Vec2<TReal> second) =>
        IVectorExtensions.Distance<Vec2<TReal>, TReal>(first, second);
    public static TReal DistanceSquared(Vec2<TReal> first, Vec2<TReal> second) =>
        IVectorExtensions.DistanceSquared<Vec2<TReal>, TReal>(first, second);

    public static Vec2<TReal> Normalize(Vec2<TReal> value) =>
        IVectorExtensions.Normalize<Vec2<TReal>, TReal>(value);

    public static Vec2<TReal> Lerp(Vec2<TReal> first, Vec2<TReal> second, TReal amount) =>
        typeof(TReal) == typeof(float)
            ? FromPacked(Vector2.Lerp(first.ToVector2(), second.ToVector2(), float.CreateTruncating(amount)))
            : IVectorExtensions.Lerp(first, second, amount);

    public static Vec2<TReal> Clamp(Vec2<TReal> value, Vec2<TReal> minimum, Vec2<TReal> maximum) =>
        IVectorExtensions.Clamp<Vec2<TReal>, TReal>(value, minimum, maximum);

    /// <summary>Unit vector at an angle in radians, counter-clockwise from +X in Y-up space.</summary>
    public static Vec2<TReal> FromAngle(TReal angleRadians)
    {
        if (typeof(TReal) == typeof(float))
            Validation.Ensure.Finite(angleRadians);

        var (sin, cos) = TReal.SinCos(angleRadians);
        return new(cos, sin);
    }

    /// <summary>The perpendicular dot product, positive for a counter-clockwise turn in Y-up space.</summary>
    public TReal Cross(Vec2<TReal> other) => X * other.Y - Y * other.X;

    /// <summary>Twice the signed triangle area, positive for a counter-clockwise turn in Y-up space.</summary>
    public TReal Orientation(Vec2<TReal> b, Vec2<TReal> c) => (b - this).Cross(c - this);

    [JsonIgnore]
    public Vec2<TReal> PerpCcw => new(-Y, X);

    [JsonIgnore]
    public Vec2<TReal> PerpCw => new(Y, -X);

    /// <summary>Transforms a point using Matrix3x2's row-vector convention and this vector's precision.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2<TReal> Transform(Vec2<TReal> point, Matrix3x2 matrix)
    {
        if (typeof(TReal) == typeof(float))
            return FromPacked(Vector2.Transform(point.ToVector2(), matrix));

        return new(
            point.X * TReal.CreateTruncating(matrix.M11) + point.Y * TReal.CreateTruncating(matrix.M21) + TReal.CreateTruncating(matrix.M31),
            point.X * TReal.CreateTruncating(matrix.M12) + point.Y * TReal.CreateTruncating(matrix.M22) + TReal.CreateTruncating(matrix.M32));
    }

    /// <summary>Applies the linear part of the transform, omitting translation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2<TReal> TransformNormal(Vec2<TReal> direction, Matrix3x2 matrix)
    {
        if (typeof(TReal) == typeof(float))
            return FromPacked(Vector2.TransformNormal(direction.ToVector2(), matrix));
        return new(
            direction.X * TReal.CreateTruncating(matrix.M11) + direction.Y * TReal.CreateTruncating(matrix.M21),
            direction.X * TReal.CreateTruncating(matrix.M12) + direction.Y * TReal.CreateTruncating(matrix.M22));
    }

    /// <summary>Rounds each coordinate to a float. Values outside the float range become infinities.</summary>
    public Vector2 ToVector2() => typeof(TReal) == typeof(float)
        ? Unsafe.BitCast<Vec2<TReal>, Vector2>(this)
        : new(float.CreateTruncating(X), float.CreateTruncating(Y));

    public Vec2<double> ToVector2d() => new(double.CreateTruncating(X), double.CreateTruncating(Y));
    public Vec2<float> ToVector2f() => new(float.CreateTruncating(X), float.CreateTruncating(Y));
    public override string ToString() => typeof(TReal) == typeof(float) ? ToVector2().ToString() : $"<{X}, {Y}>";

    // Only called from the float paths above, where the sequential layouts agree.
    private static Vec2<TReal> FromPacked(Vector2 value) => Unsafe.BitCast<Vector2, Vec2<TReal>>(value);
}
