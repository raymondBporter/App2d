using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Mathematics;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Cubic-curve controls shared by motion and tint inspectors; authoring code owns curve conversion.</summary>
internal static class AnimationCurveEditor
{
    public static void DrawChannels(EditorSession session, AssetDocument<MotionClip> document, IReadOnlyCollection<Channel> channels, float time)
    {
        foreach (var channel in channels)
        {
            var track = ClipAuthoring.Track(document.Asset, channel);
            if (track is null)
            {
                continue;
            }

            var index = track.Keys.FindIndex(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
            if (index < 0 || index + 1 >= track.Keys.Count)
            {
                continue;
            }

            var key = track.Keys[index];
            var next = track.Keys[index + 1];
            var count = channel.Kind == MotionClip.RotateKind ? 1 : channel.Kind is MotionClip.ScaleKind or MotionClip.ShearKind ? 2 : 3;
            for (var axis = 0; axis < count; axis++)
            {
                var component = axis;
                var curve = axis == 0 ? key.Curve : axis == 1 ? key.CurveY : key.CurveZ;
                float Value(ClipKey k) => channel.Kind == MotionClip.RotateKind ? k.Angle : component == 0 ? k.Value.X : component == 1 ? k.Value.Y : k.Value.Z;
                Draw($"{channel.Kind} {(channel.Kind == MotionClip.RotateKind ? "angle" : "XYZ"[axis])} curve",
                    curve, key.Ease, Value(key), Value(next), changed =>
                        session.Change(document, () => ClipAuthoring.SetCurve(document.Asset, channel, time, component, changed)));
            }
        }
    }

    public static void Draw(string label, KeyCurve2D? curve, string ease, float start, float end, Action<KeyCurve2D?> set)
    {
        if (!ImGui.TreeNode(label + (curve is null ? "" : " (cubic)")))
        {
            return;
        }

        if (curve is null)
        {
            if (ImGui.SmallButton("Add cubic curve"))
            {
                set(KeyCurve2D.FromEase(ease == ClipEase.Step ? ClipEase.Linear : ease));
            }
        }
        else
        {
            var absolute = curve.Absolute;
            if (ImGui.Checkbox("Value controls in channel units", ref absolute))
            {
                if (curve.TryConvertValueControls(absolute, start, end, out var converted))
                {
                    set(converted);
                }
            }
            var x = new Vector2(curve.Control1.X, curve.Control2.X);
            if (Ui.Drag2("Time controls (0–1)", ref x, .005f, 0, 1))
            {
                set(curve with
                {
                    Control1 = curve.Control1 with { X = Clamping.Clamp01(x.X) },
                    Control2 = curve.Control2 with { X = Clamping.Clamp01(x.Y) }
                });
            }

            var y = new Vector2(curve.Control1.Y, curve.Control2.Y);
            if (Ui.Drag2(absolute ? "Value controls" : "Value controls (fractions)", ref y, .005f, 0, 0))
            {
                set(curve with
                {
                    Control1 = curve.Control1 with { Y = y.X },
                    Control2 = curve.Control2 with { Y = y.Y }
                });
            }

            if (ImGui.SmallButton("Remove cubic curve"))
            {
                set(null);
            }

            Ui.Help("Time is a fraction of this segment. Values can overshoot; channel units also allow motion between equal endpoints. Rotation and shear use radians.");
        }
        ImGui.TreePop();
    }
}
