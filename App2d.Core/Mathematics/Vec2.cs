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

    public Vec2(Vector2f value) : this(TReal.CreateTruncating(value.X), TReal.CreateTruncating(value.Y)) { }

    public Vec2(Vector2 value) : this(TReal.CreateTruncating(value.X), TReal.CreateTruncating(value.Y)) { }

    public static int Dimension => 2;
    public static Vec2<TReal> Zero => new(TReal.Zero, TReal.Zero);
    public static Vec2<TReal> One => new(TReal.One, TReal.One);
    public static Vec2<TReal> UnitX => new(TReal.One, TReal.Zero);
    public static Vec2<TReal> UnitY => new(TReal.Zero, TReal.One);
    public static Vec2<TReal> NegativeInfinity => new(TReal.NegativeInfinity);
    public static Vec2<TReal> PositiveInfinity => new(TReal.PositiveInfinity);
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

    /// <summary>Removes the component parallel to a unit normal.</summary>
    public static Vec2<TReal> Reject(Vec2<TReal> value, Vec2<TReal> unitNormal) =>
        IVectorExtensions.Reject<Vec2<TReal>, TReal>(value, unitNormal);

    /// <summary>
    /// Refracts a unit direction through a unit normal pointing toward the incident medium.
    /// Indices must be finite and positive. Total internal reflection returns false and a zero vector.
    /// </summary>
    public static bool TryRefract(Vec2<TReal> direction, Vec2<TReal> incidentNormal,
        TReal fromIndex, TReal toIndex, out Vec2<TReal> transmitted)
    {
        Validation.Ensure.Positive(fromIndex);
        Validation.Ensure.Positive(toIndex);
        var tangent = Reject(direction, incidentNormal);
        // Preserve the input precision of the rejection, then widen narrower types before scaling.
        if (typeof(TReal) == typeof(float) || typeof(TReal) == typeof(Half))
        {
            var refracted = Vec2<double>.TryRefractTangent(tangent.ToVector2d(), incidentNormal.ToVector2d(),
                double.CreateTruncating(fromIndex), double.CreateTruncating(toIndex), out var wide);
            transmitted = new(TReal.CreateTruncating(wide.X), TReal.CreateTruncating(wide.Y));
            return refracted;
        }

        return TryRefractTangent(tangent, incidentNormal, fromIndex, toIndex, out transmitted);
    }

    internal static bool TryRefractTangent(Vec2<TReal> tangent, Vec2<TReal> normal,
        TReal fromIndex, TReal toIndex, out Vec2<TReal> transmitted)
    {
        var ratio = fromIndex / toIndex;
        if (TReal.IsPositiveInfinity(ratio))
        {
            // Separate the ratio into a bounded mantissa and a power of two to keep zero and tiny tangents usable.
            var fromExponent = TReal.ILogB(fromIndex);
            var toExponent = TReal.ILogB(toIndex);
            ratio = TReal.ScaleB(fromIndex, -fromExponent) / TReal.ScaleB(toIndex, -toExponent);
            var exponent = fromExponent - toExponent;
            tangent = new(TReal.ScaleB(tangent.X, exponent), TReal.ScaleB(tangent.Y, exponent));
        }
        tangent *= ratio;

        var normalSquared = TReal.One - tangent.LengthSquared();
        if (normalSquared < TReal.Zero)
        {
            transmitted = Zero;
            return false;
        }

        transmitted = tangent - normal * TReal.Sqrt(normalSquared);
        return true;
    }

    /// <summary>Unit vector at an angle in radians, counter-clockwise from +X in Y-up space.</summary>
    public static Vec2<TReal> FromAngle(TReal angleRadians) =>
        typeof(TReal) == typeof(float) ? Direction(angleRadians) : FromAngleUnchecked(angleRadians);

    /// <summary>Returns the unit direction at a finite angle. Zero radians points along +X.</summary>
    public static Vec2<TReal> Direction(TReal angleRadians) => FromAngleUnchecked(Validation.Ensure.Finite(angleRadians));

    private static Vec2<TReal> FromAngleUnchecked(TReal angleRadians)
    {
        var (sin, cos) = TReal.SinCos(angleRadians);
        return new(cos, sin);
    }

    /// <summary>Unit vector at a finite angle in degrees, counter-clockwise from +X in Y-up space.</summary>
    public static Vec2<TReal> FromAngleDegrees(TReal angleDegrees) =>
        Direction(Angles.DegreesToRadians(angleDegrees));

    /// <summary>Vector from a finite, nonnegative radius and a finite angle in radians.</summary>
    public static Vec2<TReal> FromPolar(TReal radius, TReal angleRadians) =>
        Polar<TReal>.ToCartesian(radius, angleRadians);

    public Polar<TReal> ToPolar() => Polar<TReal>.FromCartesian(this);

    /// <summary>Heading in radians from +X, counter-clockwise in Y-up space.</summary>
    [JsonIgnore]
    public TReal AngleRadians => TReal.Atan2(Y, X);

    /// <summary>Rotates counter-clockwise about the origin.</summary>
    public Vec2<TReal> Rotate(TReal radians)
    {
        if (radians == TReal.Zero)
            return this;

        var (sin, cos) = TReal.SinCos(radians);
        return new(X * cos - Y * sin, X * sin + Y * cos);
    }

    /// <summary>Rotates counter-clockwise about a pivot.</summary>
    public Vec2<TReal> Rotate(TReal radians, Vec2<TReal> pivot) => pivot + (this - pivot).Rotate(radians);

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
    public Vector2f ToVector2f() => new(ToVector2());
    public override string ToString() => typeof(TReal) == typeof(float) ? ToVector2().ToString() : $"<{X}, {Y}>";

    // Only called from the float paths above, where the sequential layouts agree.
    private static Vec2<TReal> FromPacked(Vector2 value) => Unsafe.BitCast<Vector2, Vec2<TReal>>(value);
}
