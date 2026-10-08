using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class CutoutAttachmentTests
{
    private static CharacterModel Model() => new()
    {
        Id = "membrane", Name = "Membrane",
        Controls = [new() { Id = "root" }, new() { Id = "tip", Rest = new(1, 0, .2f) }, new() { Id = "direction", Parent = "tip", Rest = new(1, 1, .2f) }],
        Parts = [new() { Id = "membrane", Kind = PuppetPartKinds.Polygon, A = "root", Width = 1, Height = 1,
            Points = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)],
            Bindings = [new() { Point = 1, Control = "tip", Toward = "direction" }, new() { Point = 2, Control = "tip", Toward = "direction" }],
            Paint = [new() { Fill = "#ff0000", Points = [new(.6f, .2f), new(.9f, .2f), new(.9f, .8f), new(.6f, .8f)] }] }]
    };

    [Fact]
    public void AttachmentsPreserveRestAndFollowRotationTranslationAndDepth()
    {
        var model = Model(); var resolved = ResolvedModel.From(model); var part = model.Parts[0];
        var rest = PoseEvaluator.Rest(resolved);
        var bound = PartGeometry.Contour(part, rest.World, id => resolved.Rest[id]);
        var rigid = PartGeometry.Contour(part with { Bindings = null }, rest.World);
        Assert.Equal(rigid, bound);
        var clip = ClipAuthoring.New(resolved, "fold", "Fold");
        ClipAuthoring.SetKey(clip, new("translate", "tip"), 0, new(1, 0, .3f));
        ClipAuthoring.SetKey(clip, new("rotate", "tip"), 0, Vector3.Zero, MathF.PI / 2);
        var pose = PoseEvaluator.Sample(resolved, clip, 0); bound = PartGeometry.Contour(part, pose.World, id => resolved.Rest[id]);
        Assert.True(Vector3.Distance(new(2, 0, .3f), bound[1]) < 1e-5f);
        Assert.True(Vector3.Distance(new(1, 0, .3f), bound[2]) < 1e-5f);
        Assert.Equal(rigid[0], bound[0]);
        var original = PartGeometry.ArtworkPoint(part, 2, bound[2], pose.World, id => resolved.Rest[id]);
        Assert.True(Vector2.Distance(part.Points![2].XY, original.XY) < 1e-5f);
    }

    [Fact]
    public void DeformedFillPaintAndPickingShareGeometryAfterReflection()
    {
        var model = Model(); var resolved = ResolvedModel.From(model); var part = model.Parts[0];
        var clip = ClipAuthoring.New(resolved, "fold", "Fold");
        ClipAuthoring.SetKey(clip, new("translate", "tip"), 0, new(-1.5f, 0, 0));
        var pose = PoseEvaluator.Sample(resolved, clip, 0); var drawing = new PuppetDrawing(); drawing.Build(resolved, pose);
        Assert.True(drawing.Mesh.Count > 0);
        foreach (var vertex in drawing.Mesh.Vertices) Assert.True(float.IsFinite(vertex.Position.X));
        Assert.True(PartGeometry.Distance(part, pose.World, new(-.25f, .5f, 0), id => resolved.Rest[id]) <= 1);
        Assert.True(PartGeometry.Distance(part, pose.World, new(.75f, .5f, 0), id => resolved.Rest[id]) > 1);
        // Paint follows the reflected membrane rather than remaining in the old artwork rectangle.
        foreach (var vertex in drawing.Mesh.Vertices)
            if (vertex.Color.R == 255 && vertex.Color.G == 0) Assert.InRange(vertex.Position.X, -.46f, -.29f);
    }

    [Fact]
    public void PickingOverlappingFoldsUsesFilledTriangles()
    {
        var model = Model(); var resolved = ResolvedModel.From(model); var part = model.Parts[0];
        var clip = ClipAuthoring.New(resolved, "fold", "Fold");
        ClipAuthoring.SetKey(clip, new("translate", "tip"), 0, new(-.75f, .75f, 0));
        ClipAuthoring.SetKey(clip, new("rotate", "tip"), 0, Vector3.Zero, MathF.PI);
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        Assert.True(PartGeometry.Distance(part, pose.World, new(.12f, .5f, 0), id => resolved.Rest[id]) <= 1);
    }

    [Fact]
    public void PerimeterEditsRemapBindingsAndDeletingTheirControlIsRefused()
    {
        var model = Model(); var part = model.Parts[0];
        ModelAuthoring.InsertCutoutPoint(part, 1);
        Assert.Equal([1, 2, 3], part.Bindings!.Select(b => b.Point).Order());
        ModelAuthoring.RemoveCutoutPoint(part, 2);
        Assert.Equal([1, 2], part.Bindings!.Select(b => b.Point).Order()); model.Validate();
        Assert.Throws<InvalidOperationException>(() => ModelAuthoring.RemoveControl(model, "direction"));
        Assert.Contains("Detach", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.SetPartKind(model, "membrane", PuppetPartKinds.Ellipse)).Message);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("index")]
    [InlineData("control")]
    [InlineData("toward")]
    public void InvalidBindingsAreRejected(string failure)
    {
        var model = Model(); var part = model.Parts[0];
        switch (failure)
        {
            case "duplicate": part.Bindings!.Add(part.Bindings[0] with { }); break;
            case "index": part.Bindings![0].Point = 20; break;
            case "control": part.Bindings![0].Control = "missing"; break;
            case "toward": part.Bindings![0].Toward = part.Bindings[0].Control; break;
        }
        Assert.Throws<InvalidDataException>(model.Validate);
    }

    [Fact]
    public void AllStarterTemplatesUseTheSameDocumentPathAndRoundTrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "templates-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root); var session = new EditorSession(AuthoringWorkspace.Open(root));
            foreach (var template in CharacterTemplates.All)
            {
                var id = "test-" + template.Id;
                Assert.True(session.NewModel(id, template.Name, template.Id), session.Message);
                var model = session.Assets.Model(id)!.Asset;
                Assert.Equal(model.ToJson(), CharacterModel.FromJson(model.ToJson()).ToJson());
            }
            session.SaveAll(); Assert.False(session.MessageIsError, session.Message);
            Assert.Empty(AuthoredCatalog.Load(root).Errors);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void PterosaurFoldsNarrowlyAndEveryPoseRendersWithoutRetriangulatingTheFold()
    {
        var model = PterosaurTemplate.Model(); var resolved = ResolvedModel.From(model); var clips = PterosaurTemplate.Clips(model);
        float Span(MotionClip clip, float time)
        {
            var pose = PoseEvaluator.Sample(resolved, clip, time);
            var points = model.Parts.Where(p => p.Id.EndsWith("-wing")).SelectMany(p => PartGeometry.Contour(p, pose.World, id => resolved.Rest[id])).ToList();
            return points.Max(p => p.X) - points.Min(p => p.X);
        }
        var hover = clips.Single(c => c.Id.EndsWith("-hover")); var fold = clips.Single(c => c.Id.EndsWith("-fold"));
        Assert.True(Span(fold, fold.Duration) < Span(hover, 0) * .7f);
        var drawing = new PuppetDrawing();
        foreach (var clip in clips)
        {
            clip.Validate(resolved);
            Assert.Empty(clip.Travel.Keys); // The entity owns the actual flight path.
            for (var i = 0; i <= 20; i++)
            {
                drawing.Build(resolved, PoseEvaluator.Sample(resolved, clip, clip.Duration * i / 20f));
                Assert.True(drawing.Mesh.Count > 0);
            }
        }
    }
}
