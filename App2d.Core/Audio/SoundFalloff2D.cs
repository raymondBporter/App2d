using App2d.Core.Validation;

namespace App2d.Core.Audio;

/// <summary>Smooth distance attenuation in world units: full volume inside the inner radius, silent beyond the outer.</summary>
public sealed class SoundFalloff2D
{
    public SoundFalloff2D(float innerRadius, float outerRadius)
    {
        Ensure.NonNegative(innerRadius);
        Ensure.GreaterThan(outerRadius, innerRadius);
        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
    }

    public float InnerRadius { get; }
    public float OuterRadius { get; }

    public float Evaluate(Vector2f source, Vector2f listener)
    {
        Ensure.Finite(source);
        Ensure.Finite(listener);
        var distance = Vector2f.Distance(source, listener);
        var t = Realf.Clamp01((distance - InnerRadius) / (OuterRadius - InnerRadius));
        return 1f - Easingf.SmoothStepValue(t);
    }
}
