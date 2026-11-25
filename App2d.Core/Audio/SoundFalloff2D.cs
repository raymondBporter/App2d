using App2d.Core.Validation;

namespace App2d.Core.Audio;

/// <summary>Smooth distance attenuation in world units: full volume inside the inner radius, silent beyond the outer.</summary>
public sealed class SoundFalloff2D
{
    private readonly ProgressMapf _curve;

    public SoundFalloff2D(float innerRadius, float outerRadius)
    {
        Ensure.NonNegative(innerRadius);
        Ensure.GreaterThan(outerRadius, innerRadius);
        _curve = Easingf.Smooth.FromRange(outerRadius, innerRadius);
    }

    public float InnerRadius => _curve.InputEnd;
    public float OuterRadius => _curve.InputBegin;

    public float Evaluate(Vector2f source, Vector2f listener)
    {
        Ensure.Finite(source);
        Ensure.Finite(listener);
        var distance = Vector2f.Distance(source, listener);
        return _curve.EvaluateClamped(distance);
    }
}
