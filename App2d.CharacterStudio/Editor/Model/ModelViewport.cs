using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Curves;
using App2d.Core.Characters.Editing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Shapes;
using ImGuiNET;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Coordinates model overlays, part picking, and the active viewport drag tool.</summary>
internal sealed class ModelViewport(EditorSession session, Viewport viewport, ModelCutoutEditor cutout)
{
    private readonly ModelControlDrag _controls = new(session);

    public void Draw(ViewportFrame frame)
    {
        if (frame.Primary is not { } primary)
        {
            return;
        }

        var pose = primary.Subject.Pose;
        var model = primary.Subject.Model;
        if (session.EditRig)
        {
            ModelOverlay.DrawRig(frame, model, pose, session.Selection.Control);
            _controls.Drag(frame, model, pose);
            return;
        }
        if (session.Selection.Part is { } selectedPart && model.Parts.FirstOrDefault(p => p.Id == selectedPart) is { } part)
        {
            ModelOverlay.DrawPart(frame, part, pose, model);
            if (cutout.DragEnabled && part.Geometry is SimplePolygon2D && session.SubjectModel is not null)
            {
                cutout.Drag(frame, part, pose, model);
                return;
            }
        }
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var visibleParts = model.Base.Slots.Count == 0 ? model.Parts : [.. pose.Slots.Where(s => s.Part is not null).Select(s => s.Part!)];
            var (Part, Score) = visibleParts
                .Where(p => !p.Hidden)
                .Select(p => (Part: p, Score: PartPicking2D.Distance(p, pose.World, frame.World(ViewportFrame.Mouse), id => pose.Angles[id], id => pose.Bones[id], id => model.Rest[id], id => model.RestTransforms[id], model.Base.LineWidth)))
                .Where(h => h.Score <= 1)
                .OrderBy(h => h.Score)
                .FirstOrDefault();
            if (Part is not null)
            {
                session.Selection.Select(part: Part.Id);
            }
            else
            {
                session.Selection.Clear();
            }
        }
        if (frame.Hovered && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
        {
            viewport.Fit(primary.Subject);
        }
    }
}
