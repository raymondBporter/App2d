using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>A finite, strictly positive width and height. The default value is invalid.</summary>
public readonly record struct Size2D
{
    [JsonConstructor]
    public Size2D(float width, float height)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(width);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(height);
        Width = width;
        Height = height;
    }

    public float Width { get; }
    public float Height { get; }
    public bool IsValid => float.IsFinite(Width) && Width > 0f && float.IsFinite(Height) && Height > 0f;

    public Vector2 ToVector2() => new(Width, Height);
    public static Size2D FromVector2(Vector2 value) => new(value.X, value.Y);
}
