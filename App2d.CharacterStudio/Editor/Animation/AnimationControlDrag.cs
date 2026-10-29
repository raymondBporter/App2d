using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using ImGuiNET;
using System.Numerics;
using Vector3 = System.Numerics.Vector3;

namespace App2d.CharacterStudio.Editor.Animation;

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
        Vector3 World(string id) => ClipAuthoring.Nested(model, id) is { } nested ? pose.Attachments[nested.Attachment.Id].World(nested.Target) : pose.World(id);
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            // The wearer's own controls, then the exposed controls of its animated instances (P7a) by their namespaced names.
            var candidates = model.Order.Select(c => c.Id).Where(id => ClipAuthoring.ChannelFor(model, id) is not null)
                .Concat(pose.Attachments.Values.Where(e => !e.Hidden).SelectMany(e => e.Rig.Order.Where(c => e.Attachment.Asset.Exposes(c.Id) && ClipAuthoring.ChannelFor(e.Rig, c.Id) is { Kind: not MotionClip.TargetKind }).Select(c => e.Qualify(c.Id))));
            _dragging = candidates.OrderBy(id => Vector2.Distance(frame.Screen(World(id)), ViewportFrame.Mouse)).FirstOrDefault(id => Vector2.Distance(frame.Screen(World(id)), ViewportFrame.Mouse) < 12 * Ui.Scale);
            if (_dragging is not null)
            {
                session.Selection.Select(control: _dragging);
                session.BeginDrag();
                _grabbed = false;
            }
        }
        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && _dragging is null)
        {
            viewport.Fit(subject);
        }

        if (_dragging is null)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _dragging = null;
            session.EndDrag();
            return;
        }
        // The first frame after the click has paused playback; measure the grab offset in that paused pose.
        if (!_grabbed)
        {
            _grab = World(_dragging) - frame.World(ViewportFrame.Mouse);
            _grabbed = true;
            return;
        }
        if (ImGui.GetIO().MouseDelta == Vector2.Zero)
        {
            return;
        }

        var target = frame.World(ViewportFrame.Mouse) + _grab;
        session.DragControl(_dragging, target with
        {
            Z = World(_dragging).Z
        });
    }
}
