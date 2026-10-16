using App2d.Core.Rendering;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>Absolute slot tint. Each component has its own outgoing curve; endpoints are in [0,1].</summary>
public sealed record SlotColorKey2D
{
    public float Time
    {
        get; set;
    }
    public float R { get; set; } = 1;
    public float G { get; set; } = 1;
    public float B { get; set; } = 1;
    public float A { get; set; } = 1;
    public string Ease { get; set; } = ClipEase.Linear;
    public KeyCurve2D? CurveR
    {
        get; set;
    }
    public KeyCurve2D? CurveG
    {
        get; set;
    }
    public KeyCurve2D? CurveB
    {
        get; set;
    }
    public KeyCurve2D? CurveA
    {
        get; set;
    }
    [JsonIgnore] public Vector4 Value => new(R, G, B, A);
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
        var value = ColorRgba32.ParseRgba(slot.Color).ToVector4();
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

    public static Vector4 Sample(List<SlotColorKey2D> keys, float time)
    {
        if (keys.Count == 0)
        {
            return Vector4.One;
        }

        if (time <= keys[0].Time)
        {
            return keys[0].Value;
        }

        for (var i = 1; i < keys.Count; i++)
        {
            if (time > keys[i].Time)
            {
                continue;
            }

            if (time == keys[i].Time)
            {
                return keys[i].Value;
            }

            var a = keys[i - 1];
            var b = keys[i];
            var phase = (time - a.Time) / (b.Time - a.Time);
            float At(float start, float end, KeyCurve2D? curve) => a.Ease == ClipEase.Step ? start : curve?.Evaluate(phase, start, end)
                ?? start + (end - start) * ClipEase.Apply(a.Ease, phase);
            return new(At(a.R, b.R, a.CurveR), At(a.G, b.G, a.CurveG), At(a.B, b.B, a.CurveB), At(a.A, b.A, a.CurveA));
        }
        return keys[^1].Value;
    }

}
