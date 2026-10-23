using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Picks and drags control origins and bone tips in the model viewport.</summary>
internal sealed class ModelControlDrag(EditorSession session)
{
    private string? _dragging;
    private bool _draggingTip;
    private Vector3 _grab;

    public void Drag(ViewportFrame frame, ResolvedModel model, EvaluatedPose pose)
    {
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _dragging = null;
            _draggingTip = false;
            var best = 12 * Ui.Scale;
            foreach (var control in model.Order)
            {
                var origin = pose.World(control.Id);
                var distance = Vector2.Distance(frame.Screen(origin), ViewportFrame.Mouse);
                if (distance < best)
                {
                    best = distance;
                    _dragging = control.Id;
                    _draggingTip = false;
                }
                if (control.Length <= 0)
                {
                    continue;
                }

                var tip = ModelOverlay.BoneTip(control, pose);
                distance = Vector2.Distance(frame.Screen(tip), ViewportFrame.Mouse);
                if (distance < best)
                {
                    best = distance;
                    _dragging = control.Id;
                    _draggingTip = true;
                }
            }
            if (_dragging is not null)
            {
                session.Selection.Select(control: _dragging);
                var picked = _draggingTip ? ModelOverlay.BoneTip(model.Controls[_dragging], pose) : pose.World(_dragging);
                _grab = picked - frame.World(ViewportFrame.Mouse);
                session.BeginDrag();
            }
            else
            {
                session.Selection.Clear();
            }
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
        if (ImGui.GetIO().MouseDelta == Vector2.Zero)
        {
            return;
        }

        var target = frame.World(ViewportFrame.Mouse) + _grab;
        if (!_draggingTip)
        {
            session.DragControl(_dragging, target with
            {
                Z = pose.World(_dragging).Z
            });
            return;
        }
        if (session.SubjectModel is not { } document)
        {
            return;
        }

        var id = _dragging;
        session.Change(document, () => ModelAuthoring.MoveBoneTip(document.Asset, id, new(target.X, target.Y)));
    }
}
