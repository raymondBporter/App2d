using App2d.Core.Geometry.Curves;

namespace App2d.Core.Characters.Authored;

/// <summary>Cubic curve with normalized time controls. Value controls may overshoot, including between equal endpoints.</summary>
public sealed record KeyCurve2D
{
    public float X1 { get; set; }
    public float Y1 { get; set; }
    public float X2 { get; set; } = 1;
    public float Y2 { get; set; } = 1;
    /// <summary>When true, Y1/Y2 are channel values; otherwise they are fractions of the endpoint difference.</summary>
    public bool Absolute
    {
        get; set;
    }

    public void Validate()
    {
        if (!float.IsFinite(X1) || !float.IsFinite(X2) || X1 < 0 || X1 > 1 || X2 < 0 || X2 > 1 ||
            !float.IsFinite(Y1) || !float.IsFinite(Y2))
        {
            throw new InvalidDataException("Invalid keyframe Bezier curve.");
        }
    }
    public float Apply(float time) => Evaluate(time, 0, 1);

    public float Evaluate(float time, float start, float end)
    {
        if (time <= 0)
        {
            return start;
        }
        if (time >= 1)
        {
            return end;
        }
        var curve = ToCurve(start, end);
        return curve.Evaluate(curve.ParameterAtX(time)).Y;
    }

    /// <summary>The shared geometry curve in normalized time and channel-value coordinates.</summary>
    public CubicBezier2D ToCurve(float start, float end)
    {
        var (first, second) = Controls(start, end);
        return new(new(0, start), new(X1, first), new(X2, second), new(1, end));
    }

    public (float First, float Second) Controls(float start, float end) => Absolute ? (Y1, Y2) : (start + Y1 * (end - start), start + Y2 * (end - start));

    /// <summary>Converts value controls without changing the curve. Equal endpoints cannot normalize an overshoot.</summary>
    public bool TryConvertValueControls(bool absolute, float start, float end, out KeyCurve2D converted)
    {
        var (first, second) = Controls(start, end);
        converted = this;
        if (absolute)
        {
            converted = this with
            {
                Absolute = true,
                Y1 = first,
                Y2 = second
            };
        }
        else if (start != end)
        {
            converted = this with
            {
                Absolute = false,
                Y1 = (first - start) / (end - start),
                Y2 = (second - start) / (end - start)
            };
        }
        else if (first == start && second == start)
        {
            converted = this with
            {
                Absolute = false,
                Y1 = 0,
                Y2 = 1
            };
        }
        else
        {
            return false;
        }

        return true;
    }

    /// <summary>The same curve restricted to a time interval, with time controls normalized to the new segment.</summary>
    public KeyCurve2D Slice(float from, float to, float start, float end)
    {
        if (!(from >= 0 && to <= 1 && to > from))
        {
            throw new ArgumentOutOfRangeException(nameof(to));
        }

        var curve = ToCurve(start, end);
        var segment = curve.Slice(curve.ParameterAtX(from), curve.ParameterAtX(to));
        return new()
        {
            X1 = Math.Clamp((segment.Control1.X - from) / (to - from), 0, 1),
            Y1 = segment.Control1.Y,
            X2 = Math.Clamp((segment.Control2.X - from) / (to - from), 0, 1),
            Y2 = segment.Control2.Y,
            Absolute = true
        };
    }
}
