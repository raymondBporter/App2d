using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

internal sealed partial class AnimateView
{
    private string _colorKind = SlotColorTrack2D.Rgba;

    private void CurveFields(AssetDocument<MotionClip> document, IReadOnlyCollection<Channel> channels, float time)
    {
        foreach (var channel in channels)
        {
            var track = ClipAuthoring.Track(document.Asset, channel); if (track is null) continue;
            var index = track.Keys.FindIndex(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
            if (index < 0 || index + 1 >= track.Keys.Count) continue;
            var key = track.Keys[index]; var next = track.Keys[index + 1];
            var count = channel.Kind == MotionClip.RotateKind ? 1 : channel.Kind is MotionClip.ScaleKind or MotionClip.ShearKind ? 2 : 3;
            for (var axis = 0; axis < count; axis++)
            {
                var component = axis;
                var curve = axis == 0 ? key.Curve : axis == 1 ? key.CurveY : key.CurveZ;
                float Value(ClipKey k) => channel.Kind == MotionClip.RotateKind ? k.Angle : component == 0 ? k.X : component == 1 ? k.Y : k.Z;
                CurveEditor(channel.Kind + " " + (channel.Kind == MotionClip.RotateKind ? "angle" : "XYZ"[axis]) + " curve",
                    curve, key.Ease, Value(key), Value(next), changed =>
                        session.Change(document, () => ClipAuthoring.SetCurve(document.Asset, channel, time, component, changed)));
            }
        }
    }

    private void ColorFields(AssetDocument<MotionClip> document, SkeletonSlot2D slot, float time)
    {
        Ui.Header("Animated tint");
        var tracks = document.Asset.Colors.Where(t => t.Slot == slot.Id).ToList();
        string[] kinds = tracks.Any(t => t.Kind == SlotColorTrack2D.Rgba) ? [SlotColorTrack2D.Rgba]
            : tracks.Count > 0 ? [SlotColorTrack2D.Rgb, SlotColorTrack2D.Alpha] : [SlotColorTrack2D.Rgba, SlotColorTrack2D.Rgb, SlotColorTrack2D.Alpha];
        if (!kinds.Contains(_colorKind)) _colorKind = kinds[0];
        if (Ui.Combo("Color channel", _colorKind, kinds) is { } kind) _colorKind = kind;
        var selectedKind = _colorKind;
        var value = SlotColorTrack2D.Evaluate(document.Asset, slot, time);
        if (ImGui.ColorEdit4("Tint at playhead", ref value))
            session.Change(document, () => ClipAuthoring.SetColorKey(document.Asset, slot.Id, selectedKind, time, value));
        if (ImGui.SmallButton("Key tint here")) session.Edit(document, () => ClipAuthoring.SetColorKey(document.Asset, slot.Id, selectedKind, time, value));
        var track = tracks.FirstOrDefault(t => t.Kind == selectedKind);
        if (track is null) return;
        var index = track.Keys.FindIndex(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (index < 0) return;
        var key = track.Keys[index];
        if (ImGui.SmallButton("Remove tint key here"))
        {
            session.Edit(document, () =>
        {
            document.Asset.Colors.First(t => t.Slot == slot.Id && t.Kind == selectedKind).Keys.RemoveAll(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
            document.Asset.Colors.RemoveAll(t => t.Keys.Count == 0);
        });
        }

        if (Ui.Combo("Tint easing to next key", key.Ease, ClipEase.All) is { } ease)
        {
            session.Edit(document, () =>
        {
            var edited = document.Asset.Colors.First(t => t.Slot == slot.Id && t.Kind == selectedKind).Keys.First(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
            edited.Ease = ease; edited.CurveR = edited.CurveG = edited.CurveB = edited.CurveA = null;
        });
        }

        if (index + 1 >= track.Keys.Count) return;
        var next = track.Keys[index + 1];
        var components = selectedKind == SlotColorTrack2D.Alpha ? new[] { 3 } : selectedKind == SlotColorTrack2D.Rgb ? [0, 1, 2] : [0, 1, 2, 3];
        foreach (var axis in components)
        {
            var curve = axis switch { 0 => key.CurveR, 1 => key.CurveG, 2 => key.CurveB, _ => key.CurveA };
            CurveEditor("RGBA"[axis] + " tint curve", curve, key.Ease, key.Value[axis], next.Value[axis], changed => session.Change(document, () =>
            {
                var edited = document.Asset.Colors.First(t => t.Slot == slot.Id && t.Kind == selectedKind).Keys.First(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
                if (changed is not null && edited.Ease == ClipEase.Step) edited.Ease = ClipEase.Linear;
                if (axis == 0) edited.CurveR = changed;
                else if (axis == 1) edited.CurveG = changed;
                else if (axis == 2) edited.CurveB = changed; else edited.CurveA = changed;
            }));
        }
    }

    private static void CurveEditor(string label, KeyCurve2D? curve, string ease, float start, float end, Action<KeyCurve2D?> set)
    {
        if (!ImGui.TreeNode(label + (curve is null ? "" : " (cubic)"))) return;
        if (curve is null)
        {
            if (ImGui.SmallButton("Add cubic curve")) set(new() { X1 = 1f / 3, X2 = 2f / 3, Y1 = ease == ClipEase.Smooth ? 0 : 1f / 3, Y2 = ease == ClipEase.Smooth ? 1 : 2f / 3 });
        }
        else
        {
            var absolute = curve.Absolute;
            if (ImGui.Checkbox("Value controls in channel units", ref absolute))
            {
                var (first, second) = curve.Controls(start, end);
                if (absolute) set(curve with { Absolute = true, Y1 = first, Y2 = second });
                else if (start != end) set(curve with { Absolute = false, Y1 = (first - start) / (end - start), Y2 = (second - start) / (end - start) });
                else if (first == start && second == start) set(curve with { Absolute = false, Y1 = 0, Y2 = 1 });
            }
            var x = new Vector2(curve.X1, curve.X2);
            if (Ui.Drag2("Time controls (0–1)", ref x, .005f, 0, 1)) set(curve with { X1 = Math.Clamp(x.X, 0, 1), X2 = Math.Clamp(x.Y, 0, 1) });
            var y = new Vector2(curve.Y1, curve.Y2);
            if (Ui.Drag2(absolute ? "Value controls" : "Value controls (fractions)", ref y, .005f, 0, 0)) set(curve with { Y1 = y.X, Y2 = y.Y });
            if (ImGui.SmallButton("Remove cubic curve")) set(null);
            Ui.Help("Time is a fraction of this segment. Values can overshoot; channel units also allow motion between equal endpoints. Rotation and shear use radians.");
        }
        ImGui.TreePop();
    }
}
