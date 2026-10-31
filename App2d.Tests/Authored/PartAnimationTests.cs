using App2d.CharacterStudio.Editing;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Physics;
using App2d.Core.Rendering.Characters;
using App2d.Core.Shapes;
using App2d.Gameplay.World;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class PartAnimationTests
{
    private static Part Panel(bool loop = true) => new()
    {
        Id = "animated-panel",
        Name = "Animated panel",
        Payload = PartPayloads.Animated,
        Controls = [new() { Id = "root", Node = RigNodeKinds.Bone }],
        Exposed = ["panel"],
        Geometry = [new() { Id = "panel", A = "root", Frame = "root", OffsetX = .3f,
            Geometry = Rectangle2D.Builder.FromSize(2, 1), Material = new() { Alpha = .5f, TextureAnimation = new()
            {
                Loop = loop, Frames = [new() { Texture = "small.png", Seconds = .25f },
                    new() { Texture = "wide.png", Seconds = .75f, Source = new() { X = .5f, Width = .5f } }]
            } } }],
        Layers = [new() { Id = "base", Clip = "sway" }],
        Clips = [new() { Id = "sway", Name = "Sway", Model = "animated-panel", Duration = 2,
            Tracks = [
                new() { Kind = MotionClip.RotateKind, Target = "root", Keys = [new() { Time = 0 }, new() { Time = 2, Angle = .6f }] },
                new() { Kind = MotionClip.AlphaKind, Target = "panel", Keys = [new() { Time = 0, Value = Vector3.UnitX }, new() { Time = 2, Value = new(.2f, 0, 0) }] },
                new() { Kind = MotionClip.TintKind, Target = "panel", Keys = [new() { Time = 0, Value = Vector3.One }, new() { Time = 2, Value = new(.5f, .8f, 1) }] }
            ] }]
    };

    private static ResolvedAttachment Resolve(Part asset) => ResolvedAttachment.Resolve(new() { Id = "panel", Asset = asset.Id, Socket = "mount" },
        _ => asset, new Dictionary<string, ModelSocket> { ["mount"] = new() { Id = "mount" } }, "test");

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void ASeekingBoneKeepsTextureSizeAnchorColorAndBothFacingsDeterministic(int facing)
    {
        var asset = Panel();
        var before = asset.ToJson();
        var attachment = Resolve(asset);
        var drawing = new PuppetDrawing();
        var pose = PoseEvaluator.Preview(attachment, LayeredFrame2D.Identity, .5);
        drawing.AddAttachment(pose);
        Assert.Equal("wide.png", Assert.Single(drawing.Batches).Texture);
        var vertices = drawing.Mesh.Vertices.ToArray();
        Assert.Equal(6, vertices.Length);
        Assert.All(vertices, v => Assert.InRange(v.Color.A, (byte)101, (byte)102));
        Assert.InRange(vertices[0].Color.R, (byte)222, (byte)224);
        var frame = PartAttachment2D.FrameOf(attachment.Parts[0], pose.World, pose.Frame, pose.Rest);
        var actor = new ActorPose(pose.Pose, new(5, 3), facing);
        foreach (var vertex in vertices)
        {
            var local = frame.ToLocal(new Vector3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z)).XY;
            Assert.InRange(MathF.Abs(local.X), .9999f, 1.0001f);
            Assert.InRange(MathF.Abs(local.Y), .4999f, .5001f);
        }
        var worldA = actor.Place(new(vertices[0].Position.X, vertices[0].Position.Y, vertices[0].Position.Z));
        var worldB = actor.Place(new(vertices[1].Position.X, vertices[1].Position.Y, vertices[1].Position.Z));
        Assert.InRange(Vector3.Distance(worldA, worldB), 1.9999f, 2.0001f);
        Assert.Equal(.5f, vertices.Min(v => v.TextureCoordinate.X));
        Assert.Equal(1f, vertices.Max(v => v.TextureCoordinate.X));
        drawing.Clear();
        drawing.AddAttachment(PoseEvaluator.Preview(attachment, LayeredFrame2D.Identity, 1.6));
        drawing.Clear();
        drawing.AddAttachment(PoseEvaluator.Preview(attachment, LayeredFrame2D.Identity, .5));
        Assert.Equal(vertices, drawing.Mesh.Vertices.ToArray());
        Assert.Equal(before, asset.ToJson());
        Assert.Equal(before, Part.FromJson(before).ToJson());
    }

    [Theory]
    [InlineData(true, "small.png")]
    [InlineData(false, "wide.png")]
    public void TextureSamplingMatchesTheSharedPlayerAtBoundariesAndDoesNotFreezeAStaticShader(bool loop, string end)
    {
        var part = Panel(loop) with { Payload = PartPayloads.Static, Controls = [], Exposed = [], Layers = [], Clips = [] };
        part.Geometry[0].A = part.Geometry[0].Frame = PartLocation.Origin;
        var frames = part.Geometry[0].Material!.TextureAnimation!.Compile()!;
        var player = new AnimationPlayer2D<TextureFrame>();
        player.Play(frames);
        var shader = new PartShader2D(part);
        foreach (var time in new[] { 0f, .25f, .5f, 1f })
        {
            if (time > 0) player.Update(time - player.ElapsedSeconds);
            shader.SetTime(time);
            Assert.Equal(frames.Sample(time).Texture, player.CurrentFrame.Texture);
            Assert.Equal(player.CurrentFrame.Texture, Assert.Single(shader.Drawing.Batches).Texture);
        }
        Assert.Equal(end, frames.Sample(1).Texture);
        shader.SetTime(.1);
        Assert.Equal("small.png", Assert.Single(shader.Drawing.Batches).Texture);
    }

    [Fact]
    public void NamespacedMaterialTracksBlendWithoutMirroringAndHeldVisibilityUsesTheSameSampler()
    {
        var part = Panel();
        var model = new CharacterModel
        {
            Id = "wearer",
            Name = "Wearer",
            Controls = [new() { Id = "root" }],
            Sockets = [new() { Id = "mount", Control = "root" }],
            Attachments = [new() { Id = "screen", Asset = part.Id, Socket = "mount" }]
        };
        var resolved = ResolvedModel.From(model, partOf: _ => part);
        var clip = ClipAuthoring.New(resolved, "fade", "Fade");
        ClipAuthoring.SetKey(clip, new(MotionClip.AlphaKind, "screen/panel"), 0, new(.3f, 0, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.TintKind, "screen/panel"), 0, new(.2f, .4f, .6f));
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, "screen/panel"), 0, Vector3.Zero);
        ClipAuthoring.SetKey(clip, new(MotionClip.ShowKind, "screen/panel"), .5f, Vector3.UnitX);
        clip.Validate(resolved);
        var hidden = PoseEvaluator.Sample(resolved, clip, .49, input: new() { ReverseHorizontalMotion = true });
        Assert.False(hidden.Attachments["screen"].Pose.Visible(part.Geometry[0]));
        var shown = PoseEvaluator.Sample(resolved, clip, .5, input: new() { ReverseHorizontalMotion = true });
        Assert.True(shown.Attachments["screen"].Pose.Visible(part.Geometry[0]));
        Assert.Equal(new Vector4(.2f, .4f, .6f, .3f), shown.Attachments["screen"].Pose.Tints["panel"]);
        Assert.Equal(0, ClipAuthoring.Value(clip, new(MotionClip.ShowKind, "screen/panel"), .49f).Value.X);
        var inner = resolved.Attachments[0].Rig!;
        var overlay = new MotionClip { Id = "tint", Name = "Tint", Model = part.Id, Tracks = [new() { Kind = MotionClip.AlphaKind, Target = "panel", Keys = [new() { Value = Vector3.UnitX }] }] };
        var layered = PoseEvaluator.Sample(inner, part.Clips[0], 1, input: new() { Overlay = new(overlay, 0, new HashSet<string> { "panel" }, .5f) });
        Assert.InRange(layered.Tints["panel"].W, .7999f, .8001f);
    }

    [Fact]
    public void StaticPartPreviewSeeksTextureAndPathAndRoundTripsUndoWithoutSteppingPhysics()
    {
        var root = Path.Combine(Path.GetTempPath(), "part-animation-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "parts"));
            var part = Panel() with
            {
                Payload = PartPayloads.Static,
                Controls = [],
                Exposed = [],
                Layers = [],
                Clips = [],
                Motion = new() { Curve = CubicBezier2D.Builder.Create(Vector2.Zero, new(0, 1), new(2, 1), new(2, 0)), PeriodSeconds = 2 }
            };
            part.Geometry[0].A = part.Geometry[0].Frame = PartLocation.Origin;
            part.Save(Path.Combine(root, "parts", part.Id + ".json"));
            var session = new EditorSession(AuthoringWorkspace.Open(root));
            session.Open(part.Id);
            Assert.Equal(2, session.TransportClip!.Duration);
            session.Seek(.5f);
            var subject = Assert.Single(session.Scene());
            var drawing = new PuppetDrawing(); drawing.Build(subject.Model, subject.Pose);
            Assert.Equal("wide.png", Assert.Single(drawing.Batches).Texture);
            var expected = part.Motion.CreateMotion().Position(.5);
            Assert.Equal(expected, subject.Model.Attachments[0].Socket.Transform.Translation);
            Assert.False(session.Transport.Playing);
            var doc = session.PartDocument!;
            Assert.True(session.Edit(doc, () => doc.Asset.Geometry[0].Material = doc.Asset.Geometry[0].Material! with { Alpha = .2f }));
            doc.Undo();
            Assert.Equal(.5f, doc.Asset.Geometry[0].Material!.Alpha);
            Assert.True(session.Save(doc));
            Assert.Equal(part.ToJson(), AuthoringWorkspace.Open(root).Part(part.Id)!.Asset.ToJson());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MaterialOverridesCanClearAnimationAndInvalidFramesOrChannelsAreRejected()
    {
        var part = Panel();
        var material = part.Geometry[0].Material!;
        var inherited = new RenderMaterialDefinition2D { Asset = "screen" };
        var library = new Dictionary<string, RenderMaterialDefinition2D> { ["screen"] = material };
        var still = inherited.WithOverride(new() { Texture = "still.png", TextureAnimation = new() }).Resolve(library);
        Assert.Equal("still.png", still.Sample(20, Vector4.One).Texture);
        Assert.Empty(still.TextureAnimation!.Frames);
        Assert.Throws<InvalidDataException>(() => (material with { TextureAnimation = new() { Frames = [new() { Texture = "../outside.png" }] } }).Validate("test"));
        Assert.Throws<InvalidDataException>(() => (material with { TextureAnimation = new() { Frames = [new() { Texture = "valid.png", Seconds = 0 }] } }).Validate("test"));
        Assert.Throws<InvalidDataException>(() => (material with { Source = new() { X = .5f, Width = .8f } }).Validate("test"));
        part.Clips[0].Tracks.First(t => t.Kind == MotionClip.AlphaKind).Keys[0].Value = new(1.1f, 0, 0);
        Assert.Throws<InvalidDataException>(part.Validate);
    }

    [Fact]
    public void ARestingRigStillSamplesTextureFramesOnItsLayerClock()
    {
        var part = Panel();
        part.Clips.Clear();
        part.Layers[0].Clip = null;
        part.Layers[0].Clock = PartClocks.Mapped;
        part.Layers[0].Rate = 2;
        var drawing = new PuppetDrawing();
        drawing.AddAttachment(PoseEvaluator.Preview(Resolve(part), LayeredFrame2D.Identity, .25));
        Assert.Equal("wide.png", Assert.Single(drawing.Batches).Texture);
    }

    [Fact]
    public void ExcerptsPreserveHeldVisibilityAndMaterialSetupBeforeTheFirstKey()
    {
        var clip = new MotionClip
        {
            Id = "fade",
            Name = "Fade",
            Model = "panel",
            Tracks = [
            new() { Kind = MotionClip.ShowKind, Target = "panel", Keys = [new() { Value = Vector3.Zero }, new() { Time = .5f, Value = Vector3.UnitX }] },
            new() { Kind = MotionClip.AlphaKind, Target = "panel", SetupBeforeFirst = true, Keys = [new() { Time = .5f, Value = new(.3f, 0, 0) }] }
        ]
        };
        var excerpt = ClipAuthoring.Excerpt(clip, .2f, .8f, "cut", "Cut");
        excerpt.Validate();
        foreach (var time in new[] { 0, .15f, .3f, .4f, .6f })
        {
            foreach (var track in clip.Tracks)
            {
                Assert.Equal(ClipAuthoring.Value(clip, new(track.Kind, track.Target), time + .2f).Value,
                    ClipAuthoring.Value(excerpt, new(track.Kind, track.Target), time).Value);
            }
        }
    }

    [Fact]
    public void PathVelocityStillLandsOnTheSharedCurveAtEveryFixedStep()
    {
        var part = Part.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"));
        var world = new PhysicsWorld2D();
        using var instance = new AuthoredObject2D(EntityId2D.Create(), new("lift", part, new(10, 20)), world, 1, uint.MaxValue);
        var motion = part.Motion!.CreateMotion(new(10, 20));
        const float dt = 1f / 60;
        double seconds = 0;
        for (var i = 0; i < 240; i++)
        {
            var before = instance.WorldObject.Transform.Position;
            seconds += dt;
            instance.Update(dt);
            Assert.True(Vector2.Distance((motion.Position(seconds) - before) / dt, instance.Body!.LinearVelocity) < .001f);
            world.Step(dt);
            Assert.True(Vector2.Distance(motion.Position(seconds), instance.WorldObject.Transform.Position) < .001f);
        }
    }
}
