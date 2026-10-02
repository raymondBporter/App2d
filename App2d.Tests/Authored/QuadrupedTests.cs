using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Geometry;
using App2d.Core.Meshes;
using App2d.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class QuadrupedTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StarterClipsResolveAndKeepFourLegsReachable(bool dinosaur)
    {
        var model = QuadrupedTemplate.Model(triceratops: dinosaur); var resolved = ResolvedModel.From(model);
        Assert.Equal(4, model.Chains.Count);
        Assert.Equal(model.ToJson(), CharacterModel.FromJson(model.ToJson()).ToJson());
        foreach (var clip in QuadrupedTemplate.Clips(model))
        {
            clip.Validate(resolved);
            for (var i = 0; i <= 40; i++)
            {
                var pose = PoseEvaluator.Sample(resolved, clip, clip.Duration * i / 40f);
                Assert.All(pose.Points.Values, p => Assert.True(float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z)));
                Assert.All(pose.Chains, c => Assert.True(c.Residual < .06f, $"{clip.Id} at {i}: {c.Chain} residual {c.Residual}"));
            }
        }
    }

    [Fact]
    public void WalkingContactHoldsFootInWorldAndAdvancesAtNextLoop()
    {
        var model = QuadrupedTemplate.Model(); var resolved = ResolvedModel.From(model);
        var clip = QuadrupedTemplate.Clips(model).Single(c => c.Id.EndsWith("-walk"));
        var a = PoseEvaluator.Sample(resolved, clip, .2f); var b = PoseEvaluator.Sample(resolved, clip, .4f);
        Assert.True(Vector3.Distance(a.World("near-front-foot"), b.World("near-front-foot")) < .001f);
        var next = PoseEvaluator.Sample(resolved, clip, clip.Duration + .2f, repeat: true);
        Assert.True(Vector3.Distance(next.World("near-front-foot") - a.World("near-front-foot"), new(clip.Travel.Keys[^1].X, 0, 0)) < .001f);
    }

    [Fact]
    public void ConcaveCutoutPickingAndPaintRespectTheNotch()
    {
        var part = new PuppetPart
        {
            A = "body",
            Kind = "polygon",
            Width = 1,
            Height = 1,
            Points = [new(0, 0), new(1, 0), new(1, .3f), new(.3f, .3f), new(.3f, 1), new(0, 1)],
            Paint = [new() { Fill = "#ff0000", Points = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)] }]
        };
        part.Validate(_ => true);
        Assert.True(PartGeometry.Distance(part, _ => Vector3.Zero, new(.15f, .8f, 0)) <= 1);
        Assert.True(PartGeometry.Distance(part, _ => Vector3.Zero, new(.8f, .8f, 0)) > 1);
        var mesh = TriangleMesh2D.TriangulateSimplePolygon(part.Points.Select(p => p.XY)); Assert.Equal(.51f, mesh.Area, 5);
        var drawing = new PuppetDrawing(); drawing.Build("#000000", 0, [part], _ => Vector3.Zero);
        foreach (var vertex in drawing.Mesh.Vertices)
            Assert.True(vertex.Position.X <= .30001f || vertex.Position.Y <= .30001f, "Fill or paint spills across the notch.");
        part.Points = [new(0, 0), new(1, 1), new(0, 1), new(1, 0)];
        Assert.Throws<InvalidDataException>(() => part.Validate(_ => true));
    }

    [Fact]
    public void ShippedCreatureModelsAndTheirClipsLoadThroughCatalog()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.True(catalog.Errors.Count == 0, string.Join("\n", catalog.Errors));
        foreach (var id in new[] { "quadruped", "triceratops" })
        {
            var model = catalog.Resolve(id);
            Assert.Equal(4, model.Chains.Count);
            var clips = catalog.Animations.Values.Where(c => c.Model == id).ToList();
            Assert.Equal(8, clips.Count);
            foreach (var clip in clips) clip.Validate(model);
        }
    }

    [Fact]
    public void EditorCreatesUnsavedModelAndClipsAndSavesThemTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), "quadruped-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root); var session = new EditorSession(AuthoringWorkspace.Open(root));
            Assert.True(session.NewModel("dino", "Dino", "triceratops"), session.Message);
            Assert.Equal("dino-idle", session.ClipId); Assert.False(session.EditRig);
            Assert.Equal(8, session.Assets.Clips.Count());
            Assert.All(session.Assets.Clips, c => Assert.True(c.IsNew));
            session.SaveAll(); Assert.False(session.MessageIsError, session.Message);
            var catalog = AuthoredCatalog.Load(root); Assert.Equal(8, catalog.Animations.Count);
            Assert.NotNull(catalog.Resolve("dino"));
        }
        finally { Directory.Delete(root, true); }
    }
}
