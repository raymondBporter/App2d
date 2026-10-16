using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Cubic time/value handles. Endpoints belong to the neighboring keys; value handles may overshoot.</summary>
public sealed record KeyCurve2D
{
    public Vector2 Control1 { get; set; }
    public Vector2 Control2 { get; set; } = Vector2.One;
    /// <summary>When true, handle Y coordinates are channel values; otherwise they are fractions of the endpoint difference.</summary>
    public bool Absolute { get; set; }

    public void Validate()
    {
        if (!NumericValidation.IsFinite(Control1) || !NumericValidation.IsFinite(Control2) ||
            !NumericValidation.IsInRange(Control1.X, 0f, 1f) || !NumericValidation.IsInRange(Control2.X, 0f, 1f))
            throw new InvalidDataException("Invalid keyframe Bezier curve.");
    }

    /// <summary>Creates the continuous cubic for a named ease; held steps have no continuous equivalent.</summary>
    public static KeyCurve2D FromEase(string ease) => ease switch
    {
        ClipEase.Linear => new() { Control1 = new(1f / 3, 1f / 3), Control2 = new(2f / 3, 2f / 3) },
        ClipEase.Smooth => new() { Control1 = new(1f / 3, 0), Control2 = new(2f / 3, 1) },
        _ => throw ArgGuard.CreateInvalid("The ease must have a continuous cubic equivalent.", nameof(ease))
    };

    public float Apply(float time) => Evaluate(time, 0, 1);

    public float Evaluate(float time, float start, float end)
    {
        if (time <= 0) return start;
        if (time >= 1) return end;
        var curve = ToCurve(start, end);
        return curve.Evaluate(curve.ParameterAtX(time)).Y;
    }

    /// <summary>The shared geometry curve in normalized time and channel-value coordinates.</summary>
    public CubicBezier2D ToCurve(float start, float end)
    {
        var (first, second) = Controls(start, end);
        return new(new(0, start), new(Control1.X, first), new(Control2.X, second), new(1, end));
    }

    public (float First, float Second) Controls(float start, float end) => Absolute ? (Control1.Y, Control2.Y)
        : (Interpolation.Lerp(start, end, Control1.Y), Interpolation.Lerp(start, end, Control2.Y));

    /// <summary>Converts value controls without changing the curve. Equal endpoints cannot normalize an overshoot.</summary>
    public bool TryConvertValueControls(bool absolute, float start, float end, out KeyCurve2D converted)
    {
        var (first, second) = Controls(start, end);
        converted = this;
        if (!absolute && start == end && (first != start || second != start)) return false;
        var values = absolute ? new Vector2(first, second) : start == end ? new Vector2(0, 1)
            : (new Vector2(first, second) - new Vector2(start)) / (end - start);
        converted = this with { Absolute = absolute, Control1 = Control1 with { Y = values.X }, Control2 = Control2 with { Y = values.Y } };
        return true;
    }

    /// <summary>Splits at normalized time, rather than at the Bezier parameter. Each piece uses its sampled endpoint values.</summary>
    public (KeyCurve2D Left, KeyCurve2D Right) Split(float time, float start, float end)
    {
        ArgGuard.InRange(time, 0f, 1f);
        var curve = ToCurve(start, end);
        var (left, right) = curve.Split(curve.ParameterAtX(time));
        return (FromSegment(left, 0, time), FromSegment(right, time, 1));
    }

    /// <summary>Restricts normalized time to an interval, keeping value handles in channel units.</summary>
    public KeyCurve2D Slice(float from, float to, float start, float end)
    {
        ArgGuard.InRange(from, 0f, 1f);
        ArgGuard.InRange(to, from, 1f);
        var curve = ToCurve(start, end);
        return FromSegment(curve.Slice(curve.ParameterAtX(from), curve.ParameterAtX(to)), from, to);
    }

    private static KeyCurve2D FromSegment(CubicBezier2D segment, float from, float to) => new()
    {
        Control1 = new(from == to ? 0 : Clamping.Clamp01((segment.Control1.X - from) / (to - from)), segment.Control1.Y),
        Control2 = new(from == to ? 1 : Clamping.Clamp01((segment.Control2.X - from) / (to - from)), segment.Control2.Y),
        Absolute = true
    };
}
