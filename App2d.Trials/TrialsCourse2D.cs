using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.HeightFields;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Trials;

/// <summary>Editable course generation settings. Width extends the course; waves and ramps retain their world-space scale.</summary>
public sealed record TrialsCourse2D
{
    public static TrialsCourse2D Default { get; } = new();

    /// <summary>Total world-space width, including the starting area to the left of the bike.</summary>
    public float Width { get; init; } = 42_800f;
    public float XMin { get; init; } = -1600f;
    public float XMax => XMin + Width;
    public float YMin { get; init; } = -1800f;
    public float ControlPointSpacing { get; init; } = 160f;
    public float WaveStartX { get; init; } = 600f;
    public float WaveFadeInLength { get; init; } = 480f;
    public float WaveFadeOutLength { get; init; } = 640f;

    /// <summary>Frequency is cycles per world unit (1 / wavelength); amplitude is vertical world units.</summary>
    public IReadOnlyList<(float Frequency, float Amplitude)> FrequencyAmplitudes { get; init; } =
        [(1f / 2400f, 48f), (1f / 900f, 22f), (1f / 380f, 10f)];

    /// <summary>Additive ramp profiles in world X/Y coordinates. Each contributes zero outside its endpoints.</summary>
    public IReadOnlyList<PolylineCurve2D> Ramps { get; init; } =
    [
        PolylineCurve2D.Builder.Create([new(1800f, 0f), new(1960f, 20f), new(2120f, 70f), new(2280f, 125f), new(2440f, 130f), new(2600f, 0f)]),
        PolylineCurve2D.Builder.Create([new(4680f, 0f), new(4840f, 50f), new(5000f, 110f), new(5160f, 160f), new(5320f, 160f), new(5480f, -40f), new(5640f, 0f)])
    ];

    /// <summary>Creates a cubic B-spline profile, always including XMin and XMax, even for widths not divisible by the spacing.</summary>
    public BSpline2D CreateCurve()
    {
        ArgGuard.Positive(Width);
        ArgGuard.Finite(XMin);
        ArgGuard.Finite(XMax);
        ArgGuard.ThrowIf(XMax <= XMin, "Course width must produce distinct endpoints.", nameof(Width));
        ArgGuard.Finite(YMin);
        ArgGuard.Positive(ControlPointSpacing);
        ArgGuard.Finite(WaveStartX);
        ArgGuard.Positive(WaveFadeInLength);
        ArgGuard.Positive(WaveFadeOutLength);
        ArgGuard.ThrowIfNull(FrequencyAmplitudes);
        ArgGuard.ThrowIfNull(Ramps);
        var waves = FrequencyAmplitudes.ToArray();
        foreach (var (frequency, amplitude) in waves)
        {
            ArgGuard.NonNegative(frequency);
            ArgGuard.Finite(amplitude);
        }
        var ramps = Ramps.Select(curve => new PolylineHeightField2D(curve, float.NegativeInfinity)).ToArray();
        // Keep the grid anchored to XMin as width changes. Only the final span may be shorter.
        // Small courses still need four control points for a cubic spline.
        var spacing = Math.Min(ControlPointSpacing, Width / 3d);
        var segments = Math.Max(3, checked((int)Math.Ceiling(Width / spacing)));
        var controls = new List<Vector2>(checked(segments + 1));
        for (var index = 0; index <= segments; index++)
        {
            var x = index == segments ? XMax : (float)(XMin + index * spacing);
            ArgGuard.ThrowIf(index > 0 && x <= controls[^1].X, "Control point spacing is too small at these course coordinates.", nameof(ControlPointSpacing));
            var envelope = SmoothStep((x - WaveStartX) / WaveFadeInLength) * SmoothStep((XMax - x) / WaveFadeOutLength);
            var height = 0d;
            foreach (var (frequency, amplitude) in waves)
                height += amplitude * Math.Sin(Math.Tau * frequency * ((double)x - WaveStartX));
            height *= envelope;
            foreach (var ramp in ramps)
                if (x >= ramp.XMin && x <= ramp.XMax) height += ramp.HeightAt(x);
            controls.Add(new(x, ArgGuard.Finite((float)height)));
        }
        return BSpline2D.Builder.Create(controls, degree: 3);
    }

    private static float SmoothStep(float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return amount * amount * (3f - 2f * amount);
    }
}
