using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Animation;

/// <summary>Preview build, attachment preview, control channels, animated part instances with their exposed targets (P7a), and slots in their on-screen order.</summary>
internal sealed class AnimationOutline(EditorSession session, AnimationWeaponEditor weapons)
{
    public void Draw()
    {
        if (session.ClipDocument is not { } clip)
        {
            Ui.Help("Choose an animation in the browser, or create one with New.");
            return;
        }
        var basis = clip.Asset.Model;
        var subjects = session.Assets.Models.Where(m => m.Id == basis).Select(m => m.Id).Concat(session.Assets.Variants.Where(v => v.Asset.Base == basis).Select(v => v.Id)).ToArray();
        if (Ui.Combo("Preview on", session.SubjectId ?? basis, subjects, id => session.Assets.Find(id)?.Name ?? id) is { } subject)
        {
            session.SetSubject(subject);
        }

        Ui.Help("Keys are stored in the base's reference units, so they play on every build.");
        if (session.Scene().FirstOrDefault() is not { } primary)
        {
            return;
        }

        weapons.DrawOutline(primary);
        Ui.Header("Channels");
        foreach (var control in primary.Model.Order)
        {
            var channel = ClipAuthoring.ChannelFor(primary.Model, control.Id);
            if (channel is null)
            {
                continue;
            }

            Row(clip.Asset, control.Id, $"{control.Id}{(channel.Value.Kind == MotionClip.TargetKind ? "  (IK " + channel.Value.Target + ")" : "")}", channel.Value);
        }
        var animated = primary.Model.Attachments.Where(a => a.IsAnimated).ToArray();
        if (animated.Length > 0)
        {
            Ui.Header("Parts");
            foreach (var attachment in animated)
            {
                var played = clip.Asset.Plays.Any(p => p.Instance == attachment.Id);
                if (!played)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
                }

                if (ImGui.Selectable($"{attachment.Id}  (expression)", session.Selection.Attachment == attachment.Id && session.Selection.Control is null))
                {
                    session.Selection.Select(attachment: attachment.Id);
                    session.EditWeapon = false;
                }

                if (!played)
                {
                    ImGui.PopStyleColor();
                }

                // The targets the asset exposes to the wearer, as namespaced channels; a piece's fit and visibility are keyed through the control inspector's channels too.
                foreach (var target in attachment.Asset.Exposed)
                {
                    var id = attachment.Id + PartAsset.Namespace + target;
                    var isPiece = attachment.Parts.Any(p => p.Id == target);
                    var channel = isPiece ? new Channel(MotionClip.FitKind, id) : ClipAuthoring.ChannelFor(primary.Model, id);
                    if (channel is null || channel.Value.Kind == MotionClip.TargetKind)
                    {
                        continue;
                    }

                    Row(clip.Asset, id, $"    {target}{(isPiece ? "  (piece)" : "")}", channel.Value, isPiece ? [channel.Value, new(MotionClip.ShowKind, id)] : null);
                }
            }
        }
        if (primary.Model.Base.Slots.Count > 0)
        {
            Ui.Header("Slots");
            foreach (var slot in primary.Model.Base.Slots)
            {
                if (ImGui.Selectable(slot.Name ?? slot.Id, session.Selection.Slot == slot.Id))
                {
                    session.Selection.Select(slot: slot.Id);
                    session.EditWeapon = false;
                }
            }
        }
    }

    private void Row(MotionClip clip, string id, string label, Channel channel, Channel[]? keyedBy = null)
    {
        var keyed = (keyedBy ?? [channel, new(MotionClip.RotateKind, id)]).Any(c => ClipAuthoring.Track(clip, c) is not null);
        if (!keyed)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        }

        if (ImGui.Selectable(label, session.Selection.Control == id))
        {
            session.Selection.Select(control: id);
            session.EditWeapon = false;
        }
        if (!keyed)
        {
            ImGui.PopStyleColor();
        }
    }
}
