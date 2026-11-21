using App2d.Core.Animation;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Absolute slot tint. Each component has its own outgoing curve; endpoints are in [0,1].</summary>
public sealed record SlotColorKey2D : IEditableTimedKey
{
    public float Time { get; set; }
    public Vector4 Value { get; set; } = Vector4.One;
    public Interpolator<Vector4f, float>? Interpolator { get; set; }
    public EasingCurve Ease { get; set; } = EasingCurve.Linear;
    public EasingDirection EaseDirection { get; set; } = EasingDirection.In;
    public BezierParameters? CurveR { get; set; }
    public BezierParameters? CurveG { get; set; }
    public BezierParameters? CurveB { get; set; }
    public BezierParameters? CurveA { get; set; }
}

/// <summary>RGBA, RGB, or alpha timeline. RGB and alpha can have independent key times and curves.</summary>
public sealed record SlotColorTrack2D
{
    public const string Rgba = "rgba", Rgb = "rgb", Alpha = "alpha";
    public string Slot { get; set; } = "";
    public string Kind { get; set; } = Rgba;
    public bool SetupBeforeFirst { get; set; } = true;
    public List<SlotColorKey2D> Keys { get; set; } = [];

    public static Vector4 Evaluate(MotionClip clip, SkeletonSlot2D slot, float time)
    {
        SkeletonAppearance2D.Color(slot.Color);
        var value = ColorExtensions.FromHexRgba(slot.Color).ToNumericsVector4();
        foreach (var track in clip.Colors.Where(t => t.Slot == slot.Id))
        {
            if (track.Keys.Count == 0 || track.SetupBeforeFirst && time < track.Keys[0].Time)
            {
                continue;
            }

            var sample = Sample(track.Keys, time);
            if (track.Kind is Rgba or Rgb)
            {
                value.X = sample.X;
                value.Y = sample.Y;
                value.Z = sample.Z;
            }
            if (track.Kind is Rgba or Alpha)
            {
                value.W = sample.W;
            }
        }
        return Vector4.Clamp(value, Vector4.Zero, Vector4.One);
    }

    public static Vector4 Sample(List<SlotColorKey2D> keys, float time) =>
        KeyframeSampling.Sample(keys, time, Vector4.One, static key => key.Value, SampleSegment);

    private static Vector4 SampleSegment(SlotColorKey2D a, SlotColorKey2D b, float phase)
    {
        var rule = a.Interpolator ?? Interpolation.Default<Vector4f, float>().WithProgress(Easingf.FromCurve(a.Ease, a.EaseDirection))
            .With(Vector4f.Channels.R, Curve(a.CurveR)).With(Vector4f.Channels.G, Curve(a.CurveG))
            .With(Vector4f.Channels.B, Curve(a.CurveB)).With(Vector4f.Channels.A, Curve(a.CurveA));
        return rule.Evaluate(new Vector4f(a.Value), new Vector4f(b.Value), phase).ToVector4();

        Interpolator<float, float>? Curve(BezierParameters? parameters) =>
            AuthoredInterpolation.CreateOverride(a.Ease, a.EaseDirection, parameters);
    }
}
