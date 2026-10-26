using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P11a: the model bundle conversion onto the canonical rig: point-solved constraints onto the bone solver with pivots where the
/// root's frame is read, translation-only affine nodes to point controls with fit tracks, verification against the source,
/// and the transactional scratch-tree write.
/// </summary>
public sealed class ModelConversionTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static MotionClip CreatureClip(ResolvedModel creature)
    {
        // The arm reaches out while the shoulder turns (the clip keys the chain root's rotation); the leg plants.
        var clip = ClipAuthoring.New(creature, "creature-reach", "Reach", 1, loop: false);
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "shoulder", Keys = [new(), new() { Time = 1, Angle = .6f }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new(), new() { Time = .5f, Value = new(-.2f, -.3f, 0) }, new() { Time = 1, Value = new(.1f, .2f, 0) }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "leg", Keys = [new(), new() { Time = 1, Value = new(.15f, .1f, 0) }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TranslateKind, Target = "body", Keys = [new(), new() { Time = 1, Value = new(0, .05f, 0) }] });
        clip.Validate(creature);
        return clip;
    }

    [Fact]
    public void APointRigConvertsOntoTheBoneSolverWithAPivotWhereTheRootIsReadAndKeepsEveryPositionSocketAndContact()
    {
        var source = TestModels.Creature();
        source.Sockets.Add(new() { Id = "grip", Control = "hand", Frame = "shoulder" });
        source.Sockets.Add(new() { Id = "shoe", Control = "foot", Toward = "knee" });
        source.Validate();
        var clips = new[] { CreatureClip(ResolvedModel.From(source)) };
        var (model, converted, report) = ModelConversion.Build(source, clips, c => c.Id == "arm" ? "upper-arm-bone" : null);
        // The arm's root frames its own target, the grip and the clip's turn: the shoulder stays the pivot, the solved bone hangs beneath it.
        var arm = model.IkChains.Single(c => c.Id == "arm");
        var leg = model.IkChains.Single(c => c.Id == "leg");
        Assert.Equal(ModelChain.BoneSolver, arm.Solver);
        Assert.Equal("upper-arm-bone", arm.Root);
        Assert.Equal("shoulder", arm.Frame);
        Assert.Equal(RigNodeKinds.Point, model.Controls.Single(c => c.Id == "shoulder").Node);
        Assert.Equal("shoulder", model.Controls.Single(c => c.Id == "upper-arm-bone").Parent);
        Assert.Equal("hip", leg.Root);
        Assert.Equal(RigLinkOrientations.Character, model.Controls.Single(c => c.Id == "hand").Link!.Orientation);
        Assert.Equal(RigLinkOrientations.Character, model.Controls.Single(c => c.Id == "foot").Link!.Orientation);
        Assert.Contains(report, line => line.Contains("'shoulder' keeps its authored frame as the pivot"));
        Assert.Contains(report, line => line.Contains("its target frame") && line.Contains("socket 'grip' framed on it") && line.Contains("clip 'creature-reach' keys its rotate"));
        Assert.Equal(CharacterModel.CurrentVersion, model.Version);
        Assert.Equal(source.StructureRevision + 1, model.StructureRevision);
        // The verification the file conversion runs: nothing but the solved bones' frame angles moved.
        var differences = ModelConversion.Verify(source, clips, [], model, converted, _ => null, 1e-4f, 1e-5f, 1e-3f);
        Assert.Empty(differences);
        var was = ResolvedModel.From(source);
        var now = ResolvedModel.From(model);
        foreach (var seconds in new[] { 0, .5, 1 })
        {
            var a = PoseEvaluator.Sample(was, clips[0], seconds);
            var b = PoseEvaluator.Sample(now, converted[0], seconds);
            Assert.Equal(a.Angles["shoulder"], b.Angles["shoulder"], 5);
            Assert.Equal(0, b.Angles["hand"], 5);
            Assert.Equal(0, b.Angles["foot"], 5);
            Assert.NotEqual(b.Angles["upper-arm-bone"], b.Angles["shoulder"]);
            var ga = new ActorPose(a, Vector2.Zero, 1).Socket(source.Sockets[0]);
            var gb = new ActorPose(b, Vector2.Zero, 1).Socket(model.Sockets[0]);
            TestModels.Near(ga.Origin, gb.Origin, 1e-5f, "grip origin");
            TestModels.Near(ga.XAxis, gb.XAxis, 1e-5f, "grip axis follows the pivot's turn");
        }
        // The converted model round-trips, and a bone constraint is not converted twice.
        Assert.Equal(model.ToJson(), CharacterModel.FromJson(model.ToJson()).ToJson());
        Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneConstraint(model, "arm", converted));
    }

    [Fact]
    public void ATranslationOnlyAffineNodeBecomesAPointControlWhoseScaleTracksAreArtworkFitAndShearIsRefused()
    {
        var source = new CharacterModel
        {
            Id = "eyes",
            Name = "Eyes",
            Controls =
            [
                new() { Id = "head", Rest = new(0, 1) },
                new() { Id = "eye", Parent = "head", Node = RigNodeKinds.Affine, Transform = new() { X = .1f, Y = .2f } },
            ],
            Parts =
            [
                new() { Id = "skull", A = "head", Geometry = Ellipse2D.Builder.Create(new(.3f, .3f)), Material = new() { Fill = "#ffffff" } },
                new() { Id = "pupil", A = "eye", Frame = "eye", Geometry = Ellipse2D.Builder.Create(new(.05f, .05f)), Material = new() { Fill = "#000000" } },
            ],
        };
        source.Validate();
        var resolved = ResolvedModel.From(source);
        var blink = ClipAuthoring.New(resolved, "eyes-blink", "Blink", 1, loop: true);
        blink.Tracks.Add(new() { Kind = MotionClip.ScaleKind, Target = "eye", Keys = [new(), new() { Time = .5f, Value = new(0, -1, 0) }, new() { Time = 1 }] });
        blink.Validate(resolved);
        var (model, converted, report) = ModelConversion.Build(source, [blink]);
        var eye = model.Controls.Single(c => c.Id == "eye");
        Assert.Equal(RigNodeKinds.Point, eye.Node);
        Assert.Null(eye.Transform);
        TestModels.Near(new(.1f, 1.2f, 0), eye.Rest.XYZ, 1e-6f);
        var fit = Assert.Single(converted[0].Tracks);
        Assert.Equal((MotionClip.FitKind, "pupil"), (fit.Kind, fit.Target));
        Assert.Contains(report, line => line.Contains("scale track on 'eye' -> fit track(s) on 'pupil'"));
        Assert.Empty(ModelConversion.Verify(source, [blink], [], model, converted, _ => null, 1e-4f, 1e-5f, 1e-3f));
        // Inherited scale or shear is not representable: refused by name with the control's path, never approximated.
        var sheared = CharacterModel.FromJson(source.ToJson());
        sheared.Controls[1].Transform = new() { X = .1f, Y = .2f, ShearX = .3f };
        Assert.Contains("eyes > controls > eye > transform", Assert.ThrowsAny<Exception>(() => ModelConversion.Build(sheared, [])).Message);
        var shearClip = MotionClip.FromJson(blink.ToJson());
        shearClip.Tracks[0].Kind = MotionClip.ShearKind;
        Assert.Contains("shear has no artwork-fit equivalent", Assert.ThrowsAny<Exception>(() => ModelConversion.Build(source, [shearClip])).Message);
    }

    [Fact]
    public void TheShippedLibraryIsCanonicalAndTheFileConversionIsTransactionalAndRerunnable()
    {
        // Every shipped constraint is on the bone solver and no control is an affine node (the P11a conversion reports are the record).
        foreach (var model in Catalog.Models.Values)
        {
            Assert.All(model.IkChains, c => Assert.Equal(ModelChain.BoneSolver, c.Solver));
            Assert.All(model.Controls, c => Assert.NotEqual(RigNodeKinds.Affine, c.Node));
            Assert.Equal(CharacterModel.CurrentVersion, model.Version);
        }
        Assert.All(Catalog.Parts.Values.Where(p => p.IsAnimated), p => Assert.All(p.Constraints.OfType<ModelChain>(), c => Assert.Equal(ModelChain.BoneSolver, c.Solver)));
        var output = Path.Combine(Path.GetTempPath(), "app2d-model-conversion", Guid.NewGuid().ToString("N"));
        try
        {
            // A canonical bundle converts to itself: the model is written once, the clips need nothing, a rerun is unchanged, an edit is kept.
            var first = ModelConversion.Convert(TestModels.AuthoredRoot, "stalker", output);
            Assert.True(first.Converted, string.Join("; ", first.Differences));
            Assert.True(File.Exists(Path.Combine(output, "model-conversion-stalker.md")));
            Assert.Equal("written", first.Files.Single(f => f.Kind == "model").Status);
            Assert.All(first.Files.Where(f => f.Kind == "clip"), f => Assert.StartsWith("unchanged", f.Status));
            Assert.StartsWith("unchanged", ModelConversion.Convert(TestModels.AuthoredRoot, "stalker", output).Status);
            var path = Path.Combine(output, "models", "stalker.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"name\": \"Stalker\"", "\"name\": \"Stalker (edited)\""));
            Assert.StartsWith("kept", ModelConversion.Convert(TestModels.AuthoredRoot, "stalker", output).Status);
            Assert.Contains("(edited)", File.ReadAllText(path));
            Assert.Equal(CharacterModel.CurrentVersion, Catalog.Models["stalker"].Version);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public void TheSeedPersonIsTheShippedStructure()
    {
        var seed = PersonTemplate.Model(face: true);
        var shipped = Catalog.Models["person"];
        Assert.Equal(shipped.Controls.Select(c => (c.Id, c.Node, c.Parent, c.Link?.Orientation)), seed.Controls.Select(c => (c.Id, c.Node, c.Parent, c.Link?.Orientation)));
        Assert.Equal(shipped.IkChains.Select(c => (c.Id, c.Root, c.Solver, c.Frame)), seed.IkChains.Select(c => (c.Id, c.Root, c.Solver, c.Frame)));
        Assert.Equal(shipped.Parameters.Select(p => (p.Id, p.Bindings.Count)), seed.Parameters.Select(p => (p.Id, p.Bindings.Count)));
    }
}
