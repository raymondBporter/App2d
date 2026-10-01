using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Timing;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor;

/// <summary>Preview-only end behavior; choosing one does not change the authored clip.</summary>
internal static class PlaybackModePicker
{
    public static void Draw(EditorSession session, MotionClip clip)
    {
        var transport = session.Transport;
        var selected = transport.EndModeOverride;
        ImGui.TextDisabled("Preview end:"); ImGui.SameLine();
        ImGui.SetNextItemWidth(140 * Ui.Scale);
        var label = selected switch
        {
            null => clip.Loop ? "Clip default (loop)" : "Clip default (hold)",
            PlaybackEndMode.Hold => "Hold",
            PlaybackEndMode.Loop => "Loop",
            _ => "Ping pong",
        };
        if (!ImGui.BeginCombo("##preview-end", label)) return;
        Choice("Clip default", null);
        Choice("Hold", PlaybackEndMode.Hold);
        Choice("Loop", PlaybackEndMode.Loop);
        Choice("Ping pong", PlaybackEndMode.PingPong);
        ImGui.EndCombo();

        void Choice(string name, PlaybackEndMode? mode)
        {
            if (!ImGui.Selectable(name, selected == mode) || selected == mode) return;
            var time = transport.Time;
            transport.EndModeOverride = mode;
            session.Seek(time);
        }
    }
}
