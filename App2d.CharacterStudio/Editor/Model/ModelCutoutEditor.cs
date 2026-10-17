using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using ImGuiNET;
using System.Numerics;

namespace App2d.CharacterStudio.Editor.Model;

/// <summary>Edits cutout vertices and bindings, and groups each viewport drag into one undo step.</summary>
internal sealed class ModelCutoutEditor(EditorSession session)
{
    private bool _editCutout;
    private int _cutoutDrag = -1;

    public bool DragEnabled => _editCutout;

    public void Inspector(PuppetPart part, Action<Action<PuppetPart>> change)
    {
        if (part.Geometry is SimplePolygon2D polygon && ImGui.CollapsingHeader("Edit cutout silhouette"))
        {
            ImGui.Checkbox("Drag silhouette points in viewport", ref _editCutout);
            Ui.Help("Draw the rest silhouette here. Each point can follow the part frame or a named control; Point toward follows that control frame's rotation. Animate the controls, not the outline.");
            var vertices = PartContours2D.CutoutPoints(part);
            var controls = session.SubjectModel!.Asset.Controls.Select(c => c.Id).ToArray();
            void Cutout(Action<List<Vector2>> edit)
            {
                var points = new List<Vector2>(vertices);
                edit(points);
                try
                {
                    ModelAuthoring.CheckCutout(points);
                    change(p => ModelAuthoring.SetCutout(p, points));
                }
                catch (InvalidDataException) { /* Keep the last valid perimeter while editing. */ }
            }
            for (var i = 0; i < vertices.Count; i++)
            {
                var index = i;
                var xy = vertices[i];
                ImGui.PushID("cutout-" + i);
                if (Ui.Drag2("Point " + (i + 1), ref xy, .005f, -2, 2))
                {
                    Cutout(p => p[index] = xy);
                }

                ModelPointBindingInspector.Draw(part, index, controls, change);
                if (vertices.Count < 64 && ImGui.SmallButton("Insert after"))
                {
                    change(p => ModelAuthoring.InsertCutoutPoint(p, index));
                }

                ImGui.SameLine();
                if (vertices.Count > 3 && ImGui.SmallButton("Remove"))
                {
                    try
                    {
                        change(p => ModelAuthoring.RemoveCutoutPoint(p, index));
                    }
                    catch (InvalidDataException) { }
                }
                ImGui.PopID();
            }
        }
    }

    public void Drag(ViewportFrame frame, PuppetPart part, EvaluatedPose pose, ResolvedModel model)
    {
        var contour = PartContours2D.Contour(part, pose.World, id => pose.Angles[id], id => pose.Bones[id], id => model.Rest[id], id => model.RestTransforms[id]);
        var points = PartContours2D.CutoutPoints(part);
        for (var i = 0; i < points.Count; i++)
        {
            var at = frame.Screen(contour[i]);
            frame.Draw.AddCircleFilled(at, 4.5f * Ui.Scale, Ui.Color(232, 169, 55));
            if (frame.Hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && Vector2.Distance(at, ViewportFrame.Mouse) < 9 * Ui.Scale)
            {
                _cutoutDrag = i;
                session.BeginDrag();
            }
        }
        if (_cutoutDrag < 0)
        {
            return;
        }

        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            _cutoutDrag = -1;
            session.EndDrag();
            return;
        }
        if (_cutoutDrag >= points.Count || ImGui.GetIO().MouseDelta == Vector2.Zero || session.SubjectModel is not { } document)
        {
            return;
        }

        var edited = new List<Vector2>(points);
        try
        {
            edited[_cutoutDrag] = PartAttachment2D.ArtworkPoint(part, _cutoutDrag, frame.World(ViewportFrame.Mouse).XY, pose.World, id => model.Rest[id], id => pose.Angles[id], id => pose.Bones[id], id => model.RestTransforms[id]);
        }
        catch (InvalidOperationException)
        {
            return;
        }
        try
        {
            ModelAuthoring.CheckCutout(edited);
        }
        catch (InvalidDataException)
        {
            return;
        }
        session.Change(document, () => ModelAuthoring.SetCutout(document.Asset.Parts.First(p => p.Id == part.Id), edited));
    }
}
