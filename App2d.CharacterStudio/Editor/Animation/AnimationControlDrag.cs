using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor;

/// <summary>Pointer picking and grab offsets; EditorSession performs pose edits and autokey transactions.</summary>
internal sealed class AnimationControlDrag(EditorSession session, Viewport viewport)
{
    private string? _dragging;
    private Vector3 _grab;
    private bool _grabbed;

    public void Draw(ViewportFrame frame, Subject subject)
    {
        var model = subject.Model;
        var pose = subject.Pose;
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragging = model.Order.Select(c => c.Id).Where(id => ClipAuthoring.ChannelFor(model, id) is not null)
                .OrderBy(id => Vector2.Distance(frame.Screen(pose.World(id)), ViewportFrame.Mouse)).FirstOrDefault(id => Vector2.Distance(frame.Screen(pose.World(id)), ViewportFrame.Mouse) < 12 * Ui.Scale);
            if (_dragging is not null) { session.Selection.Select(control: _dragging); session.BeginDrag(); _grabbed = false; }
        }
        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && _dragging is null) viewport.Fit(subject);
        if (_dragging is null) return;
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) { _dragging = null; session.EndDrag(); return; }
        // The first frame after the click has paused playback; measure the grab offset in that paused pose.
        if (!_grabbed) { _grab = pose.World(_dragging) - frame.World(ViewportFrame.Mouse); _grabbed = true; return; }
        if (ImGui.GetIO().MouseDelta == Vector2.Zero) return;
        var target = frame.World(ViewportFrame.Mouse) + _grab;
        session.DragControl(_dragging, target with { Z = pose.World(_dragging).Z });
    }
}
