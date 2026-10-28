using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;

namespace App2d.Core.Characters;

/// <summary>Closed-vocabulary checks shared by the authored validators.</summary>
public static class EntityVocabulary
{
    public static void Require(string? value, IReadOnlyList<string> allowed, string field)
    {
        if (value is null || !allowed.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{field} must be one of {string.Join(", ", allowed)}; found '{value}'.");
        }
    }
}

/// <summary>An authored value's accepted range. The soft range is the editor's suggested slider span; files may hold any value within the hard range.</summary>
public readonly record struct Limit(float Min, float Max, float SoftMin, float SoftMax) : IInterval<float>
{
    public Limit(float min, float max) : this(min, max, min, max) { }
    public Limit Soft(float min, float max) => this with { SoftMin = min, SoftMax = max };
    public float Clamp(float value) => IntervalMath<float>.Clamp(this, value);
    public void Check(float value, string field)
    {
        if (!float.IsFinite(value) || value < Min || value > Max)
        {
            throw new InvalidDataException($"{field} must be between {Min} and {Max}; found {value}.");
        }
    }
    public static void Color(string? value, string field)
    {
        if (!ColorExtensions.TryParseRgb(value.AsSpan(), out _))
        {
            throw new InvalidDataException($"{field} must be a #rrggbb color; found '{value}'.");
        }
    }
}
