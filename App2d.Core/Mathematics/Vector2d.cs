using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>
/// Two double-precision coordinates. Only storage, constants and operators are declared here;
/// Dot, Length, Cross, Rotate and the rest are inherited from IVector2 and IVector.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[method: JsonConstructor]
public readonly record struct Vector2d(double X, double Y) : IVector2<Vector2d, double>
{
    public Vector2d(double value) : this(value, value) { }
    public Vector2d(Vector2f value) : this(value.X, value.Y) { }
    public Vector2d(Vector2 value) : this(value.X, value.Y) { }

    public static Vector2d Create(double x, double y) => new(x, y);
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

    public override string ToString() => $"<{X}, {Y}>";
}
