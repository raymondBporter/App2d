using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Characters.Spine;
using App2d.Core.Rendering.Characters;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class SpineInterchangeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-spine-" + Guid.NewGuid());
    private static byte[] Image()
    {
        using var bitmap = new Bitmap(16, 20); using (var g = Graphics.FromImage(bitmap)) g.Clear(Color.Coral);
        using var stream = new MemoryStream(); bitmap.Save(stream, ImageFormat.Png); return stream.ToArray();
    }
    private const string Rig = """
        {
          "skeleton": { "spine": "4.2.22" },
          "bones": [
            { "name": "Wheel Root", "x": 50, "y": 70, "rotation": 30, "scaleX": -1.5, "scaleY": 2, "shearY": 15 },
            { "name": "spoke.tip", "parent": "Wheel Root", "x": 40, "y": 5, "rotation": 20 }
          ],
          "slots": [
            { "name": "rear paint", "bone": "Wheel Root", "attachment": "red" },
            { "name": "front paint", "bone": "spoke.tip", "attachment": "blue" }
          ],
          "skins": [
            { "name": "default", "attachments": {
              "rear paint": { "red": { "path": "red", "width": 16, "height": 20 } },
              "front paint": { "blue": { "path": "blue", "width": 16, "height": 20, "x": 10 } }
            } },
            { "name": "Gold finish", "attachments": { "rear paint": { "red": { "path": "gold", "width": 16, "height": 20 } } } }
          ],
          "events": { "tick": { "int": 3 } },
          "animations": { "Spin wheel": {
            "bones": {
              "Wheel Root": {
                "rotate": [ { "time": 0, "value": 0, "curve": [0.2, 40, 0.8, 140] }, { "time": 1, "value": 180 } ],
                "scale": [ { "x": 1, "y": 1 }, { "time": 1, "x": 0.5, "y": 1.5 } ],
                "shear": [ {}, { "time": 1, "x": 10, "y": -20 } ]
              },
              "spoke.tip": { "translate": [ { "time": 0.25, "x": 20, "y": 10 }, { "time": 1, "x": 30, "y": 20 } ] }
            },
            "slots": { "front paint": { "attachment": [ { "time": 0.5, "name": null }, { "time": 0.75, "name": "blue" } ] } },
            "drawOrder": [ { "time": 0.4, "offsets": [ { "slot": "rear paint", "offset": 1 }, { "slot": "front paint", "offset": -1 } ] } ],
            "events": [ { "time": 0.2, "name": "tick" }, { "time": 0.8, "name": "tick", "int": 5 } ]
          } }
        }
        """;

    [Fact]
    public void AffineChannelsComposeReflectShearAndCollapseWithoutLosingDescendantFrames()
    {
        var result = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image()); var model = ResolvedModel.From(result.Model);
        var setup = result.Model.Controls[0].Transform!;
        var rest = PoseEvaluator.Rest(model);
        var expected = Vector2.Transform(new(.4f, .05f), setup.Matrix);
        TestModels.Near(new(expected, 0), rest.World("spoke-tip"));
        var clip = result.Animations.Single(); var pose = PoseEvaluator.Sample(model, clip, 1);
        var animated = setup with { Rotation = setup.Rotation + MathF.PI, ScaleX = setup.ScaleX * .5f, ScaleY = setup.ScaleY * 1.5f, ShearX = 10 * MathF.PI / 180, ShearY = setup.ShearY - 20 * MathF.PI / 180 };
        expected = Vector2.Transform(new(.7f, .25f), animated.Matrix);
        TestModels.Near(new(expected, 0), pose.World("spoke-tip"));
        clip.Tracks.Single(t => t.Kind == MotionClip.ScaleKind).Keys[^1].X = -1;
        var collapsed = PoseEvaluator.Sample(model, clip, 1);
        Assert.Equal(0, collapsed.Bones["wheel-root"].GetDeterminant(), 5);
        Assert.True(float.IsFinite(collapsed.World("spoke-tip").X));
    }

    [Fact]
    public void SkinsFallbackAttachmentHidingOrderAndRepeatedEventsHaveIndependentMeaning()
    {
        var result = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image()); var model = ResolvedModel.From(result.Model); var clip = result.Animations[0];
        var pose = PoseEvaluator.Sample(model, clip, .6, input: new() { Skin = "gold-finish" });
        Assert.Equal(["front-paint", "rear-paint"], pose.Slots.Select(s => s.Slot.Id));
        Assert.Null(pose.Slots[0].Part);
        Assert.Contains("gold-finish", pose.Slots[1].Part!.Id);
        pose = PoseEvaluator.Sample(model, clip, .9, input: new() { Skin = "gold-finish" });
        Assert.Contains("default", pose.Slots[0].Part!.Id);
        Assert.Equal([3, 5], clip.Events.Select(e => e.Int));
        Assert.Equal("tick", clip.Events[1].Name);
        var drawing = new PuppetDrawing(); drawing.Build(model, pose);
        Assert.Equal(2, drawing.Batches.Count); Assert.All(drawing.Batches, b => Assert.NotNull(b.Texture));
        Assert.Equal(12, drawing.Mesh.Count);
    }

    [Fact]
    public void NativeAffineRoundTripPreservesCurvesNamesMatricesAndTimelineSemantics()
    {
        var initial = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image());
        Store(initial.Images); var model = ResolvedModel.From(initial.Model); model.TextureRoot = _root;
        var package = SpineExport2D.Build(model, initial.Animations);
        var imported = SpineImport2D.Parse(package.Json, "returned", "Returned", name => package.Images["images/" + name + ".png"]);
        var returned = ResolvedModel.From(imported.Model);
        for (var i = 0; i <= 20; i++)
        {
            var t = i / 20.0; var a = PoseEvaluator.Sample(model, initial.Animations[0], t); var b = PoseEvaluator.Sample(returned, imported.Animations[0], t);
            foreach (var bone in initial.Model.Controls)
            {
                var other = imported.Model.Controls.Single(c => c.Name == bone.Name);
                TestModels.Near(a.World(bone.Id), b.World(other.Id), 2e-5f);
                MatrixNear(a.Bones[bone.Id], b.Bones[other.Id]);
            }
            Assert.Equal(a.Slots.Select(s => s.Part is null), b.Slots.Select(s => s.Part is null));
            Assert.Equal(a.Slots.Select(s => s.Slot.Name), b.Slots.Select(s => s.Slot.Name));
        }
        Assert.NotNull(imported.Animations[0].Tracks.First(t => t.Kind == MotionClip.RotateKind).Keys[0].Curve);
        Assert.Equal("Wheel Root", imported.Model.Controls[0].Name);
        Assert.Equal(initial.Model.Controls.Count, imported.Model.Controls.Count);
    }

    [Fact]
    public void ExistingPointIkAndProceduralArtworkExportAsEditableBonesAndRegions()
    {
        var source = ResolvedModel.From(TestModels.Creature()); var clip = TestModels.Clip(source);
        clip.Tracks = [new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new(), new() { Time = 1, X = -.2f, Y = .1f }] }];
        var package = SpineExport2D.Build(source, [clip]);
        var imported = SpineImport2D.Parse(package.Json, "returned", "Returned", name => package.Images["images/" + name + ".png"]);
        var model = ResolvedModel.From(imported.Model);
        foreach (var time in new[] { 0f, .25f, .5f, .75f, 1f })
        {
            var expected = PoseEvaluator.Sample(source, clip, time); var actual = PoseEvaluator.Sample(model, imported.Animations[0], time);
            foreach (var id in source.Controls.Keys) TestModels.Near(expected.World(id), actual.World(id), 2e-4f);
        }
        Assert.Equal(source.Parts.Count, imported.Model.Slots.Count);
        Assert.All(imported.Model.Parts, p => Assert.NotNull(p.Material!.Texture));
        Assert.Contains(package.Notes, n => n.Contains("Point IK"));
        var backup = JsonNode.Parse(package.NativeBackup)!;
        Assert.Equal(2, CharacterModel.FromJson(backup["model"]!.ToJsonString()).Chains.Count);
    }

    [Fact]
    public void AffinePoseEditingIsTheInverseOfEvaluationAndRestMovesRespectChildChoice()
    {
        var source = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image()); var model = ResolvedModel.From(source.Model);
        var clip = source.Animations[0]; var pose = PoseEvaluator.Sample(model, clip, .3);
        var desired = pose.World("spoke-tip") + new Vector3(.2f, -.15f, 0);
        ClipAuthoring.Pose(model, clip, pose, .3f, "spoke-tip", desired);
        TestModels.Near(desired, PoseEvaluator.Sample(model, clip, .3).World("spoke-tip"));
        var child = model.Rest["spoke-tip"];
        ModelAuthoring.MoveRest(source.Model, "wheel-root", new(1, 2, 0), children: false);
        var moved = ResolvedModel.From(source.Model);
        TestModels.Near(new(1, 2, 0), moved.Rest["wheel-root"]);
        TestModels.Near(child, moved.Rest["spoke-tip"]);
    }

    [Theory]
    [InlineData("\"ik\": [{\"name\":\"follow\"}],")]
    [InlineData("\"physics\": [{\"name\":\"bounce\"}],")]
    public void UnsupportedConstraintsAreReportedBeforeAnyPartialImport(string extra)
    {
        var json = Rig.Replace("\"bones\":", extra + "\"bones\":", StringComparison.Ordinal);
        Assert.Contains("does not yet support", Assert.Throws<InvalidDataException>(() => SpineImport2D.Parse(json, "wheel", "Wheel", _ => Image())).Message);
    }

    [Fact]
    public void AtlasRestoresTrimAndTheExportedPackageWorksWithoutLooseImages()
    {
        Directory.CreateDirectory(_root); using (var page = new Bitmap(5, 7))
        {
            page.SetPixel(1, 2, Color.Red); page.SetPixel(2, 2, Color.Blue); page.Save(Path.Combine(_root, "page.png"), ImageFormat.Png);
        }
        var path = Path.Combine(_root, "test.atlas"); File.WriteAllText(path, "page.png\nsize: 5, 7\nregion\nbounds: 1, 2, 2, 1\noffsets: 1, 2, 4, 5\n");
        var extracted = new SpineAtlas2D(path).Extract("region");
        using var stream = new MemoryStream(extracted); using var image = new Bitmap(stream);
        Assert.Equal(4, image.Width); Assert.Equal(5, image.Height);
        Assert.Equal(Color.Red.ToArgb(), image.GetPixel(1, 2).ToArgb());
        var source = ResolvedModel.From(TestModels.Creature()); var package = SpineExport2D.Build(source, [TestModels.Clip(source)]);
        package.Save(Path.Combine(_root, "rig.json"));
        var atlas = new SpineAtlas2D(Path.Combine(_root, "rig.atlas"));
        var first = package.Images.First(); var region = Path.GetFileNameWithoutExtension(first.Key);
        Assert.True(atlas.Extract(region).Length > 0);
        var loaded = SpineImport2D.Load(Path.Combine(_root, "rig.json"), "atlas-rig", "Atlas rig", imagesDirectory: Path.Combine(_root, "missing-images"));
        Assert.Equal(source.Parts.Count, loaded.Images.Count);
    }

    [Fact]
    public void VariantsAndFlatteningPreserveAffineSetupAndPoseEditing()
    {
        var imported = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image());
        var variant = new ModelVariant { Id = "moved-wheel", Name = "Moved wheel", Base = "wheel", Rest = new() { ["wheel-root"] = new(2, 3) } };
        var model = ResolvedModel.From(imported.Model, variant); var rest = PoseEvaluator.Rest(model);
        TestModels.Near(new(2, 3, 0), rest.World("wheel-root"));
        var flat = ResolvedModel.From(ModelAuthoring.Flatten(model, "flat-wheel", "Flat wheel"));
        foreach (var bone in model.Order) TestModels.Near(rest.World(bone.Id), PoseEvaluator.Rest(flat).World(bone.Id));
        var clip = imported.Animations[0]; var pose = PoseEvaluator.Sample(model, clip, .3);
        var desired = pose.World("wheel-root") + new Vector3(.2f, .3f, 0);
        ClipAuthoring.Pose(model, clip, pose, .3f, "wheel-root", desired);
        TestModels.Near(desired, PoseEvaluator.Sample(model, clip, .3).World("wheel-root"));
    }

    [Fact]
    public void ExportDisambiguatesAnimationNamesPreservesDurationAndDoesNotGrowRoots()
    {
        var source = ResolvedModel.From(TestModels.Creature()); var first = TestModels.Clip(source);
        var second = ClipAuthoring.Duplicate(first, "another-motion", first.Name);
        var package = SpineExport2D.Build(source, [first, second]);
        var imported = SpineImport2D.Parse(package.Json, "returned", "Returned", name => package.Images["images/" + name + ".png"]);
        Assert.Equal(2, imported.Animations.Select(c => c.Name).Distinct().Count());
        Store(imported.Images); var model = ResolvedModel.From(imported.Model); model.TextureRoot = _root;
        var clip = imported.Animations[0]; clip.Duration = 2;
        var again = SpineExport2D.Build(model, [clip]);
        var returned = SpineImport2D.Parse(again.Json, "returned-again", "Returned again", name => again.Images["images/" + name + ".png"]);
        Assert.Equal(2, returned.Animations[0].Duration);
        Assert.Equal(imported.Model.Controls.Count, returned.Model.Controls.Count);
    }

    [Fact]
    public void KeyPoseBeforeAnImportedTimelineStartsKeepsSetupValues()
    {
        var source = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image());
        var clip = source.Animations[0]; var model = ResolvedModel.From(source.Model);
        var expected = PoseEvaluator.Sample(model, clip, .1).World("spoke-tip");
        ClipAuthoring.KeyPose(clip, .1f);
        TestModels.Near(expected, PoseEvaluator.Sample(model, clip, .1).World("spoke-tip"));
    }

    [Fact]
    public void AffineReparentingKeepsWorldFrameAndVariantMovesCanLeaveChildrenBehind()
    {
        var source = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image()); var initial = ResolvedModel.From(source.Model);
        var oldFrame = initial.RestTransforms["spoke-tip"];
        ModelAuthoring.Reparent(source.Model, "spoke-tip", null);
        var moved = ResolvedModel.From(source.Model);
        MatrixNear(oldFrame, moved.RestTransforms["spoke-tip"]);
        TestModels.Near(initial.Rest["spoke-tip"], moved.Rest["spoke-tip"]);
        var variant = new ModelVariant { Id = "moved-wheel", Name = "Moved", Base = "wheel" };
        source = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image());
        ModelAuthoring.MoveRest(ResolvedModel.From(source.Model), variant, "wheel-root", new(3, 4, 0), children: false);
        TestModels.Near(initial.Rest["spoke-tip"], PoseEvaluator.Rest(ResolvedModel.From(source.Model, variant)).World("spoke-tip"));
    }

    [Fact]
    public void ImageExportKeepsRecoloringHidingAndPackageImageNamesSeparate()
    {
        var source = SpineImport2D.Parse(Rig, "wheel", "Wheel", _ => Image()); Store(source.Images);
        source.Model.Parts[0].Material = source.Model.Parts[0].RenderMaterial with { Fill = "#804020", Tint = "ffffffff" };
        source.Model.Parts[1].Hidden = true;
        var model = ResolvedModel.From(source.Model); model.TextureRoot = _root;
        var first = SpineExport2D.Build(model, source.Animations);
        var returned = SpineImport2D.Parse(first.Json, "returned", "Returned", name => first.Images["images/" + name + ".png"]);
        Assert.Equal("804020ff", returned.Model.Parts[0].RenderMaterial.Tint);
        Assert.EndsWith("00", returned.Model.Parts[1].RenderMaterial.Tint);
        source.Model.Id = "another-wheel";
        var other = ResolvedModel.From(source.Model); other.TextureRoot = _root;
        var second = SpineExport2D.Build(other, []);
        Assert.Empty(first.Images.Keys.Intersect(second.Images.Keys));
    }

    [Fact]
    public void PreviousNativeFormatsUpgradeWithoutChangingPointRigBehavior()
    {
        var source = TestModels.Creature(); var node = JsonNode.Parse(source.ToJson())!; node["version"] = 2;
        var loaded = CharacterModel.FromJson(node.ToJsonString());
        Assert.Equal(CharacterModel.CurrentVersion, loaded.Version);
        var clip = TestModels.Clip(ResolvedModel.From(source)); node = JsonNode.Parse(clip.ToJson())!; node["version"] = 1;
        var motion = MotionClip.FromJson(node.ToJsonString());
        Assert.Equal(MotionClip.CurrentVersion, motion.Version);
        var a = PoseEvaluator.Sample(ResolvedModel.From(source), clip, .5); var b = PoseEvaluator.Sample(ResolvedModel.From(loaded), motion, .5);
        foreach (var control in source.Controls) TestModels.Near(a.World(control.Id), b.World(control.Id));
        node = JsonNode.Parse(new ModelVariant { Id = "other", Name = "Other", Base = source.Id }.ToJson())!; node["version"] = 2;
        Assert.Equal(ModelVariant.CurrentVersion, ModelVariant.FromJson(node.ToJsonString()).Version);
    }

    [Theory]
    [InlineData("person")]
    [InlineData("quadruped")]
    public void ShippedCharactersAndAllTheirAnimationsCanReturnAsImageRigs(string id)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot); Assert.Empty(catalog.Errors);
        var model = catalog.Resolve(id); var clips = catalog.Animations.Values.Where(c => c.Model == id).ToList();
        var package = SpineExport2D.Build(model, clips);
        var imported = SpineImport2D.Parse(package.Json, "returned", "Returned", name => package.Images["images/" + name + ".png"]);
        var returned = ResolvedModel.From(imported.Model);
        Assert.Equal(clips.Count, imported.Animations.Count);
        for (var i = 0; i < clips.Count; i++)
        {
            var clip = clips[i]; var motion = imported.Animations[i];
            foreach (var time in new[] { 0, MathF.Floor(clip.Duration * 30) / 60, clip.Duration })
            {
                var expected = PoseEvaluator.Sample(model, clip, time); var actual = PoseEvaluator.Sample(returned, motion, time);
                foreach (var bone in model.Order)
                {
                    var other = imported.Model.Controls.Single(c => c.Name == bone.Id);
                    // Depth is converted to slot order; compare the screen-plane motion.
                    var a = expected.World(bone.Id); var b = actual.World(other.Id);
                    TestModels.Near(new(a.X, a.Y, 0), new(b.X, b.Y, 0), 2e-4f, clip.Id + " " + bone.Id);
                }
            }
        }
    }

    private void Store(IReadOnlyDictionary<string, byte[]> images)
    {
        foreach (var (path, data) in images) { var full = Path.Combine(_root, path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); File.WriteAllBytes(full, data); }
    }
    private static void MatrixNear(Matrix3x2 a, Matrix3x2 b)
    {
        Assert.InRange(MathF.Abs(a.M11 - b.M11), 0, 2e-5f); Assert.InRange(MathF.Abs(a.M12 - b.M12), 0, 2e-5f);
        Assert.InRange(MathF.Abs(a.M21 - b.M21), 0, 2e-5f); Assert.InRange(MathF.Abs(a.M22 - b.M22), 0, 2e-5f);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
