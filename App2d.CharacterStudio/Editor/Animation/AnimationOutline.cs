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
        foreach (var piece in primary.Model.Parts)
            Row(clip.Asset, piece.Id, piece.Id + "  (geometry)", new(MotionClip.FitKind, piece.Id), [.. MotionClip.PartKinds.Select(k => new Channel(k, piece.Id))]);
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

                // P10c: where the instance mounts (a wearer control or another instance's control), whether it owns the root motion, and who keys it in this clip.
                var mount = ResolvedModel.NestedSocket(attachment.Socket) is { } nested ? $"on {nested.Instance}/{nested.Control}" : $"on {attachment.Socket.Control}";
                var owner = primary.Model.MotionOwner?.Id == attachment.Id ? ", root motion" : "";
                var keyedBy = clip.Asset.Plays.Any(p => p.Instance == attachment.Id) ? "this clip" : clip.Asset.Tracks.Any(t => MotionClip.InstanceOf(t.Target) == attachment.Id) ? "this clip's keys" : "its default";
                if (ImGui.Selectable($"{attachment.Id}  ({mount}{owner}; {keyedBy})", session.Selection.Attachment == attachment.Id && session.Selection.Control is null))
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
                    var id = attachment.Id + Part.Namespace + target;
                    var isPiece = attachment.Parts.Any(p => p.Id == target);
                    var channel = isPiece ? new Channel(MotionClip.FitKind, id) : ClipAuthoring.ChannelFor(primary.Model, id);
                    if (channel is null || channel.Value.Kind == MotionClip.TargetKind)
                    {
                        continue;
                    }

                    Row(clip.Asset, id, $"    {target}{(isPiece ? "  (piece)" : "")}", channel.Value, isPiece ? [.. ClipAuthoring.ChannelsForControl(primary.Model, id)] : null);
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

        var piece = channel.Kind == MotionClip.FitKind;
        if (ImGui.Selectable(label, (piece ? session.Selection.Part : session.Selection.Control) == id))
        {
            session.Selection.Select(control: piece ? null : id, part: piece ? id : null);
            session.EditWeapon = false;
        }
        if (!keyed)
        {
            ImGui.PopStyleColor();
        }
    }
}
