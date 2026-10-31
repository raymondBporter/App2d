using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Progress;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Animation;

/// <summary>
/// Evaluates scalar channels at one segment phase, sharing up to four Bezier timing solves.
/// Step maps hold the outgoing value. Exact key selection belongs to KeyframeSampling.
/// </summary>
internal struct KeyframeSegmentSampler(float phase, IProgressMap progressMap)
{
    private readonly float _progress = (float)progressMap.Sample(phase);
    private readonly bool _held = progressMap is ProgressMap { Kind: ProgressMapKind.Step };
    private CurveTimingCache _timings;
    private int _count;

    /// <summary>
    /// Interpolates endpoints through the progress map, or evaluates a custom curve whose X coordinates are
    /// normalized segment time and Y coordinates are channel values. A custom curve replaces named easing;
    /// held segments take precedence over either. Values and intentional overshoot are not clamped.
    /// </summary>
    public float Interpolate(float start, float end, CubicBezier2D? curve = null)
    {
        if (_held) return phase < 1f ? start : end;
        if (curve is not { } geometry) return Interpolation.Lerp(start, end, _progress);
        if (phase <= 0) return start;
        if (phase >= 1) return end;

        var handles = new Vector2(geometry.Control1.X, geometry.Control2.X);
        for (var i = 0; i < _count; i++)
        {
            if (_timings[i].Handles == handles)
                return geometry.Evaluate(_timings[i].Parameter).Y;
        }

        // Only X determines the parameter; every channel retains its own Y handles.
        var parameter = geometry.ParameterAtX(phase);
        if (_count < 4) _timings[_count++] = (handles, parameter);
        return geometry.Evaluate(parameter).Y;
    }

    [InlineArray(4)]
    private struct CurveTimingCache
    {
        // Required inline-array backing field; compiler-generated indexing accesses its storage.
        private (Vector2 Handles, float Parameter) _element0;
    }
}
