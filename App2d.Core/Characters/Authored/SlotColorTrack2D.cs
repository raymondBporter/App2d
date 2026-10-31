using App2d.Core.Mathematics.Progress;
using App2d.Core.Animation;
using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Absolute slot tint. Each component has its own outgoing curve; endpoints are in [0,1].</summary>
public sealed record SlotColorKey2D : IEditableTimedKey
{
    public float Time { get; set; }
    public Vector4 Value { get; set; } = Vector4.One;
    public ProgressMapKind Ease { get; set; } = ProgressMapKind.Linear;
    public KeyCurve2D? CurveR { get; set; }
    public KeyCurve2D? CurveG { get; set; }
    public KeyCurve2D? CurveB { get; set; }
    public KeyCurve2D? CurveA { get; set; }
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
        KeyframeSampling.Sample(keys, time, Vector4.One, new ColorInterpolation());

    private readonly struct ColorInterpolation : IKeyframeInterpolation<SlotColorKey2D, Vector4>
    {
        public Vector4 GetValue(SlotColorKey2D key) => key.Value;
        public bool IsHeld(SlotColorKey2D key) => key.Ease == ProgressMapKind.Step;

        public Vector4 Interpolate(SlotColorKey2D a, SlotColorKey2D b, float phase)
        {
            var progressMap = ProgressMaps.FromKind(a.Ease);
            var sample = new KeyframeSegmentSampler(phase, progressMap);
            return new(Scalar(a.Value.X, b.Value.X, a.CurveR), Scalar(a.Value.Y, b.Value.Y, a.CurveG),
                Scalar(a.Value.Z, b.Value.Z, a.CurveB), Scalar(a.Value.W, b.Value.W, a.CurveA));

            float Scalar(float start, float end, KeyCurve2D? curve) => sample.Interpolate(start, end, curve?.ToCurve(start, end));
        }
    }
}
