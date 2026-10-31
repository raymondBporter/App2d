using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class CurveAttachmentTests
{
    private static CharacterModel Model(bool cubic = false)
    {
        var model = new CharacterModel
        {
            Id = "ribbon",
            Name = "Ribbon",
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
                Id = "stroke", A = "start", B = "end", Layer = -.01f,
                Geometry = cubic
                    ? new CubicBezier2D { Start = Vector2.Zero, Control1 = new(-.5f, 1f / 3), Control2 = new(-.5f, 2f / 3), End = Vector2.UnitY }.Validated()
                    : new QuadraticBezier2D { Start = Vector2.Zero, Control = new(-.5f, .5f), End = Vector2.UnitY }.Validated(),
                Material = new() { Fill = "#ff0000", StrokeWidth = .02f },
                Bindings = cubic
                    ? [new() { Point = 0, Control = "start" }, new() { Point = 1, Control = "bend" }, new() { Point = 2, Control = "bend2" }, new() { Point = 3, Control = "end" }]
                    : [new() { Point = 0, Control = "start" }, new() { Point = 1, Control = "bend" }, new() { Point = 2, Control = "end" }]
            }]
        };
        model.Validate();
        return model;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundCurvesPreserveRestAndFollowAnimatedHandlesAndTheirParent(bool cubic)
    {
        var model = Model(cubic);
        var part = model.Parts[0];
        var resolved = ResolvedModel.From(model);
        var rest = PoseEvaluator.Rest(resolved);
        var original = PartContours2D.Contour(part with
        {
            Bindings = null
        }, rest.World);
        var attached = PartContours2D.Contour(part, rest.World, id => resolved.Rest[id]);
        for (var i = 0; i < original.Count; i++)
        {
            TestModels.Near(original[i], attached[i], 1e-6f);
        }

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
        expectedMiddle.Z += part.Layer;
        TestModels.Near(expectedMiddle, TestModels.DrawPoint(path, 16), 1e-6f);
        TestModels.Near(pose.World("start") + new Vector3(0, 0, part.Layer), TestModels.DrawPoint(path, 0), 1e-6f);
        TestModels.Near(pose.World("end") + new Vector3(0, 0, part.Layer), TestModels.DrawPoint(path, path.Count - 1), 1e-6f);
        Assert.All(path, p => Assert.Equal(0, p.Z));

        // Picking follows the rendered curve, including a moved handle outside the original endpoint frame.
        foreach (var point in path)
        {
            Assert.InRange(PartPicking2D.Distance(part, pose.World, point, id => resolved.Rest[id]), 0, 1e-4f);
        }

        Assert.True(PartPicking2D.Distance(part, pose.World, path[16] + new Vector3(0, 2, 0), id => resolved.Rest[id]) > 1);
        var drawing = new PuppetDrawing();
        drawing.Build(resolved, pose);
        Assert.NotEmpty(drawing.Mesh.Vertices.ToArray());
        Assert.Contains(drawing.Mesh.Vertices.ToArray(), v => MathF.Abs(v.Layer - expectedMiddle.Z) < 1e-6f);
    }

    [Fact]
    public void AHandlePreservesItsDrawingOffsetWhileFollowingATowardControl()
    {
        var model = Model();
        var part = model.Parts[0];
        model.Controls.Single(c => c.Id == "bend").Rest = new(1.5f, 1, .2f);
        model.Controls.Add(new()
        {
            Id = "up",
            Parent = "bend",
            Rest = new(1.5f, 2, .2f)
        });
        part.Bindings = [new() { Point = 1, Control = "bend", Toward = "up" }];
        var resolved = ResolvedModel.From(model);
        var clip = ClipAuthoring.New(resolved, "turn", "Turn");
        ClipAuthoring.SetKey(clip, new("translate", "bend"), 0, new(.5f, 0, .4f));
        ClipAuthoring.SetKey(clip, new("rotate", "bend"), 0, Vector3.Zero, MathF.PI / 2);
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        var path = PartContours2D.Contour(part, pose.World, id => resolved.Rest[id]);
        // Rest artwork handle (1, 1) sits .5 left of its control. That offset turns down.
        TestModels.Near(new(1.5f, .25f, .39f), TestModels.DrawPoint(path, 16), 1e-6f);
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
        var model = Model();
        var part = model.Parts[0];
        switch (failure)
        {
            case "duplicate":
                part.Bindings!.Add(part.Bindings[0] with
                {
                });
                break;
            case "index":
                part.Bindings![0].Point = 3;
                break;
            case "control":
                part.Bindings![0].Control = "missing";
                break;
            case "toward":
                part.Bindings![1].Toward = "bend";
                break;
            case "unsupported-curve":
                part.Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated();
                break;
        }
        Assert.Throws<InvalidDataException>(model.Validate);
    }
}
