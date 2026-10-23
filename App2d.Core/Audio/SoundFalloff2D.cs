using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Audio;

/// <summary>Smooth distance attenuation in world units: full volume inside the inner radius, silent beyond the outer.</summary>
public sealed class SoundFalloff2D
{
    public SoundFalloff2D(float innerRadius, float outerRadius)
    {
        ArgGuard.NonNegative(innerRadius);
        ArgGuard.GreaterThan(outerRadius, innerRadius);
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
    }

    public float InnerRadius { get; }
    public float OuterRadius { get; }

    public float Evaluate(Vector2 source, Vector2 listener)
    {
        ArgGuard.Finite(source);
        ArgGuard.Finite(listener);
        var distance = Vector2.Distance(source, listener);
        var t = Math.Clamp((distance - InnerRadius) / (OuterRadius - InnerRadius), 0f, 1f);
        return 1f - Progress.SmoothStepUnclamped(t);
    }
}
