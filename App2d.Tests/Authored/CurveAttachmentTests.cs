using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Rendering.Characters;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class CurveAttachmentTests
{
    private static CharacterModel Model(bool cubic = false)
    {
        var model = new CharacterModel
        {
            Id = "ribbon", Name = "Ribbon",
            Controls =
            [
                new() { Id = "root" },
                new() { Id = "start", Parent = "root", Rest = new(0, 0, .2f) },
                new() { Id = "bend", Parent = "root", Rest = new(cubic ? 2f / 3 : 1, 1, .2f) },
                new() { Id = "bend2", Parent = "root", Rest = new(4f / 3, 1, .2f) },
                new() { Id = "end", Parent = "root", Rest = new(2, 0, .2f) }
            ],
            Parts = [new()
            {
                Id = "stroke", A = "start", B = "end", Width = .02f, Depth = -.01f,
                Geometry = cubic
                    ? new CubicBezierCurveDefinition2D { Start = Vector2.Zero, Control1 = new(-.5f, 1f / 3), Control2 = new(-.5f, 2f / 3), End = Vector2.UnitY }
                    : new QuadraticBezierCurveDefinition2D { Start = Vector2.Zero, Control = new(-.5f, .5f), End = Vector2.UnitY },
                Material = new() { Fill = "#ff0000" },
                Bindings = cubic
                    ? [new() { Point = 0, Control = "start" }, new() { Point = 1, Control = "bend" }, new() { Point = 2, Control = "bend2" }, new() { Point = 3, Control = "end" }]
                    : [new() { Point = 0, Control = "start" }, new() { Point = 1, Control = "bend" }, new() { Point = 2, Control = "end" }]
            }]
        };
        model.Validate(); return model;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundCurvesPreserveRestAndFollowAnimatedHandlesAndTheirParent(bool cubic)
    {
        var model = Model(cubic); var part = model.Parts[0]; var resolved = ResolvedModel.From(model);
        var rest = PoseEvaluator.Rest(resolved);
        var original = PartContours2D.Contour(part with { Bindings = null }, rest.World);
        var attached = PartContours2D.Contour(part, rest.World, id => resolved.Rest[id]);
        for (var i = 0; i < original.Count; i++) TestModels.Near(original[i], attached[i], 1e-6f);

        var clip = ClipAuthoring.New(resolved, "bend", "Bend");
        ClipAuthoring.SetKey(clip, new("translate", "bend"), 0, new(.3f, -.4f, .4f));
        ClipAuthoring.SetKey(clip, new("translate", "start"), 0, new(-.2f, 0, 0));
        ClipAuthoring.SetKey(clip, new("translate", "root"), 0, new(3, 4, 0));
        ClipAuthoring.SetKey(clip, new("rotate", "root"), 0, Vector3.Zero, .7f);
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        var path = PartContours2D.Contour(part, pose.World, id => resolved.Rest[id]);
        var expectedMiddle = cubic
            ? (pose.World("start") + 3 * pose.World("bend") + 3 * pose.World("bend2") + pose.World("end")) / 8
            : (pose.World("start") + 2 * pose.World("bend") + pose.World("end")) / 4;
        expectedMiddle.Z += part.Depth;
        TestModels.Near(expectedMiddle, path[16], 1e-6f);
        TestModels.Near(pose.World("start") + new Vector3(0, 0, part.Depth), path[0], 1e-6f);
        TestModels.Near(pose.World("end") + new Vector3(0, 0, part.Depth), path[^1], 1e-6f);

        // Picking follows the rendered curve, including a moved handle outside the original endpoint frame.
        foreach (var point in path)
            Assert.InRange(PartPicking2D.Distance(part, pose.World, point, id => resolved.Rest[id]), 0, 1e-4f);
        Assert.True(PartPicking2D.Distance(part, pose.World, path[16] + new Vector3(0, 2, 0), id => resolved.Rest[id]) > 1);
        var drawing = new PuppetDrawing(); drawing.Build(resolved, pose);
        Assert.NotEmpty(drawing.Mesh.Vertices.ToArray());
        Assert.Contains(drawing.Mesh.Vertices.ToArray(), v => MathF.Abs(v.Position.Z - expectedMiddle.Z) < 1e-6f);
    }

    [Fact]
    public void AHandlePreservesItsDrawingOffsetWhileFollowingATowardControl()
    {
        var model = Model(); var part = model.Parts[0];
        model.Controls.Single(c => c.Id == "bend").Rest = new(1.5f, 1, .2f);
        model.Controls.Add(new() { Id = "up", Parent = "bend", Rest = new(1.5f, 2, .2f) });
        part.Bindings = [new() { Point = 1, Control = "bend", Toward = "up" }];
        var resolved = ResolvedModel.From(model);
        var clip = ClipAuthoring.New(resolved, "turn", "Turn");
        ClipAuthoring.SetKey(clip, new("translate", "bend"), 0, new(.5f, 0, .4f));
        ClipAuthoring.SetKey(clip, new("rotate", "bend"), 0, Vector3.Zero, MathF.PI / 2);
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        var path = PartContours2D.Contour(part, pose.World, id => resolved.Rest[id]);
        // Rest artwork handle (1, 1) sits .5 left of its control. That offset turns down.
        TestModels.Near(new(1.5f, .25f, .39f), path[16], 1e-6f);
        Assert.Throws<InvalidOperationException>(() => PartContours2D.Contour(part, pose.World));
        Assert.Throws<InvalidOperationException>(() => ModelAuthoring.RemoveControl(model, "up"));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("index")]
    [InlineData("control")]
    [InlineData("toward")]
    [InlineData("unsupported-curve")]
    public void InvalidCurveBindingsAreRejected(string failure)
    {
        var model = Model(); var part = model.Parts[0];
        switch (failure)
        {
            case "duplicate": part.Bindings!.Add(part.Bindings[0] with { }); break;
            case "index": part.Bindings![0].Point = 3; break;
            case "control": part.Bindings![0].Control = "missing"; break;
            case "toward": part.Bindings![1].Toward = "bend"; break;
            case "unsupported-curve": part.Geometry = new LineCurveDefinition2D { Start = Vector2.Zero, End = Vector2.UnitY }; break;
        }
        Assert.Throws<InvalidDataException>(model.Validate);
    }

    [Fact]
    public void CurveFaceIsAnOrdinarySavedRigWithEditableTracks()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var model = catalog.Models["curve-face"];
        var saved = CharacterModel.FromJson(model.ToJson());
        var resolved = ResolvedModel.From(saved);
        var clip = MotionClip.FromJson(catalog.Animations["curve-face-smile"].ToJson());
        clip.Validate(resolved);
        Assert.All(saved.Parts, p => Assert.Equal("none", p.Face));
        Assert.Empty(clip.Faces);
        Assert.All(clip.Tracks, t => Assert.Equal(MotionClip.TranslateKind, t.Kind));
        var mouth = saved.Parts.Single(p => p.Id == "mouth");
        var rest = PoseEvaluator.Rest(resolved);
        var smile = PoseEvaluator.Sample(resolved, clip, 1.5);
        var straight = PartContours2D.Contour(mouth, rest.World, id => resolved.Rest[id]);
        var bent = PartContours2D.Contour(mouth, smile.World, id => resolved.Rest[id]);
        Assert.True(bent[16].Y < straight[16].Y - .1f);
        Assert.True(bent[0].Y > straight[0].Y);
        var drawing = new PuppetDrawing(); drawing.Build(resolved, smile);
        Assert.True(drawing.Mesh.Count > 0);
        Assert.Equal(model.ToJson(), saved.ToJson());
    }

}
