using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Audio;

/// <summary>Smooth distance attenuation, measured in world units.</summary>
public sealed class SoundFalloff2D
{
    public static SoundFalloff2D Default { get; } = new(96f, 800f);
    public static SoundFalloff2D HammerImpact { get; } = new(128f, 1200f);

    public SoundFalloff2D(float innerRadius, float outerRadius)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(innerRadius);
        ArgGuard.ThrowIfNotPositive(outerRadius);
        if (outerRadius <= innerRadius)
            throw new ArgumentOutOfRangeException(nameof(outerRadius), "Outer radius must exceed inner radius.");
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
    }

    public float InnerRadius { get; }
    public float OuterRadius { get; }

    public float Evaluate(Vector2 source, Vector2 listener)
    {
        ArgGuard.ThrowIfNotFinite(source);
        ArgGuard.ThrowIfNotFinite(listener);
        var distance = Vector2.Distance(source, listener);
        var t = Math.Clamp((distance - InnerRadius) / (OuterRadius - InnerRadius), 0f, 1f);
        return 1f - t * t * (3f - 2f * t);
    }
}
