using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P7a of the character-system migration: animated part payloads instanced with their own rig, layers and clocks inside a
/// wearer's pose, the wearer's namespaced tracks and selection channel, expression bindings, and the part channels (fit,
/// show). The proof part is a tail, not a face: the same primitives serve any animated attachment.
/// </summary>
public sealed class AnimatedPartTests
{
    private const string Tail = "tail", Instance = "tail", Socket = Humanoid.BodySocket;

    internal static PartAsset TailAsset(string fitting = PartFittings.Layout) => PartSeeds.Tail(fitting);

    private static Func<string, PartAsset?> Library(params PartAsset[] parts) => id => parts.FirstOrDefault(p => p.Id == id);

    private static (CharacterModel Model, PartAsset Tail, ResolvedModel Resolved) Wearer(string fitting = PartFittings.Layout)
    {
        var person = PersonTemplate.Model();
        var tail = TailAsset(fitting);
        ModelAuthoring.AddAttachment(person, Tail, Socket, Library(tail), Instance);
        return (person, tail, ResolvedModel.From(person, null, Library(tail)));
    }

    private static MotionClip Walk() => MotionClip.FromJson(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "animations", "person-walk.json")));

    [Fact]
    public void AnAnimatedPartRoundTripsAndRefusesBrokenRigsLayersAndBindingsByName()
    {
        var tail = TailAsset();
        var json = tail.ToJson();
        Assert.Contains("\"payload\": \"animated\"", json);
        var read = PartAsset.FromJson(json);
        Assert.Equal(json, read.ToJson());
        Assert.True(read.IsAnimated && read.Exposes("tip") && !read.Exposes("root"));
        Assert.Equal(ChannelSemantics.ArtworkFit, ChannelSemantics.OfTrack(MotionClip.FitKind, null));
        Assert.Equal(ChannelSemantics.EnabledState, ChannelSemantics.OfTrack(MotionClip.ShowKind, null));

        static string Error(Action<PartAsset> breaks)
        {
            var broken = TailAsset();
            breaks(broken);
            return Assert.Throws<InvalidDataException>(broken.Validate).Message;
        }
        Assert.Contains("parent cycle", Error(a => { a.Controls[0].Parent = "tip"; }));
        Assert.Contains("duplicate control 'tip'", Error(a => a.Controls.Add(new() { Id = "tip", Parent = "root" })));
        Assert.Contains("neither a rig control nor a piece", Error(a => a.Exposed.Add("tail-feather")));
        Assert.Contains("no control group 'ears'", Error(a => a.Layers[1].Mask = "ears"));
        Assert.Contains("needs a mask", Error(a => a.Layers[1].Mask = null));
        Assert.Contains("no clip 'curl'", Error(a => a.Expressions["curl"] = new() { Clip = "curl" }));
        Assert.Contains("hiding word", Error(a => a.Expressions[PartAsset.None] = new()));
        Assert.Contains("unknown clock", Error(a => a.Layers[0].Clock = "sidereal"));
        Assert.Contains("is not bound", Error(a => a.Expression = "sulk"));
        Assert.Contains("authored for 'dog'", Error(a => a.Clips[0].Model = "dog"));
        Assert.Contains("a static payload carries no rig", Error(a => a.Payload = PartPayloads.Static));
        Assert.Contains("unknown fitting", Error(a => a.Fitting = "squash"));
    }

    [Fact]
    public void AWearersClipRefusesUnexposedStaticAndUnboundNestedTargetsWithAReferencePath()
    {
        var (person, tail, resolved) = Wearer();
        var clip = ClipAuthoring.New(resolved, "wag-walk", "Wag walk");
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "tail/tip"), 0, new(.05f, 0, 0));
        ClipAuthoring.SetPlay(clip, Instance, 0, "flick");
        clip.Validate(resolved);

        static string Error(ResolvedModel model, Action<MotionClip> breaks, ResolvedModel resolved)
        {
            var broken = ClipAuthoring.New(resolved, "broken", "Broken");
            breaks(broken);
            return Assert.Throws<InvalidDataException>(() => broken.Validate(model)).Message;
        }
        Assert.Contains("path: person > attachments > tail > part tail > exposed", Error(resolved, c => ClipAuthoring.SetKey(c, new(MotionClip.TranslateKind, "tail/root"), 0, Vector3.UnitX), resolved));
        Assert.Contains("path: person > attachments > mane", Error(resolved, c => ClipAuthoring.SetKey(c, new(MotionClip.TranslateKind, "mane/root"), 0, Vector3.UnitX), resolved));
        Assert.Contains("binds no expression 'curl'", Error(resolved, c => ClipAuthoring.SetPlay(c, Instance, 0, "curl"), resolved));
        Assert.Contains("sockets or chains", Error(resolved, c => c.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "tail/tip" }), resolved));
        // A static instance exposes nothing and plays nothing.
        var glove = PartAssetTests.Glove();
        ModelAuthoring.AddAttachment(person, "glove", Humanoid.LeftHandSocket, Library(tail, glove), "glove");
        var withGlove = ResolvedModel.From(person, null, Library(tail, glove));
        Assert.Contains("static part 'glove'", Error(withGlove, c => ClipAuthoring.SetKey(c, new(MotionClip.TranslateKind, "glove/palm"), 0, Vector3.UnitX), withGlove));
        Assert.Contains("plays nothing", Error(withGlove, c => ClipAuthoring.SetPlay(c, "glove", 0, "wag"), withGlove));
        // A group may own an instance whole; an instance default must be a bound word.
        person.Groups.Add(new() { Id = "tail-owner", Targets = [Instance] });
        person.Validate();
        person.Attachments[0].Expression = "sulk";
        Assert.Contains("binds no expression 'sulk'", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person, null, Library(tail, glove))).Message);
    }

    [Fact]
    public void TheInstanceEvaluatesItsOwnRigOnItsSocketInsideTheWearersPose()
    {
        var (_, tail, resolved) = Wearer();
        var walk = Walk();
        var pose = PoseEvaluator.Sample(resolved, walk, .3, repeat: true);
        var evaluated = pose.Attachments[Instance];
        var rig = resolved.Attachment(Instance)!.Rig!;
        // The nested pose is the part's own clip sampled on the shared clock, placed by the instance placement on the socket.
        var expected = PoseEvaluator.Sample(rig, tail.Clips[0], .3, repeat: true, new() { InPlace = true });
        var actor = new ActorPose(pose, Vector2.Zero, 1);
        var placement = resolved.Attachment(Instance)!.Placement(actor.SocketPlacement(resolved.Attachment(Instance)!.Socket));
        foreach (var control in rig.Order)
        {
            TestModels.Near(expected.Points[control.Id], evaluated.Pose.Points[control.Id]);
            TestModels.Near(placement.At(expected.Points[control.Id]), evaluated.World(control.Id));
        }
        Assert.Equal(expected.Angles["root"], evaluated.Pose.Angles["root"], 5);
        Assert.NotEqual(0, evaluated.Pose.Angles["root"]);
        Assert.Equal("wag", pose.Expressions[Instance]);
        Assert.False(evaluated.Hidden);
        // Drawing, contours and picking read the nested pose: the tuft is where the tip is, in the wearer's frame.
        var contour = PartContours2D.Contour(tail.Parts[1], evaluated);
        var center = contour.Aggregate(Vector3.Zero, (a, b) => a + b) / contour.Count;
        TestModels.Near(evaluated.World("tip"), center, 1e-3f);
        Assert.Equal(0, PartPicking2D.Distance(resolved.Attachment(Instance)!, pose, evaluated.World("tip")), 3);
        // Seeking is pure: the same instant again, and after a later one, gives the same pose; part markers reach nothing.
        var again = PoseEvaluator.Sample(resolved, walk, .3, repeat: true);
        _ = PoseEvaluator.Sample(resolved, walk, .9, repeat: true);
        var oncemore = PoseEvaluator.Sample(resolved, walk, .3, repeat: true);
        foreach (var control in rig.Order)
        {
            TestModels.Near(evaluated.Pose.Points[control.Id], again.Attachments[Instance].Pose.Points[control.Id], 1e-7f);
            TestModels.Near(evaluated.Pose.Points[control.Id], oncemore.Attachments[Instance].Pose.Points[control.Id], 1e-7f);
        }
        Assert.DoesNotContain(walk.Markers, m => m.Id == "swish");
    }

    [Fact]
    public void TheWearersNamespacedTracksOwnOnlyWhatTheyKeyAndTheInverseLandsThere()
    {
        var (_, tail, resolved) = Wearer();
        var clip = ClipAuthoring.New(resolved, "tip-key", "Tip key", loop: false);
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "tail/tip"), 0, new(.07f, .02f, 0));
        clip.Validate(resolved);
        var pose = PoseEvaluator.Sample(resolved, clip, .3);
        var evaluated = pose.Attachments[Instance];
        var rig = resolved.Attachment(Instance)!.Rig!;
        var plain = PoseEvaluator.Sample(rig, tail.Clips[0], .3, repeat: true, new() { InPlace = true });
        // The root still wags (the wearer owns only the tip); the tip carries the wearer's offset in its parent's axes.
        Assert.Equal(plain.Angles["root"], evaluated.Pose.Angles["root"], 5);
        TestModels.Near(plain.Points["mid"], evaluated.Pose.Points["mid"]);
        var midFrame = plain.Placement("mid");
        TestModels.Near(midFrame.At(new Vector3(.07f, .2f + .02f, 0)), evaluated.Pose.Points["tip"], 1e-5f);
        // The pose inverse on a nested control writes the namespaced key that puts it at the asked world position.
        var target = evaluated.World("tip") + new Vector3(.03f, -.05f, 0);
        var channel = ClipAuthoring.Pose(resolved, clip, pose, .3f, "tail/tip", target);
        Assert.Equal(new Channel(MotionClip.TranslateKind, "tail/tip"), channel);
        TestModels.Near(target, PoseEvaluator.Sample(resolved, clip, .3).Attachments[Instance].World("tip"), 1e-4f);
        Assert.Equal([new Channel(MotionClip.TranslateKind, "tail/tip"), new(MotionClip.RotateKind, "tail/tip"), new(MotionClip.ScaleKind, "tail/tip")], ClipAuthoring.ChannelsForControl(resolved, "tail/tip"));
        // A fit and a show key on the exposed piece reach the nested part.
        ClipAuthoring.SetKey(clip, new(MotionClip.FitKind, "tail/tuft"), 0, new(1, -1, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, "tail/tuft"), 0, new(0, 0, 0));
        clip.Validate(resolved);
        var fitted = PoseEvaluator.Sample(resolved, clip, .3).Attachments[Instance];
        Assert.Equal(new Vector2(2, 0), fitted.Pose.Fits["tuft"]);
        Assert.Contains("tuft", fitted.Pose.HiddenParts);
        Assert.DoesNotContain(fitted.DrawnParts(), p => p.Id == "tuft");
    }

    [Fact]
    public void ClocksAreExplicitSharedMappedAndLocal()
    {
        var (person, tail, resolved) = Wearer();
        var walk = Walk();
        var rig = resolved.Attachment(Instance)!.Rig!;
        float Root(ResolvedModel model, double seconds, double clock) => PoseEvaluator.Sample(model, walk, seconds, repeat: true, new() { Clock = clock }).Attachments[Instance].Pose.Angles["root"];
        float Plain(double seconds) => PoseEvaluator.Sample(rig, tail.Clips[0], seconds, repeat: true).Angles["root"];
        Assert.Equal(Plain(1.7), Root(resolved, 1.7, 99), 5);
        tail.Layers[0].Clock = PartClocks.Mapped;
        tail.Layers[0].Rate = 2;
        tail.Layers[0].Offset = .1f;
        var mapped = ResolvedModel.From(person, null, Library(tail));
        Assert.Equal(Plain(.1 + 2 * 1.7), Root(mapped, 1.7, 99), 5);
        tail.Layers[0].Clock = PartClocks.Local;
        tail.Layers[0].Offset = 0;
        var local = ResolvedModel.From(person, null, Library(tail));
        Assert.Equal(Plain(99), Root(local, 1.7, 99), 5);
        Assert.Equal(Plain(0), Root(local, 1.7, 0), 5);
    }

    [Fact]
    public void ExpressionsResolveFromRequestChannelInstanceAndAssetInThatOrder()
    {
        var (person, tail, resolved) = Wearer();
        var rig = resolved.Attachment(Instance)!.Rig!;
        var clip = ClipAuthoring.New(resolved, "flick-walk", "Flick walk", duration: 2, loop: false);
        ClipAuthoring.SetPlay(clip, Instance, .5f, "flick");
        ClipAuthoring.SetPlay(clip, Instance, 1.5f, "still");
        clip.Validate(resolved);
        // Before the first key the first key holds; the flick runs from its key (.25 s in at .75 s) on the twitch layer, the wag goes on beneath.
        var at = PoseEvaluator.Sample(resolved, clip, .75, input: new() { Clock = 40 }).Attachments[Instance];
        Assert.Equal("flick", at.Expression);
        var wagOnly = PoseEvaluator.Sample(rig, tail.Clips[0], .75, repeat: true);
        Assert.Equal(wagOnly.Angles["root"], at.Pose.Angles["root"], 5);
        var tipInMid = wagOnly.Placement("mid").ToLocal(at.Pose.Points["tip"]);
        Assert.Equal(.2f, tipInMid.Y, 4);
        Assert.Equal(.1f, tipInMid.X, 4);
        Assert.Equal("flick", PoseEvaluator.Sample(resolved, clip, .25).Attachments[Instance].Expression);
        // 'still' empties the base layer: the root rests.
        var still = PoseEvaluator.Sample(resolved, clip, 1.75).Attachments[Instance];
        Assert.Equal("still", still.Expression);
        Assert.Equal(0, still.Pose.Angles["root"], 5);
        // A semantic request the asset binds wins over the channel; one it does not bind leaves the channel in force.
        Assert.Equal("tucked", PoseEvaluator.Sample(resolved, clip, .75, input: new("tucked")).Attachments[Instance].Expression);
        Assert.True(PoseEvaluator.Sample(resolved, clip, .75, input: new("tucked")).Attachments[Instance].Hidden);
        Assert.Equal("flick", PoseEvaluator.Sample(resolved, clip, .75, input: new("confused")).Attachments[Instance].Expression);
        // An explicit play with its own seconds wins over everything and owns the clip time.
        var owned = PoseEvaluator.Sample(resolved, clip, .75, input: new("tucked") { Plays = [new(Instance, "flick", .25)] }).Attachments[Instance];
        Assert.Equal("flick", owned.Expression);
        Assert.Equal(.1f, owned.Pose.Placement("mid").ToLocal(owned.Pose.Points["tip"]).X, 4);
        // A pose binding holds one time of its clip whatever the clock says.
        var peak = PoseEvaluator.Sample(resolved, clip, 1.2, input: new("peak") { Clock = 7 }).Attachments[Instance];
        Assert.Equal(PoseEvaluator.Sample(rig, tail.Clips[0], .25).Angles["root"], peak.Pose.Angles["root"], 5);
        // 'none' hides; the instance default and the asset default fill in when nothing selects.
        ClipAuthoring.SetPlay(clip, Instance, 1.5f, PartAsset.None);
        Assert.True(PoseEvaluator.Sample(resolved, clip, 1.75).Attachments[Instance].Hidden);
        Assert.Equal("wag", PoseEvaluator.Rest(resolved).Attachments[Instance].Expression);
        person.Attachments[0].Expression = "still";
        var quiet = ResolvedModel.From(person, null, Library(tail));
        Assert.Equal("still", PoseEvaluator.Rest(quiet).Attachments[Instance].Expression);
        Assert.Equal("still", PoseEvaluator.Rest(quiet).Expressions[Instance]);
        // A masked wearer layer owning the instance selects from half weight; below that the base channel holds.
        var overlay = ClipAuthoring.New(resolved, "flick-over", "Flick over", loop: false);
        ClipAuthoring.SetPlay(overlay, Instance, 0, "tucked");
        overlay.Validate(resolved);
        var mask = new HashSet<string>(StringComparer.Ordinal) { Instance };
        Assert.Equal("tucked", PoseEvaluator.Sample(resolved, clip, .75, input: new() { Overlay = new(overlay, 0, mask, .6f) }).Attachments[Instance].Expression);
        Assert.Equal("flick", PoseEvaluator.Sample(resolved, clip, .75, input: new() { Overlay = new(overlay, 0, mask, .4f) }).Attachments[Instance].Expression);
    }

    [Fact]
    public void FittingIsDeclaredPerAssetAsLayoutOrStretch()
    {
        foreach (var fitting in PartFittings.All)
        {
            var (person, tail, _) = Wearer(fitting);
            person.Attachments[0].FitX = 2;
            var resolved = ResolvedModel.From(person, null, Library(tail));
            var pose = PoseEvaluator.Rest(resolved);
            var evaluated = pose.Attachments[Instance];
            var actor = new ActorPose(pose, Vector2.Zero, 1);
            var socket = actor.SocketPlacement(resolved.Attachment(Instance)!.Socket);
            var rest = PoseEvaluator.Rest(resolved.Attachment(Instance)!.Rig!);
            // Both policies put the fitted origin at the socket; layout repositions controls and keeps the tuft round, stretch scales the frame.
            TestModels.Near(socket.Origin, evaluated.World("root"));
            var contour = PartContours2D.Contour(tail.Parts[1], evaluated);
            var width = contour.Max(p => p.X) - contour.Min(p => p.X);
            var height = contour.Max(p => p.Y) - contour.Min(p => p.Y);
            if (fitting == PartFittings.Layout)
            {
                Assert.Equal(1, evaluated.Placement.XAxis.Length(), 4);
                Assert.Equal(width, height, 3);
                // The default wag has turned the root at its first key; the tip still lands at its nested position with X doubled.
                Assert.NotEqual(rest.Points["tip"], evaluated.Pose.Points["tip"]);
                TestModels.Near(socket.At(new Vector3(evaluated.Pose.Points["tip"].X * 2, evaluated.Pose.Points["tip"].Y, 0)), evaluated.World("tip"));
            }
            else
            {
                Assert.Equal(2, evaluated.Placement.XAxis.Length(), 4);
                Assert.Equal(2 * height, width, 3);
            }
        }
    }

    [Fact]
    public void FitAndShowTracksAreOrdinaryPartChannelsOnTheWearerToo()
    {
        var person = PersonTemplate.Model();
        var resolved = ResolvedModel.From(person);
        var clip = ClipAuthoring.New(resolved, "squint", "Squint", loop: false);
        ClipAuthoring.SetKey(clip, new(MotionClip.FitKind, Humanoid.Head), 0, new(0, -1, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, Humanoid.Body), 0, new(0, 0, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, Humanoid.Body), .5f, new(1, 0, 0));
        clip.Validate(resolved);
        var pose = PoseEvaluator.Sample(clip: clip, model: resolved, seconds: .2);
        var head = resolved.Parts.First(p => p.Id == Humanoid.Head);
        Assert.Equal(new Vector2(1, 0), pose.Fits[Humanoid.Head]);
        var squashed = PartContours2D.Contour(head, resolved, pose);
        Assert.Equal(squashed.Min(p => p.Y), squashed.Max(p => p.Y), 4);
        Assert.False(pose.Visible(resolved.Parts.First(p => p.Id == Humanoid.Body)));
        Assert.True(PoseEvaluator.Sample(resolved, clip, .6).Visible(resolved.Parts.First(p => p.Id == Humanoid.Body)));
        Assert.Contains("unknown part", Assert.Throws<InvalidDataException>(() => { ClipAuthoring.SetKey(clip, new(MotionClip.FitKind, "halo"), 0, Vector3.Zero); clip.Validate(resolved); }).Message);
        clip.Tracks.RemoveAll(t => t.Target == "halo");
        Assert.Contains("0 (hidden) or 1 (shown)", Assert.Throws<InvalidDataException>(() => { ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, Humanoid.Head), 0, new(.5f, 0, 0)); clip.Validate(); }).Message);
    }

    [Fact]
    public void AVariantOverridesAnInstanceExpressionWithProvenanceAndAnEntityClockRunsOn()
    {
        var (person, tail, resolved) = Wearer();
        var variant = new ModelVariant { Id = "quiet", Name = "Quiet", Base = person.Id };
        ModelAuthoring.EditAttachmentOverride(variant, resolved, Instance, i => i.Expression = "still");
        var target = PropertyRef.OfAttachment(Instance, RigProperties.Expression);
        Assert.Equal("still", variant.Override(target)?.Selection);
        var quiet = ResolvedModel.From(person, variant, Library(tail));
        Assert.Equal(PropertySource.Override, quiet.Provenance(target));
        Assert.Equal("still", PoseEvaluator.Rest(quiet).Attachments[Instance].Expression);
        Assert.Equal("wag", PoseEvaluator.Rest(resolved).Attachments[Instance].Expression);
        ModelAuthoring.ResetOverride(variant, target);
        Assert.Equal("wag", PoseEvaluator.Rest(ResolvedModel.From(person, variant, Library(tail))).Attachments[Instance].Expression);
        variant.Overrides.Add(PropertyOverride.Of(target, "sulk"));
        Assert.Contains("binds no expression 'sulk'", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person, variant, Library(tail))).Message);
        // Round trip of the instance's own default through the model file.
        person.Attachments[0].Expression = "flick";
        Assert.Equal("flick", CharacterModel.FromJson(person.ToJson()).Attachments[0].Expression);
    }
}
