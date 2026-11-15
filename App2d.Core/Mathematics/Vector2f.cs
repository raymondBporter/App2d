using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>A two-dimensional float vector backed by <see cref="Vector2"/>.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Vector2f : IVector<Vector2f, float>
{
    private readonly Vector2 _value;

    [JsonConstructor]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2f(float X, float Y) : this(new Vector2(X, Y)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2f(float value) : this(new Vector2(value)) { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2f(Vector2 value) => _value = value;

    public float X { get => _value.X; init => _value.X = value; }
    public float Y { get => _value.Y; init => _value.Y = value; }
    public float this[int index] => _value[index];

    public void Deconstruct(out float x, out float y) => (x, y) = (X, Y);

    public static int Dimension => 2;
    public static Vector2f Zero => default;
    public static Vector2f One => new(Vector2.One);
    public static Vector2f UnitX => new(Vector2.UnitX);
    public static Vector2f UnitY => new(Vector2.UnitY);
    public static Vector2f NegativeInfinity => new(float.NegativeInfinity);
    public static Vector2f PositiveInfinity => new(float.PositiveInfinity);
    public static Vector2f FromCoordinates(ReadOnlySpan<float> coordinates) => new(coordinates[0], coordinates[1]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator +(Vector2f left, Vector2f right) => new(left._value + right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator -(Vector2f left, Vector2f right) => new(left._value - right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator -(Vector2f value) => new(-value._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(Vector2f value, float scale) => new(value._value * scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(float scale, Vector2f value) => value * scale;

    /// <summary>Multiplies the corresponding coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(Vector2f left, Vector2f right) => new(left._value * right._value);

    /// <summary>Divides the corresponding coordinates.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator /(Vector2f left, Vector2f right) => new(left._value / right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator /(Vector2f value, float scale) => new(value._value / scale);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2f left, Vector2f right) => Vector2.Dot(left._value, right._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Dot(Vector2f other) => Vector2.Dot(_value, other._value);

    public static Vector2f Min(Vector2f left, Vector2f right) => new(Vector2.Min(left._value, right._value));
    public static Vector2f Max(Vector2f left, Vector2f right) => new(Vector2.Max(left._value, right._value));
    public static Vector2f Abs(Vector2f value) => new(Vector2.Abs(value._value));
    public static bool IsFinite(Vector2f value) => float.IsFinite(value.X) && float.IsFinite(value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float LengthSquared() => _value.LengthSquared();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Length() => _value.Length();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2f first, Vector2f second) => Vector2.Distance(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector2f first, Vector2f second) => Vector2.DistanceSquared(first._value, second._value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Normalize(Vector2f value) => new(Vector2.Normalize(value._value));

    public Vector2f Normalized() => Normalize(this);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Lerp(Vector2f first, Vector2f second, float amount) =>
        new(Vector2.Lerp(first._value, second._value, amount));

    public static Vector2f Clamp(Vector2f value, Vector2f minimum, Vector2f maximum) => Min(Max(value, minimum), maximum);

    /// <summary>Reflects a vector across a plane with a unit normal.</summary>
    public static Vector2f Reflect(Vector2f value, Vector2f unitNormal) => new(Vector2.Reflect(value._value, unitNormal._value));

    public static Vector2f SquareRoot(Vector2f value) => new(Vector2.SquareRoot(value._value));

    /// <summary>Removes the component parallel to a unit normal.</summary>
    public static Vector2f Reject(Vector2f value, Vector2f unitNormal) => value - unitNormal * Dot(value, unitNormal);

    /// <summary>
    /// Refracts a unit direction through a unit normal pointing toward the incident medium.
    /// Indices must be finite and positive. Total internal reflection returns false and a zero vector.
    /// </summary>
    public static bool TryRefract(Vector2f direction, Vector2f incidentNormal,
        float fromIndex, float toIndex, out Vector2f transmitted)
    {
        Ensure.Positive(fromIndex);
        Ensure.Positive(toIndex);
        var tangent = Reject(direction, incidentNormal);
        var refracted = Vector2d.TryRefractTangent(tangent.ToVector2d(), incidentNormal.ToVector2d(), fromIndex, toIndex, out var wide);
        transmitted = wide.ToVector2f();
        return refracted;
    }

    /// <summary>Returns the unit direction at a finite angle. Zero radians points along +X.</summary>
    public static Vector2f Direction(float angleRadians)
    {
        Ensure.Finite(angleRadians);
        var (sin, cos) = float.SinCos(angleRadians);
        return new(cos, sin);
    }

    /// <summary>Unit vector at a finite angle in radians, counter-clockwise from +X in Y-up space.</summary>
    public static Vector2f FromAngle(float angleRadians) => Direction(angleRadians);

    /// <summary>Unit vector at a finite angle in degrees, counter-clockwise from +X in Y-up space.</summary>
    public static Vector2f FromAngleDegrees(float angleDegrees) => Direction(Angles.DegreesToRadians(angleDegrees));

    /// <summary>Vector from a finite, nonnegative radius and a finite angle in radians.</summary>
    public static Vector2f FromPolar(float radius, float angleRadians)
    {
        var polar = new Polar<float>(radius, angleRadians);
        return Direction(polar.AngleRadians) * polar.Radius;
    }

    public Polar<float> ToPolar() => Polar<float>.FromCartesian(new Vec2<float>(this));

    /// <summary>Heading in radians from +X, counter-clockwise in Y-up space.</summary>
    [JsonIgnore]
    public float AngleRadians => float.Atan2(Y, X);

    /// <summary>Rotates counter-clockwise about the origin.</summary>
    public Vector2f Rotate(float radians)
    {
        if (radians == 0f)
            return this;

        var (sin, cos) = float.SinCos(radians);
        return new(X * cos - Y * sin, X * sin + Y * cos);
    }

    /// <summary>Rotates counter-clockwise about a pivot.</summary>
    public Vector2f Rotate(float radians, Vector2f pivot) => pivot + (this - pivot).Rotate(radians);

    /// <summary>The perpendicular dot product, positive for a counter-clockwise turn in Y-up space.</summary>
    public float Cross(Vector2f other) => X * other.Y - Y * other.X;

    /// <summary>Twice the signed triangle area, positive for a counter-clockwise turn in Y-up space.</summary>
    public float Orientation(Vector2f b, Vector2f c) => (b - this).Cross(c - this);

    [JsonIgnore]
    public Vector2f PerpCcw => new(-Y, X);

    [JsonIgnore]
    public Vector2f PerpCw => new(Y, -X);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Transform(Vector2f point, Matrix3x2 matrix) => new(Vector2.Transform(point._value, matrix));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f TransformNormal(Vector2f direction, Matrix3x2 matrix) => new(Vector2.TransformNormal(direction._value, matrix));

    public static Vector2f Transform(Vector2f point, Matrix4x4 matrix) => new(Vector2.Transform(point._value, matrix));
    public static Vector2f TransformNormal(Vector2f direction, Matrix4x4 matrix) => new(Vector2.TransformNormal(direction._value, matrix));
    public static Vector2f Transform(Vector2f value, Quaternion rotation) => new(Vector2.Transform(value._value, rotation));

    public void CopyTo(Span<float> destination) => _value.CopyTo(destination);
    public bool TryCopyTo(Span<float> destination) => _value.TryCopyTo(destination);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2 ToVector2() => _value;

    public Vector2f ToVector2f() => this;
    public Vector2d ToVector2d() => new(X, Y);
    public override string ToString() => _value.ToString();
}
