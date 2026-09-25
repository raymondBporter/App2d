using App2d.Core.Characters.Editing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor;

/// <summary>The one transport: clip choice, play, scrub and preview speed. Speed is a preview setting, never clip duration.</summary>
internal static class TransportBar
{
    /// <param name="preview">In Model the clip only previews the build, and rest is one click away.</param>
    public static void Draw(EditorSession session, bool preview)
    {
        var clips = session.SubjectId is null ? [] : session.Assets.ClipsFor(session.SubjectId).OrderBy(c => c.Name).ToArray();
        var clip = session.ClipDocument;
        ImGui.SetNextItemWidth(170 * Ui.Scale);
        if (ImGui.BeginCombo("##clip", clip?.Name ?? "(no animation)"))
        {
            foreach (var c in clips)
            {
                var playable = session.Assets.CanPlay(c.Id, session.SubjectId!, out var error);
                if (ImGui.Selectable(c.Name + (playable ? "" : "  (incompatible)"), c.Id == session.ClipId)) session.SetClip(c.Id);
                if (!playable && ImGui.IsItemHovered()) ImGui.SetTooltip(error);
            }
            ImGui.EndCombo();
        }
        if (clip is null) { ImGui.SameLine(); ImGui.TextDisabled(clips.Length == 0 ? "No animations for this model yet." : ""); return; }
        var transport = session.Transport; var duration = clip.Asset.Duration;
        ImGui.SameLine(); if (ImGui.Button(transport.Playing ? "Pause" : "Play", new(64 * Ui.Scale, 0))) session.TogglePlay();
        ImGui.SameLine(); if (ImGui.Button("|<")) session.Seek(0);
        ImGui.SameLine(); if (ImGui.Button(">|")) session.Seek(duration);
        ImGui.SameLine(); ImGui.SetNextItemWidth(Math.Max(120, ImGui.GetContentRegionAvail().X - (preview ? 250 : 170) * Ui.Scale));
        var time = transport.Time;
        if (ImGui.SliderFloat("##time", ref time, 0, duration, $"%.3f / {duration:F3} s")) { session.Transport.Pause(); session.Seek(time); }
        ImGui.SameLine(); ImGui.SetNextItemWidth(90 * Ui.Scale);
        var speed = transport.Speed; if (ImGui.SliderFloat("##speed", ref speed, .1f, 2, "%.2fx speed")) transport.Speed = speed;
        if (preview) { ImGui.SameLine(); var rest = session.ShowRest; if (ImGui.Checkbox("Rest", ref rest)) session.ShowRest = rest; }
        if (preview && session.EditRig) Ui.Help("Editing the rig shows rest. Turn off Edit rig to watch the build move.");
        else if (!clip.Asset.Loop && transport.Time >= duration) ImGui.TextDisabled("Holding the final pose.");
    }
}
