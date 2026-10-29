using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Selected slot attachment, draw order, tint, and outgoing tint curves.</summary>
internal sealed class AnimationSlotInspector(EditorSession session)
{
    private string _colorKind = SlotColorTrack2D.Rgba;

    public void Draw(AssetDocument<MotionClip> document, Subject subject, string id)
    {
        var slot = subject.Model.Base.Slots.FirstOrDefault(s => s.Id == id);
        if (slot is null)
        {
            return;
        }

        Ui.Header("Slot " + (slot.Name ?? id));
        var time = session.Transport.Time;
        var track = document.Asset.Attachments.FirstOrDefault(t => t.Slot == id);
        var attachmentKey = track?.Keys.LastOrDefault(k => k.Time <= time);
        var current = attachmentKey is null ? slot.Attachment : attachmentKey.Attachment;
        var choices = subject.Model.Base.Skins.SelectMany(s => s.Attachments.GetValueOrDefault(id)?.Keys.AsEnumerable() ?? []).Distinct().Prepend("(none)");
        if (Ui.Combo("Attachment at playhead", current ?? "(none)", choices) is { } attachment)
        {
            session.Edit(document, () => ClipAuthoring.SetAttachmentKey(document.Asset, id, time, attachment == "(none)" ? null : attachment));
        }

        if (ImGui.Button("Remove attachment key here"))
        {
            session.Edit(document, () => ClipAuthoring.DeleteAttachmentKey(document.Asset, id, time));
        }

        if (ImGui.Button("Draw earlier"))
        {
            session.Edit(document, () => ClipAuthoring.MoveSlot(document.Asset, subject.Model.Base.Slots, id, time, -1));
        }

        ImGui.SameLine();
        if (ImGui.Button("Draw later"))
        {
            session.Edit(document, () => ClipAuthoring.MoveSlot(document.Asset, subject.Model.Base.Slots, id, time, 1));
        }

        if (ImGui.Button("Remove draw order key here"))
        {
            session.Edit(document, () => ClipAuthoring.DeleteDrawOrderKey(document.Asset, time));
        }

        ColorFields(document, slot, time);
        Ui.Help("Attachment and order changes are keyed at the playhead. Slots are listed from back to front.");
    }

    private void ColorFields(AssetDocument<MotionClip> document, SkeletonSlot2D slot, float time)
    {
        Ui.Header("Animated tint");
        var tracks = document.Asset.Colors.Where(t => t.Slot == slot.Id).ToList();
        string[] kinds = tracks.Any(t => t.Kind == SlotColorTrack2D.Rgba) ? [SlotColorTrack2D.Rgba]
            : tracks.Count > 0 ? [SlotColorTrack2D.Rgb, SlotColorTrack2D.Alpha] : [SlotColorTrack2D.Rgba, SlotColorTrack2D.Rgb, SlotColorTrack2D.Alpha];
        if (!kinds.Contains(_colorKind))
        {
            _colorKind = kinds[0];
        }

        if (Ui.Combo("Color channel", _colorKind, kinds) is { } kind)
        {
            _colorKind = kind;
        }

        var selectedKind = _colorKind;
        var value = SlotColorTrack2D.Evaluate(document.Asset, slot, time);
        if (ImGui.ColorEdit4("Tint at playhead", ref value))
        {
            session.Change(document, () => ClipAuthoring.SetColorKey(document.Asset, slot.Id, selectedKind, time, value));
        }

        if (ImGui.SmallButton("Key tint here"))
        {
            session.Edit(document, () => ClipAuthoring.SetColorKey(document.Asset, slot.Id, selectedKind, time, value));
        }

        var track = tracks.FirstOrDefault(t => t.Kind == selectedKind);
        if (track is null)
        {
            return;
        }

        var index = track.Keys.FindIndex(k => MathF.Abs(k.Time - time) < ClipAuthoring.SameTime);
        if (index < 0)
        {
            return;
        }

        var key = track.Keys[index];
        if (ImGui.SmallButton("Remove tint key here"))
        {
            session.Edit(document, () => ClipAuthoring.DeleteColorKey(document.Asset, slot.Id, selectedKind, time));
        }

        if (Ui.Combo("Tint easing to next key", key.Ease, ClipEase.All) is { } ease)
        {
            session.Edit(document, () => ClipAuthoring.SetColorEase(document.Asset, slot.Id, selectedKind, time, ease));
        }

        if (index + 1 >= track.Keys.Count)
        {
            return;
        }

        var next = track.Keys[index + 1];
        var components = selectedKind == SlotColorTrack2D.Alpha ? new[] { 3 } : selectedKind == SlotColorTrack2D.Rgb ? [0, 1, 2] : [0, 1, 2, 3];
        foreach (var axis in components)
        {
            var curve = axis switch
            {
                0 => key.CurveR,
                1 => key.CurveG,
                2 => key.CurveB,
                _ => key.CurveA
            };
            AnimationCurveEditor.Draw("RGBA"[axis] + " tint curve", curve, key.Ease, key.Value[axis], next.Value[axis],
                changed => session.Change(document, () => ClipAuthoring.SetColorCurve(document.Asset, slot.Id, selectedKind, time, axis, changed)));
        }
    }
}
