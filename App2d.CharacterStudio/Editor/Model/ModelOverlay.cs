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
            draw.AddLine(frame.Screen(pose.World(control.Parent!)), frame.Screen(pose.World(control.Id)), Ui.Color(104, 112, 122, 170), 1.5f);
        }

        foreach (var control in model.Order.Where(c => c.Length > 0))
        {
            var origin = pose.World(control.Id);
            var selected = selectedControl == control.Id;
            var color = selected ? Ui.Color(232, 169, 55) : Ui.Color(84, 179, 160);
            draw.AddLine(frame.Screen(origin), frame.Screen(BoneTip(control, pose)), color, (selected ? 4 : 2.5f) * Ui.Scale);
            draw.AddCircleFilled(frame.Screen(BoneTip(control, pose)), 4 * Ui.Scale, color);
        }
        foreach (var control in model.Order)
        {
            var p = frame.Screen(pose.World(control.Id));
            var selected = selectedControl == control.Id;
            var end = model.Chains.Values.Any(c => c.End == control.Id);
            draw.AddCircleFilled(p, (selected ? 6 : 4.5f) * Ui.Scale, selected ? Ui.Color(232, 169, 55) : end ? Ui.Color(50, 145, 160) : Ui.Color(230, 233, 229));
            draw.AddCircle(p, (selected ? 6 : 4.5f) * Ui.Scale, Ui.Color(64, 78, 89), 16, 1);
            if (selected)
            {
                draw.AddText(p + new Vector2(9, -18) * Ui.Scale, Ui.Color(44, 53, 68), control.Id);
            }
        }
    }

    public static void DrawPart(ViewportFrame frame, PuppetPart part, EvaluatedPose pose, ResolvedModel model)
    {
        var outline = PartContours2D.Contour(part, pose.World, id => pose.Angles[id], id => pose.Bones[id], id => model.Rest[id], id => model.RestTransforms[id]).Select(p => frame.Screen(p)).ToArray();
        for (var i = 1; i < outline.Length; i++)
        {
            frame.Draw.AddLine(outline[i - 1], outline[i], Ui.Color(232, 169, 55), 2);
        }

        if (part.Geometry is not ICurve2D && outline.Length > 1)
        {
            frame.Draw.AddLine(outline[^1], outline[0], Ui.Color(232, 169, 55), 2);
        }
    }

    public static Vector3 BoneTip(ModelControl control, EvaluatedPose pose)
    {
        var origin = pose.World(control.Id);
        return new(Vector2.Transform(new(control.Length, 0), pose.Bones[control.Id]), origin.Z);
    }
}
