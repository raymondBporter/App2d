using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P7b of the character-system migration: the curve-face example converted into the reusable animated face part
/// <c>parts/face-curve.json</c> (ordinary curve controls, pupils squashed by explicit fit tracks instead of rig scale, blink
/// and smile clips with expression bindings and a blink layer on the actor's clock), the conversion's report, verification
/// and rerun behaviour, and the part instanced on a walking Person.
/// </summary>
public sealed class FaceConversionTests : IDisposable
{
    private const string PartId = "face-curve";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "face-conversion-" + Guid.NewGuid().ToString("N"));
    private string Output => Path.Combine(_root, "scratch");

    public FaceConversionTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, "authored", Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private string Authored => Path.Combine(_root, "authored");

    [Fact]
    public void TheShippedFacePartIsTheConversionOfTheCurveFaceModelAndTheSourceIsUntouched()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.Empty(catalog.Errors);
        var shipped = catalog.Parts[PartId];
        Assert.True(shipped.IsAnimated);
        var clips = catalog.Animations.Values.Where(c => c.Model == "curve-face").OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var (rebuilt, report) = FaceConversion.Build(catalog.Models["curve-face"], clips, "head", FaceConversionPlans.CurveFace(PartId));
        Assert.Equal(File.ReadAllText(catalog.PathOf(PartId)), rebuilt.ToJson());
        // Eye squash is an explicit artwork fit on the pupils (baseline 7.3), never a rig scale; the affine eyes are plain point controls.
        Assert.DoesNotContain(shipped.Clips.SelectMany(c => c.Tracks), t => t.Kind is MotionClip.ScaleKind or MotionClip.ShearKind);
        Assert.Equal(["left-pupil", "right-pupil"], shipped.Clip("curve-face-blink")!.Tracks.Where(t => t.Kind == MotionClip.FitKind).Select(t => t.Target).Order());
        Assert.All(shipped.Controls, c => Assert.Equal(RigNodeKinds.Point, c.Node));
        Assert.Equal(27, shipped.Controls.Count);
        Assert.Equal(11, shipped.Parts.Count);
        Assert.Equal(9, shipped.Clips.Count);
        Assert.Equal(["expression", "blink"], shipped.Layers.Select(l => l.Id));
        Assert.Equal(PartClocks.Local, shipped.Layers[1].Clock);
        Assert.Contains("clip 'curve-face-blink': scale track on 'left-eye' -> fit track(s) on 'left-pupil'", report);
        Assert.Contains(report, line => line.StartsWith("control 'left-eye': legacy affine node (translation only) -> point control", StringComparison.Ordinal));
        // The source model and its clips are the originals: still at version 5 with the affine eyes and scale tracks.
        Assert.Contains("\"version\": 5", File.ReadAllText(catalog.PathOf("curve-face")));
        Assert.Equal(2, catalog.Models["curve-face"].Controls.Count(c => c.Node == RigNodeKinds.Affine));
        Assert.Contains(catalog.Animations["curve-face-blink"].Tracks, t => t.Kind == MotionClip.ScaleKind);
        // Every converted clip reproduces the source model within the recorded tolerances at sampled instants.
        Assert.Empty(FaceConversion.Verify(catalog.Resolve("curve-face"), clips, "head", shipped, 1e-4f, 1e-3f));
    }

    [Fact]
    public void ConversionWritesOnAScratchTreeWithAReportAndRerunsWithoutClobbering()
    {
        File.Delete(Path.Combine(Authored, "parts", PartId + ".json"));
        var first = FaceConversion.Convert(Authored, "curve-face", "head", FaceConversionPlans.CurveFace(PartId), Output);
        Assert.True(first.Converted, string.Join("; ", first.Differences));
        Assert.StartsWith("converted:", first.Status);
        var output = Path.Combine(Output, "parts", PartId + ".json");
        Assert.True(File.Exists(output) && File.Exists(Path.Combine(Output, "face-conversion-report.md")));
        Assert.False(File.Exists(Path.Combine(Authored, "parts", PartId + ".json")));
        Assert.Equal(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "parts", PartId + ".json")), File.ReadAllText(output));
        var again = FaceConversion.Convert(Authored, "curve-face", "head", FaceConversionPlans.CurveFace(PartId), Output);
        Assert.StartsWith("unchanged:", again.Status);
        var edited = PartAsset.Load(output);
        edited.Name = "Edited after conversion";
        edited.Save(output);
        var kept = FaceConversion.Convert(Authored, "curve-face", "head", FaceConversionPlans.CurveFace(PartId), Output);
        Assert.StartsWith("kept:", kept.Status);
        Assert.Equal("Edited after conversion", PartAsset.Load(output).Name);
        // A plan naming something the face does not convert, or a clip that reaches outside it, is refused by name.
        var badPlan = FaceConversionPlans.CurveFace(PartId);
        badPlan.Groups["eyes"] = ["head"];
        var broken = FaceConversion.Convert(Authored, "curve-face", "head", badPlan, Path.Combine(Output, "broken"));
        Assert.Equal("failed", broken.Status);
        Assert.Contains("Plan group 'eyes' names head", broken.Differences[0]);
        var missing = FaceConversion.Convert(Authored, "curve-face", "nose", FaceConversionPlans.CurveFace(PartId), Path.Combine(Output, "missing"));
        Assert.Contains("has no control 'nose'", missing.Differences[0]);
    }

    [Fact]
    public void AWalkingPersonBlinksWhileSmilingSwapsFacesAndFollowsHeadWidth()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var face = catalog.Parts[PartId];
        var other = PartAsset.FromJson(face.ToJson());
        other.Id = "face-curve-b";
        other.Ink = "#8b0000";
        foreach (var ownClip in other.Clips)
        {
            ownClip.Model = other.Id;
        }

        other.Validate();
        // The shipped Person wears the baked face (P7c); this test wears the curve face in its place, so the shipped instance,
        // its fit bindings and the looks' words go first.
        var person = CharacterModel.FromJson(catalog.Models["person"].ToJson());
        person.Attachments.RemoveAll(a => a.Id == "face");
        foreach (var parameter in person.Parameters)
        {
            parameter.Bindings.RemoveAll(b => b.Target.Attachment == "face");
        }

        foreach (var look in person.Looks)
        {
            look.Expressions.Remove("face");
        }

        Func<string, PartAsset?> library = id => id == face.Id ? face : id == other.Id ? other : null;
        var instance = ModelAuthoring.AddAttachment(person, PartId, Humanoid.HeadSocket, library, "face");
        instance.Scale = .55f;
        instance.X = .065f;
        // Head width reaches the face through the same parameter that sizes the head part; the layout policy repositions the features.
        ModelAuthoring.AddBinding(person, "head", new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment("face", RigProperties.FitX) });
        ModelAuthoring.AddBinding(person, "head", new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment("face", RigProperties.FitY) });
        var resolved = ResolvedModel.From(person, null, library);
        var walk = catalog.Animations["person-walk"];
        var clip = ClipAuthoring.Duplicate(walk, "smile-walk", "Smile walk");
        ClipAuthoring.SetPlay(clip, "face", 0, "smile");
        clip.Validate(resolved);
        var rig = resolved.Attachment("face")!.Rig!;
        // At 0.9 s of the walk the mouth is mid-smile (the smile clip at 0.9 s) while the eyes are closed by the blink layer
        // at the actor's clock 1.0 s; the body walks on and the pupils collapse through their fit, not through any rig scale.
        var pose = PoseEvaluator.Sample(resolved, clip, .9, repeat: true, new() { InPlace = true, Clock = 1 });
        var evaluated = pose.Attachments["face"];
        var smile = PoseEvaluator.Sample(rig, face.Clip("curve-face-smile")!, .9, repeat: true);
        var blink = PoseEvaluator.Sample(rig, face.Clip("curve-face-blink")!, 1, repeat: true);
        TestModels.Near(smile.Points["mouth-bend"], evaluated.Pose.Points["mouth-bend"], 1e-5f);
        TestModels.Near(blink.Points["left-eye-upper-bend"], evaluated.Pose.Points["left-eye-upper-bend"], 1e-5f);
        Assert.Equal(new Vector2(1, 0), evaluated.Pose.Fits["left-pupil"]);
        Assert.Equal("smile", evaluated.Expression);
        Assert.NotEqual(PoseEvaluator.Rest(resolved).Points[Humanoid.LeftFoot], pose.Points[Humanoid.LeftFoot]);
        var open = PoseEvaluator.Sample(resolved, clip, .9, repeat: true, new() { InPlace = true, Clock = .4 }).Attachments["face"];
        Assert.False(open.Pose.Fits.ContainsKey("left-pupil"));
        TestModels.Near(smile.Points["mouth-bend"], open.Pose.Points["mouth-bend"], 1e-5f);
        // Swapping the face asset on the instance changes what is worn, not the base part or the other asset.
        ModelAuthoring.SetAttachmentAsset(person, "face", other.Id, library);
        var swapped = ResolvedModel.From(person, null, library);
        Assert.Equal("#8b0000", swapped.Attachment("face")!.Asset.Ink);
        Assert.Equal("smile", PoseEvaluator.Sample(swapped, clip, .9, repeat: true).Attachments["face"].Expression);
        Assert.Equal("#222b32", face.Ink);
        ModelAuthoring.SetAttachmentAsset(person, "face", PartId, library);
        // A wider head: the head parameter scales the features' positions (layout fit) and leaves each piece's own size alone.
        var wide = new ModelVariant { Id = "wide", Name = "Wide", Base = person.Id, Parameters = { ["head"] = 1.5f } };
        var wideResolved = ResolvedModel.From(person, wide, library);
        var narrow = PoseEvaluator.Rest(resolved).Attachments["face"];
        var broad = PoseEvaluator.Rest(wideResolved).Attachments["face"];
        Assert.Equal(new Vector2(1.5f * .55f / .55f, 1.5f), new Vector2(broad.LayoutFit.X / narrow.LayoutFit.X, broad.LayoutFit.Y / narrow.LayoutFit.Y));
        var narrowSpan = narrow.World("right-eye").X - narrow.World("left-eye").X;
        var broadSpan = broad.World("right-eye").X - broad.World("left-eye").X;
        Assert.Equal(1.5f, broadSpan / narrowSpan, 4);
        var narrowPupil = PartContours2D.Contour(face.Parts.First(p => p.Id == "left-pupil"), narrow);
        var broadPupil = PartContours2D.Contour(face.Parts.First(p => p.Id == "left-pupil"), broad);
        Assert.Equal(narrowPupil.Max(p => p.X) - narrowPupil.Min(p => p.X), broadPupil.Max(p => p.X) - broadPupil.Min(p => p.X), 3);
        Assert.Equal(PropertySource.ParameterBinding, wideResolved.Provenance(PropertyRef.OfAttachment("face", RigProperties.FitX)));
    }
}
