using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor;

/// <summary>Preview build, attachment preview, control channels, and slots in their on-screen order.</summary>
internal sealed class AnimationOutline(EditorSession session, AnimationWeaponEditor weapons)
{
    public void Draw()
    {
        if (session.ClipDocument is not { } clip) { Ui.Help("Choose an animation in the browser, or create one with New."); return; }
        var basis = clip.Asset.Model;
        var subjects = session.Assets.Models.Where(m => m.Id == basis).Select(m => m.Id).Concat(session.Assets.Variants.Where(v => v.Asset.Base == basis).Select(v => v.Id)).ToArray();
        if (Ui.Combo("Preview on", session.SubjectId ?? basis, subjects, id => session.Assets.Find(id)?.Name ?? id) is { } subject) session.SetSubject(subject);
        Ui.Help("Keys are stored in the base's reference units, so they play on every build.");
        if (session.Scene().FirstOrDefault() is not { } primary) return;
        weapons.DrawOutline(primary);
        Ui.Header("Channels");
        foreach (var control in primary.Model.Order)
        {
            var channel = ClipAuthoring.ChannelFor(primary.Model, control.Id);
            if (channel is null) continue;
            var keyed = ClipAuthoring.Track(clip.Asset, channel.Value) is not null || ClipAuthoring.Track(clip.Asset, new(MotionClip.RotateKind, control.Id)) is not null;
            if (!keyed) ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
            if (ImGui.Selectable($"{control.Id}{(channel.Value.Kind == MotionClip.TargetKind ? "  (IK " + channel.Value.Target + ")" : "")}", session.Selection.Control == control.Id))
            { session.Selection.Select(control: control.Id); session.EditWeapon = false; }
            if (!keyed) ImGui.PopStyleColor();
        }
        if (primary.Model.Base.Slots.Count > 0)
        {
            Ui.Header("Slots");
            foreach (var slot in primary.Model.Base.Slots)
                if (ImGui.Selectable(slot.Name ?? slot.Id, session.Selection.Slot == slot.Id)) { session.Selection.Select(slot: slot.Id); session.EditWeapon = false; }
        }
    }
}
