namespace App2d.Core.Animation;

/// <summary>
/// Evaluates values and independent scalar channels through progress maps.
/// Saved Bezier parameters are factory arguments, not a second curve API.
/// Exact key selection and held segments belong to KeyframeSampling.
/// </summary>
internal readonly struct KeyframeSegmentSampler(float phase, ProgressMapf progressMap)
{
    private readonly float _progress = progressMap.Sample(phase);

    public TValue Interpolate<TValue>(TValue start, TValue end) where TValue : IInterpolatable<TValue, float> =>
        Interpolation.Lerp(start, end, _progress);

    public Vector3f Interpolate(Vector3f start, Vector3f end,
        BezierParameters? x = null, BezierParameters? y = null, BezierParameters? z = null) =>
        x is null && y is null && z is null
            ? Interpolate<Vector3f>(start, end)
            : new(Interpolate(start.X, end.X, x), Interpolate(start.Y, end.Y, y), Interpolate(start.Z, end.Z, z));

    public Vector4f Interpolate(Vector4f start, Vector4f end,
        BezierParameters? x = null, BezierParameters? y = null, BezierParameters? z = null, BezierParameters? w = null) =>
        x is null && y is null && z is null && w is null
            ? Interpolate<Vector4f>(start, end)
            : new(Interpolate(start.X, end.X, x), Interpolate(start.Y, end.Y, y),
                Interpolate(start.Z, end.Z, z), Interpolate(start.W, end.W, w));

    /// <summary>
    /// Uses named easing or creates a value map from saved Bezier arguments. The timeline holds its endpoints;
    /// values and intentional overshoot inside a segment are not clamped.
    /// </summary>
    public float Interpolate(float start, float end, BezierParameters? parameters = null)
    {
        if (parameters is null) return Interpolation.Lerp(start, end, _progress);
        if (phase <= 0) return start;
        if (phase >= 1) return end;
        var map = parameters.Absolute
            ? Easingf.TimedBezier(start, parameters.FirstControlTime, parameters.FirstControlValue,
                parameters.SecondControlTime, parameters.SecondControlValue, end)
            : Easingf.TimedBezier(parameters.FirstControlTime, parameters.FirstControlValue,
                parameters.SecondControlTime, parameters.SecondControlValue).ToRange(start, end);
        return map.Sample(phase);
    }
}
