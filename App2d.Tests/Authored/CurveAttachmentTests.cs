using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
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
                Id = "stroke", A = "start", B = "end", Depth = -.01f,
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
        expectedMiddle.Z += part.Depth;
        TestModels.Near(expectedMiddle, path[16], 1e-6f);
        TestModels.Near(pose.World("start") + new Vector3(0, 0, part.Depth), path[0], 1e-6f);
        TestModels.Near(pose.World("end") + new Vector3(0, 0, part.Depth), path[^1], 1e-6f);

        // Picking follows the rendered curve, including a moved handle outside the original endpoint frame.
        foreach (var point in path)
        {
            Assert.InRange(PartPicking2D.Distance(part, pose.World, point, id => resolved.Rest[id]), 0, 1e-4f);
        }

        Assert.True(PartPicking2D.Distance(part, pose.World, path[16] + new Vector3(0, 2, 0), id => resolved.Rest[id]) > 1);
        var drawing = new PuppetDrawing();
        drawing.Build(resolved, pose);
        Assert.NotEmpty(drawing.Mesh.Vertices.ToArray());
        Assert.Contains(drawing.Mesh.Vertices.ToArray(), v => MathF.Abs(v.Position.Z - expectedMiddle.Z) < 1e-6f);
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
        var drawing = new PuppetDrawing();
        drawing.Build(resolved, smile);
        Assert.True(drawing.Mesh.Count > 0);
        Assert.Equal(model.ToJson(), saved.ToJson());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalWavesFollowControlPhaseAndParentMotionWithoutChangingLayer(bool bound)
    {
        var model = Model();
        var part = model.Parts[0];
        part.Geometry = new QuadraticBezier2D { Start = Vector2.Zero, Control = new(0, .5f), End = Vector2.UnitY }.Validated();
        model.Controls.Single(c => c.Id == "bend").Rest = new(1, 0, .2f);
        if (!bound)
        {
            part.Bindings = null;
        }

        part.WaveAmplitude = .1f;
        part.WaveCycles = 2;
        part.WavePhaseControl = "wave";
        model.Controls.Add(new()
        {
            Id = "wave",
            Parent = "root"
        });
        model = CharacterModel.FromJson(model.ToJson());
        part = model.Parts[0];
        var resolved = ResolvedModel.From(model);
        var rest = PoseEvaluator.Rest(resolved);
        var initial = PartContours2D.Contour(part, rest.World, id => resolved.Rest[id]);
        TestModels.Near(new(.25f, .2f, .19f), initial[4], 1e-6f);

        var clip = ClipAuthoring.New(resolved, "wave", "Wave", loop: false);
        ClipAuthoring.SetKey(clip, new("translate", "wave"), 0, new(.5f, 0, 0));
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        var shifted = PartContours2D.Contour(part, pose.World, id => resolved.Rest[id]);
        TestModels.Near(new(.25f, 0, .19f), shifted[4], 1e-6f);
        Assert.All(shifted, p => Assert.Equal(.19f, p.Z, 5));
        foreach (var p in shifted)
        {
            Assert.InRange(PartPicking2D.Distance(part, pose.World, p, id => resolved.Rest[id]), 0, 1e-4f);
        }

        ClipAuthoring.SetKey(clip, new("rotate", "root"), 0, Vector3.Zero, .7f);
        ClipAuthoring.SetKey(clip, new("translate", "root"), 0, new(3, 4, 0));
        pose = PoseEvaluator.Sample(resolved, clip, 0);
        var carried = PartContours2D.Contour(part, pose.World, id => resolved.Rest[id]);
        for (var i = 0; i < carried.Count; i++)
        {
            TestModels.Near(Vector3.Transform(shifted[i], Matrix4x4.CreateRotationZ(.7f)) + new Vector3(3, 4, 0), carried[i], 1e-5f);
        }

        Assert.Throws<InvalidOperationException>(() => ModelAuthoring.RemoveControl(model, "wave"));
    }

    [Theory]
    [InlineData("height")]
    [InlineData("cycles")]
    [InlineData("control")]
    [InlineData("shape")]
    public void InvalidNormalWaveSettingsAreRejected(string failure)
    {
        var model = Model();
        var part = model.Parts[0];
        part.WaveAmplitude = .1f;
        switch (failure)
        {
            case "height":
                part.WaveAmplitude = float.NaN;
                break;
            case "cycles":
                part.WaveCycles = 0;
                break;
            case "control":
                part.WavePhaseControl = "missing";
                break;
            case "shape":
                part.Bindings = null;
                part.Geometry = new Ellipse2D { Center = Vector2.Zero, Radii = new(.5f, .5f) }.Validated();
                break;
        }
        Assert.Throws<InvalidDataException>(model.Validate);
    }

    [Theory]
    [InlineData("smile", 1.5f)]
    [InlineData("blink", 1f)]
    [InlineData("wink", 1f)]
    [InlineData("angry", 1f)]
    [InlineData("worried", 1f)]
    [InlineData("surprised", 1f)]
    [InlineData("crossed-eyes", 1f)]
    [InlineData("hair-wave", .375f)]
    [InlineData("expressions", 1f)]
    public void FacialStudiesUseOrdinaryTracksAndReturnToTheirSetupDrawing(string role, float at)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var model = catalog.Resolve("curve-face");
        var clip = MotionClip.FromJson(catalog.Animations["curve-face-" + role].ToJson());
        clip.Validate(model);
        Assert.Empty(clip.Faces);
        Assert.All(clip.Tracks, t => Assert.True(t.Kind is MotionClip.TranslateKind or MotionClip.ScaleKind));
        var rest = PoseEvaluator.Rest(model);
        var posed = PoseEvaluator.Sample(model, clip, at);
        var end = PoseEvaluator.Sample(model, clip, clip.Duration);
        var changed = false;
        List<Vector3> Path(string id, EvaluatedPose pose) => PartContours2D.Contour(model.Parts.Single(p => p.Id == id), model, pose);
        foreach (var part in model.Parts)
        {
            var original = Path(part.Id, rest);
            var current = Path(part.Id, posed);
            var last = Path(part.Id, end);
            for (var i = 0; i < original.Count; i++)
            {
                Assert.True(float.IsFinite(current[i].X) && float.IsFinite(current[i].Y));
                changed |= Vector3.Distance(original[i], current[i]) > .001f;
                TestModels.Near(original[i], last[i], 1e-5f, part.Id);
            }
        }
        Assert.True(changed);
        if (role is "blink" or "wink")
        {
            Assert.All(Path("left-eye-upper", posed), p => Assert.Equal(1.13f, p.Y, 5));
            var upper = Path("left-eye-upper", posed);
            var lower = Path("left-eye-lower", posed);
            for (var i = 0; i < upper.Count; i++)
            {
                TestModels.Near(upper[i], lower[i], 1e-6f);
            }

            if (role == "wink")
            {
                Assert.True(Path("right-eye-upper", posed)[16].Y > 1.15f);
            }
        }
        if (role == "crossed-eyes")
        {
            var upper = Path("left-eye-upper", posed);
            var lower = Path("left-eye-lower", posed);
            Assert.True(upper[0].Y < upper[^1].Y && lower[0].Y > lower[^1].Y);
            TestModels.Near(upper[16], lower[16], 1e-6f);
        }
        if (role == "surprised")
        {
            Assert.True(Path("mouth-upper", posed)[16].Y - Path("mouth", posed)[16].Y > .29f);
        }

        if (role is "angry" or "worried")
        {
            var brow = Path("left-brow", posed);
            Assert.Equal(role == "worried", brow[^1].Y > brow[0].Y);
        }
        var drawing = new PuppetDrawing();
        drawing.Build(model, posed);
        Assert.True(drawing.Mesh.Count > 0);
    }
}
