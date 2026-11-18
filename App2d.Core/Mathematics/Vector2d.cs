using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>Two double-precision coordinates. Shared 2D behavior comes from the self-typed vector helpers.</summary>
[StructLayout(LayoutKind.Sequential)]
[method: JsonConstructor]
public readonly record struct Vector2d(double X, double Y) : IVector2<Vector2d, double>
{
    public Vector2d(double value) : this(value, value) { }
    public Vector2d(Vector2f value) : this(value.X, value.Y) { }
    public Vector2d(Vector2 value) : this(value.X, value.Y) { }

    public static int Dimension => 2;
    public static Vector2d Create(double x, double y) => new(x, y);
    public static Vector2d FromCoordinates(ReadOnlySpan<double> coordinates) => new(coordinates[0], coordinates[1]);
    public static Vector2d Zero => default;
    public static Vector2d One => new(1);
    public static Vector2d UnitX => new(1, 0);
    public static Vector2d UnitY => new(0, 1);
    public static Vector2d NegativeInfinity => new(double.NegativeInfinity);
    public static Vector2d PositiveInfinity => new(double.PositiveInfinity);
    public double this[int index] => index switch { 0 => X, 1 => Y, _ => throw new ArgumentOutOfRangeException(nameof(index)) };

    public static Vector2d operator +(Vector2d a, Vector2d b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2d operator -(Vector2d a, Vector2d b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2d operator -(Vector2d value) => new(-value.X, -value.Y);
    public static Vector2d operator *(Vector2d value, double scale) => new(value.X * scale, value.Y * scale);
    public static Vector2d operator *(double scale, Vector2d value) => value * scale;
    public static Vector2d operator *(Vector2d a, Vector2d b) => new(a.X * b.X, a.Y * b.Y);
    public static Vector2d operator /(Vector2d a, Vector2d b) => new(a.X / b.X, a.Y / b.Y);
    public static Vector2d operator /(Vector2d value, double scale)
    {
        var result = Vector128.Create(value.X, value.Y) / Vector128.Create(scale);
        return new(result.GetElement(0), result.GetElement(1));
    }

    public static double Dot(Vector2d a, Vector2d b) => a.X * b.X + a.Y * b.Y;
    public static Vector2d Min(Vector2d a, Vector2d b) => new(double.Min(a.X, b.X), double.Min(a.Y, b.Y));
    public static Vector2d Max(Vector2d a, Vector2d b) => new(double.Max(a.X, b.X), double.Max(a.Y, b.Y));
    public static Vector2d Abs(Vector2d value) => new(double.Abs(value.X), double.Abs(value.Y));
    public static bool IsFinite(Vector2d value) => double.IsFinite(value.X) && double.IsFinite(value.Y);

    public double Dot(Vector2d other) => Dot(this, other);
    public Vector2d Min(Vector2d other) => Min(this, other);
    public Vector2d Max(Vector2d other) => Max(this, other);
    public Vector2d Abs() => Abs(this);
    public bool IsFinite() => IsFinite(this);
    public double LengthSquared() => IVectorExtensions.LengthSquared<Vector2d, double>(this);
    public double Length() => IVectorExtensions.Length<Vector2d, double>(this);
    public Vector2d Normalized() => IVectorExtensions.Normalize<Vector2d, double>(this);
    public double DistanceSquared(Vector2d other) => IVectorExtensions.DistanceSquared<Vector2d, double>(this, other);
    public double Distance(Vector2d other) => IVectorExtensions.Distance<Vector2d, double>(this, other);
    public Vector2d Lerp(Vector2d other, double amount) => IVectorExtensions.Lerp<Vector2d, double>(this, other, amount);
    public Vector2d Midpoint(Vector2d other) => IVectorExtensions.Midpoint<Vector2d, double>(this, other);
    public Vector2d Project(Vector2d unitNormal) => IVectorExtensions.Project<Vector2d, double>(this, unitNormal);
    public Vector2d Reject(Vector2d unitNormal) => IVectorExtensions.Reject<Vector2d, double>(this, unitNormal);
    public Vector2d Reflect(Vector2d unitNormal) => IVectorExtensions.Reflect<Vector2d, double>(this, unitNormal);
    public Vector2d Clamp(Vector2d minimum, Vector2d maximum) => IVectorExtensions.Clamp<Vector2d, double>(this, minimum, maximum);

    public double Cross(Vector2d other) => Vector2Math<Vector2d, double>.Cross(this, other);
    public double Orientation(Vector2d second, Vector2d third) => Vector2Math<Vector2d, double>.Orientation(this, second, third);
    [JsonIgnore]
    public double AngleRadians => Vector2Math<Vector2d, double>.AngleRadians(this);
    [JsonIgnore]
    public Vector2d PerpCcw => Vector2Math<Vector2d, double>.PerpCcw(this);
    [JsonIgnore]
    public Vector2d PerpCw => Vector2Math<Vector2d, double>.PerpCw(this);
    public Vector2d Rotate(double radians) => Vector2Math<Vector2d, double>.Rotate(this, radians);
    public Vector2d Rotate(double radians, Vector2d pivot) => Vector2Math<Vector2d, double>.Rotate(this, radians, pivot);
    public Polar<double> ToPolar() => Vector2Math<Vector2d, double>.ToPolar(this);
    public Vector2 ToVector2() => Vector2Math<Vector2d, double>.ToVector2(this);
    public Vector2f ToVector2f() => Vector2Math<Vector2d, double>.ToVector2f(this);
    public Vector2d ToVector2d() => this;

    public override string ToString() => $"<{X}, {Y}>";
}
