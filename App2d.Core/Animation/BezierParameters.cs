using App2d.Core.Validation;
using App2d.Core.Mathematics.Progress;
using System.Text.Json.Serialization;

namespace App2d.Core.Animation;

/// <summary>Saved arguments for a Bezier map. The JSON names retain compatibility with existing keyframe assets.</summary>
public sealed record BezierParameters
{
    [JsonPropertyName("x1")]
    public float FirstControlTime { get; set; }
    [JsonPropertyName("y1")]
    public float FirstControlValue { get; set; }
    [JsonPropertyName("x2")]
    public float SecondControlTime { get; set; } = 1f;
    [JsonPropertyName("y2")]
    public float SecondControlValue { get; set; } = 1f;
    public bool Absolute { get; set; }

    public void Validate()
    {
        Ensure.InRange(FirstControlTime, 0f, 1f);
        Ensure.InRange(SecondControlTime, 0f, 1f);
        Ensure.Finite(FirstControlValue);
        Ensure.Finite(SecondControlValue);
    }
}

/// <summary>Translates saved control arguments into reusable core interpolation rules.</summary>
internal static class AuthoredInterpolation
{
    public static Interpolator<float, float>? CreateOverride(EasingCurve ease, EasingDirection direction, BezierParameters? parameters) =>
        parameters is null || ease == EasingCurve.Step && direction == EasingDirection.In ? null
            : Interpolation.Bezier(parameters.FirstControlTime, parameters.FirstControlValue,
                parameters.SecondControlTime, parameters.SecondControlValue, parameters.Absolute);
}
