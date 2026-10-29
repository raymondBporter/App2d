using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Shapes;

namespace App2d.CharacterStudio.Editor;

internal sealed partial class EditorSmoke
{
    private void EditPassivePart(EditorShell shell)
    {
        var s = shell.Session;
        s.Compare.Clear();
        s.Open("dumbbell");
        var doc = s.PartDocument!;
        var before = ShapeBounds2D.Calculate(PartShapes2D.Collision(doc.Asset));
        Record(s.Mode == Workspace.Appearance && !s.PreviewPartOnWearer && doc.Kind == AssetKind.Part, "the passive dumbbell opens in the shared Part editor with a standalone preview");
        Check(s.Edit(doc, () =>
        {
            doc.Asset.Physics!.Mass = 3;
            doc.Asset.Geometry.Add(new() { Id = "trim", Frame = PartLocation.Origin, Geometry = LineSegmentCurve2D.Builder.Create(new(-40, 25), new(40, 25)), Material = new() { Fill = "#25d2be", StrokeWidth = 2 } });
            doc.Asset.Validate();
        }), s);
        Record(ShapeBounds2D.Calculate(PartShapes2D.Collision(doc.Asset)) == before && doc.Asset.Behavior is null, "decorative trim adds no collision and a passive body needs no Behavior");
        s.Undo();
        Record(doc.Asset.Physics!.Mass == 2 && doc.Asset.Geometry.Count == 3, "one Part undo restores body settings and geometry together");
        s.Redo();
        Check(s.Save(doc), s);
        shell.Viewport.Fit(s.Scene().Single());
    }

    private void EditInvisiblePart(EditorShell shell)
    {
        var s = shell.Session;
        Check(s.NewPart("invisible-body", "Invisible collision body"), s);
        var doc = s.PartDocument!;
        Check(s.Edit(doc, () =>
        {
            doc.Asset.Geometry = [new() { Id = "body", Geometry = Circle2D.Builder.Create(.5f), Collision = new() }];
            doc.Asset.Physics = new() { Mass = 2, FreezeRotation = false };
            doc.Asset.Validate();
        }), s);
        s.PreviewPartOnWearer = false;
        var subject = s.Scene().Single();
        var drawing = new PuppetDrawing(); drawing.Build(subject.Model, subject.Pose);
        Record(drawing.Mesh.Count == 0 && PartShapes2D.Collision(doc.Asset) is Circle2D, "a collision-only entry has a body shape and no rendered material");
        Check(s.Save(doc), s);
        shell.Viewport.Fit(subject);
    }

    private void VerifyPartWorkflow(EditorShell shell)
    {
        var s = shell.Session;
        s.SaveAll();
        var reopened = AuthoringWorkspace.Open(s.Assets.Root);
        Record(reopened.LoadErrors.Count == 0 && reopened.Documents.All(d => reopened.Problems(d).Count == 0), "the full authored dependency bundle reopens without retired formats or unresolved references");
        foreach (var id in new[] { "dumbbell", "boot", "texture-path", "sword", "hero", "invisible-body" })
        {
            var doc = reopened.Part(id)!;
            Record(doc.Kind == AssetKind.Part && doc.Asset.Version == Part.CurrentVersion && doc.Path!.Contains(Path.DirectorySeparatorChar + "parts" + Path.DirectorySeparatorChar), $"{id} uses the same current Part document, loader and save folder");
        }
        Record(reopened.Part("dumbbell")!.Asset.Physics!.Mass == 3 && reopened.Part("invisible-body")!.Asset.Geometry[0].Material is null,
            "body settings, decorative geometry and invisible collision survive save/reopen");
        s.Open("sword");
        s.PreviewPartOnWearer = false;
        shell.Viewport.Fit(s.Scene().Single());
    }
}
