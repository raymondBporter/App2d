using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Draws rig handles and the selected part outline without changing editor state.</summary>
internal static class ModelOverlay
{
    public static void DrawRig(ViewportFrame frame, ResolvedModel model, EvaluatedPose pose, string? selectedControl)
    {
        var draw = frame.Draw;
        foreach (var control in model.Order.Where(c => c.Parent is not null))
        {
            draw.AddLine(frame.Screen(pose.World(control.Parent!)), frame.Screen(pose.World(control.Id)), StudioTheme.ModelBoneLink.PackedValue, 1.5f);
        }

        foreach (var control in model.Order.Where(c => c.Length > 0))
        {
            var origin = pose.World(control.Id);
            var selected = selectedControl == control.Id;
            var color = selected ? StudioTheme.Selection.PackedValue : StudioTheme.Bone.PackedValue;
            draw.AddLine(frame.Screen(origin), frame.Screen(BoneTip(control, pose)), color, (selected ? 4 : 2.5f) * Ui.Scale);
            draw.AddCircleFilled(frame.Screen(BoneTip(control, pose)), 4 * Ui.Scale, color);
        }
        foreach (var control in model.Order)
        {
            var p = frame.Screen(pose.World(control.Id));
            var selected = selectedControl == control.Id;
            var end = model.Chains.Values.Any(c => c.End == control.Id);
            draw.AddCircleFilled(p, (selected ? 6 : 4.5f) * Ui.Scale, selected ? StudioTheme.Selection.PackedValue : end ? StudioTheme.TargetControl.PackedValue : StudioTheme.ControlFill.PackedValue);
            draw.AddCircle(p, (selected ? 6 : 4.5f) * Ui.Scale, StudioTheme.ControlOutline.PackedValue, 16, 1);
            if (selected)
            {
                draw.AddText(p + new Vector2(9, -18) * Ui.Scale, StudioTheme.ControlLabel.PackedValue, control.Id);
            }
        }
    }

    public static void DrawPart(ViewportFrame frame, PartGeometry part, EvaluatedPose pose, ResolvedModel model)
    {
        var outline = PartContours2D.Contour(part, model, pose).Select(p => frame.Screen(p)).ToArray();
        for (var i = 1; i < outline.Length; i++)
        {
            frame.Draw.AddLine(outline[i - 1], outline[i], StudioTheme.Selection.PackedValue, 2);
        }

        if (part.Geometry is not ICurve2D && outline.Length > 1)
        {
            frame.Draw.AddLine(outline[^1], outline[0], StudioTheme.Selection.PackedValue, 2);
        }
    }

    /// <summary>Outlines every piece of a library part instance at its evaluated placement: the payload parts' contours and the solids' outlines.</summary>
    public static void DrawAttachment(ViewportFrame frame, ResolvedAttachment attachment, EvaluatedPose pose)
    {
        var placement = attachment.Placement(new ActorPose(pose, Vector2.Zero, 1).SocketPlacement(attachment.Socket));
        var color = StudioTheme.Selection.PackedValue;
        // An animated instance (P7a) outlines its pieces over its evaluated nested pose; a static one over its locations.
        pose.Attachments.TryGetValue(attachment.Id, out var evaluated);
        if (evaluated is { Hidden: true })
        {
            return;
        }

        var (world, local) = attachment.Lookups(placement);
        var pieces = evaluated is null ? attachment.Parts.Where(p => !p.Hidden).ToList() : [.. evaluated.DrawnParts()];
        foreach (var part in pieces)
        {
            var outline = (evaluated is null ? PartContours2D.Contour(part, world, local, world, local) : PartContours2D.Contour(part, evaluated)).Select(p => frame.Screen(p)).ToArray();
            for (var i = 1; i < outline.Length; i++)
            {
                frame.Draw.AddLine(outline[i - 1], outline[i], color, 2);
            }

            if (part.Geometry is not App2d.Core.Geometry.Curves.ICurve2D && outline.Length > 1)
            {
                frame.Draw.AddLine(outline[^1], outline[0], color, 2);
            }
        }
        frame.Draw.AddCircleFilled(frame.Screen(placement.Origin), 4 * Ui.Scale, color);
    }

    public static Vector3 BoneTip(ModelControl control, EvaluatedPose pose)
    {
        return pose.Placement(control.Id).At(new Vector2(control.Length, 0));
    }
}
