using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Curves;
using App2d.Core.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class RoundedPathTests
{
    [Fact]
    public void DrawingPickingAndDepthFollowTheInteriorControlAfterSaving()
    {
        var model = ModelAuthoring.Empty("tail", "Tail");
        for (var i = 0; i < 5; i++) ModelAuthoring.AddControl(model, null, new(i, 0, i), "p" + i);
        var part = ModelAuthoring.AddPart(model, PuppetPartKinds.Stroke, "p0", "p4");
        ModelAuthoring.SetRoundedPath(part, ["p0", "p1", "p2", "p3", "p4"]);
        part.CurveSections = 18; part.Depth = .1f;
        model = CharacterModel.FromJson(model.ToJson()); part = model.Parts.Single();
        model.Controls[2].Rest = new(2, 1, 4);
        var resolved = ResolvedModel.From(model); var pose = PoseEvaluator.Rest(resolved);
        var path = PartGeometry.Contour(part, pose.World);
        Assert.Equal(pose.World("p0") + new Vector3(0, 0, .1f), path[0]);
        Assert.Equal(pose.World("p4") + new Vector3(0, 0, .1f), path[^1]);
        var peak = path.MaxBy(p => p.Y);
        Assert.True(peak.Y > .7f); Assert.True(peak.Z > 3);
        Assert.InRange(PartGeometry.Distance(part, pose.World, peak), 0, 1e-4f);
        Assert.True(PartGeometry.Distance(part, pose.World, new(2, 0, 0)) > 1);
        var drawing = new PuppetDrawing(); drawing.Build(resolved, pose);
        Assert.True(drawing.Mesh.Max.Y > .7f);
        Assert.True(drawing.Mesh.TriangleCount < 150);
        Assert.Throws<InvalidOperationException>(() => ModelAuthoring.RemoveControl(model, "p2"));
    }

    [Fact]
    public void PersonUsesFourRoundedLimbsWithoutChangingRigOrTorso()
    {
        var model = PersonTemplate.Model(); var roundtrip = CharacterModel.FromJson(model.ToJson());
        var paths = roundtrip.Parts.Where(p => p.Geometry is RoundedPolylineCurveDefinition2D).ToArray();
        Assert.Equal(4, paths.Length);
        foreach (var part in paths)
        {
            Assert.Equal(20, part.CurveSections);
            Assert.Equal(1, ((RoundedPolylineCurveDefinition2D)part.Geometry!).Softness);
            Assert.Equal(3, part.Bindings!.Count);
            Assert.Equal(part.A, part.Bindings[0].Control); Assert.Equal(part.B, part.Bindings[^1].Control);
        }
        Assert.IsNotType<RoundedPolylineCurveDefinition2D>(roundtrip.Parts.Single(p => p.Id == "body").Geometry);
        foreach (var variant in new[] { PersonBuild.TallThin, PersonBuild.ShortBroad })
        {
            var resolved = ResolvedModel.From(model, variant.Apply(model, "build", "Build"));
            var pose = PoseEvaluator.Rest(resolved);
            foreach (var part in resolved.Parts.Where(p => p.Geometry is RoundedPolylineCurveDefinition2D))
            {
                var contour = PartGeometry.Contour(part, pose.World);
                Assert.Equal(pose.World(part.A) + new Vector3(0, 0, part.Depth), contour[0]);
                Assert.Equal(pose.World(part.B!) + new Vector3(0, 0, part.Depth), contour[^1]);
            }
        }
    }

    [Fact]
    public void InvalidCurveBindingsFailValidation()
    {
        var model = PersonTemplate.Model(); var part = model.Parts.First(p => p.Geometry is RoundedPolylineCurveDefinition2D);
        part.Bindings![1].Control = "missing"; Assert.Throws<InvalidDataException>(model.Validate);
        part.Bindings[1].Control = part.Bindings[0].Control;
        part.Bindings[1].Point = 99; Assert.Throws<InvalidDataException>(model.Validate);
    }
}
