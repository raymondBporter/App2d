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
        if (session.Selection.Attachment is { } selectedAttachment && model.Attachment(selectedAttachment) is { } attachment)
        {
            ModelOverlay.DrawAttachment(frame, attachment, pose);
        }
        if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var at = frame.World(ViewportFrame.Mouse);
            var visibleParts = model.Base.Slots.Count == 0 ? model.Parts : [.. pose.Slots.Where(s => s.Part is not null).Select(s => s.Part!)];
            var (Part, Score) = visibleParts
                .Where(p => !p.Hidden)
                .Select(p => (Part: p, Score: PartPicking2D.Distance(p, model, pose, at)))
                .Where(h => h.Score <= 1)
                .OrderBy(h => h.Score)
                .FirstOrDefault();
            // Library part instances pick through the same score as the model's parts; the nearest of either wins.
            var (Attachment, AttachmentScore) = model.Attachments
                .Where(a => !a.Instance.Hidden)
                .Select(a => (Attachment: a, Score: PartPicking2D.Distance(a, pose, at)))
                .Where(h => h.Score <= 1)
                .OrderBy(h => h.Score)
                .FirstOrDefault();
            if (Attachment is not null && (Part is null || AttachmentScore <= Score))
            {
                session.Selection.Select(attachment: Attachment.Id);
            }
            else if (Part is not null)
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
