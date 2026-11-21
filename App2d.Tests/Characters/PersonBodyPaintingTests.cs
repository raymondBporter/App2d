using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Tests.Rendering;
using System.Collections.Immutable;
using Microsoft.Xna.Framework.Graphics;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Tests.Characters;

/// <summary>Holds painted attachments to what the authored rig's parts paint, until those parts are retired.</summary>
[Collection("Graphics")]
public sealed class PersonBodyPaintingTests
{
    private const int Size = 320;
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);
    private static readonly List<MotionClip> Authored = [.. Catalog.Animations.Values.Where(c => c.Model == "person").OrderBy(c => c.Id, StringComparer.Ordinal)];

    /// <summary>The body parts alone, painted the current way: no face and nothing worn.</summary>
    private static PuppetDrawing PaintParts(ResolvedModel model, EvaluatedPose pose)
    {
        var drawing = new PuppetDrawing();
        drawing.Add(model.Parts.Select(pose.Drawn), pose.World, pose.Placement, id => model.Rest[id], model.RestPlacement);
        return drawing;
    }

    /// <summary>The attachments painted on the standard skeleton, posed by a clip with its layers and mount turns.</summary>
    private static LayeredMesh Paint(string clip, float time, ImmutableArray<Attachment> attachments)
    {
        var skeleton = PersonSkeleton.Skeleton;
        var played = PersonClips.All.Single(c => c.Name == clip);
        var local = new RigidTransform2D[skeleton.Count];
        var world = new RigidTransform2D[skeleton.Count];
        float[] keyed = new float[skeleton.Count], layers = new float[skeleton.Count];
        var turns = new Vector3f[skeleton.Mounts.Length];
        played.Pose(skeleton, time, local);
        skeleton.ToWorld(local, world);
        played.SampleLayers(time, keyed);
        skeleton.ToLayers(keyed, layers);
        played.SampleMounts(time, turns);
        var mesh = new LayeredMesh();
        AttachmentPainter.Paint(mesh, new(skeleton, world, layers, turns), attachments);
        return mesh;
    }

    private static LayeredMesh PaintAttachments(string clip, float time) => Paint(clip, time, PersonBody.Standard);

    /// <summary>
    /// Vertex for vertex: the same triangles in the same order, colours and layers, on every clip, including the ones that
    /// key the drawing order.
    /// </summary>
    [Fact]
    public void TheBodyPaintsTheSameMeshAsTheAuthoredParts()
    {
        var model = Catalog.Resolve("person");
        var compared = 0;
        foreach (var authored in Authored)
        {
            for (var step = 0; step <= 6; step++)
            {
                var time = authored.Duration * step / 6;
                var expected = PaintParts(model, PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true })).Mesh.Vertices;
                var actual = PaintAttachments(authored.Id, time).Vertices;
                Assert.Equal(expected.Length, actual.Length);
                for (var i = 0; i < expected.Length; i++)
                {
                    var where = $"{authored.Id} at {time:0.###}s, vertex {i} of {expected.Length}";
                    Assert.True((expected[i].Position - actual[i].Position).Length() < 2e-3f, $"{where}: parts {expected[i].Position}, attachments {actual[i].Position}.");
                    Assert.True(expected[i].Color == actual[i].Color, $"{where}: parts {expected[i].Color}, attachments {actual[i].Color}.");
                    Assert.True(MathF.Abs(expected[i].Layer - actual[i].Layer) < 1e-5f, $"{where}: parts layer {expected[i].Layer}, attachments {actual[i].Layer}.");
                }
                compared++;
            }
        }
        Assert.True(compared > 250, $"Only {compared} meshes were compared.");
    }

    /// <summary>
    /// Renders both meshes and compares the pixels. With the environment variable APP2D_RENDER_DIR set to a folder it also
    /// saves a picture: the authored parts on the left, the attachments on the right.
    /// </summary>
    private static void AssertSamePicture(LayeredMesh parts, LayeredMesh attachments, string name)
    {
        // The origin is on the ground between the feet; 130 pixels to a unit shows the whole figure with room to move.
        var projection = LayeredMeshRenderer.Projection(Size, Size, new(Size / 2f, Size - 30), 130);
        using var graphics = new GraphicsTestContext();
        using var renderer = new PointCharacterRenderer(graphics.Device);
        Color4b[] Render(LayeredMesh mesh)
        {
            using var target = RenderCapture2D.CreateTarget(graphics.Device, Size, Size, DepthFormat.Depth24, 0);
            using (graphics.Device.UseRenderTarget(target, Color4b.White))
            {
                renderer.ClearDepth();
                renderer.Draw(mesh, projection, Matrix.Identity);
            }
            var pixels = new Color4b[Size * Size];
            target.GetData(pixels);
            return pixels;
        }
        var expected = Render(parts);
        var actual = Render(attachments);

        if (Environment.GetEnvironmentVariable("APP2D_RENDER_DIR") is { Length: > 0 } directory)
        {
            using var both = RenderCapture2D.CreateTarget(graphics.Device, Size * 2, Size, DepthFormat.Depth24, 0);
            using (graphics.Device.UseRenderTarget(both, Color4b.White))
            {
                renderer.ClearDepth();
                renderer.Draw(parts, LayeredMeshRenderer.Projection(Size * 2, Size, new(Size / 2f, Size - 30), 130), Matrix.Identity);
                renderer.ClearDepth();
                renderer.Draw(attachments, LayeredMeshRenderer.Projection(Size * 2, Size, new(Size * 1.5f, Size - 30), 130), Matrix.Identity);
            }
            Directory.CreateDirectory(directory);
            both.SavePng(Path.Combine(directory, name + ".png"));
        }

        Assert.Contains(expected, pixel => pixel != Color4b.White);
        var different = expected.Zip(actual).Count(p => p.First != p.Second);
        Assert.True(different <= 8, $"{name}: {different} of {Size * Size} pixels differ.");
    }

    [Theory]
    [InlineData("person-idle", .5f)]
    [InlineData("person-run", .2f)]
    [InlineData("player-jump", .3f)]
    [InlineData("person-hammer-slam", .6f)]
    public void TheBodyRendersTheSamePictureAsTheAuthoredParts(string clip, float fraction)
    {
        var model = Catalog.Resolve("person");
        var authored = Catalog.Animations[clip];
        var time = authored.Duration * fraction;
        var parts = PaintParts(model, PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true }));

        AssertSamePicture(parts.Mesh, PaintAttachments(clip, time), clip);
    }

    /// <summary>
    /// The body dressed in what an authored character wears and holds: each thing drawn on its socket the current way, and as
    /// attachments on the mount of the same name.
    /// </summary>
    [Theory]
    [InlineData("club-caveman", "person-walk", .4f)]
    [InlineData("cinder-gunner", "person-idle", .3f)]
    [InlineData("hero", "person-run", .7f)]
    [InlineData("hero", "player-sword-forehand", .45f)]
    [InlineData("hero", "player-climb", .5f)]
    [InlineData("club-caveman", "club-caveman-slam", .42f)]
    public void ADressedPersonRendersTheSamePictureAsTheAuthoredParts(string character, string clip, float fraction)
    {
        var model = Catalog.Resolve("person");
        var skeleton = PersonSkeleton.Skeleton;
        var authored = Catalog.Animations[clip];
        var time = authored.Duration * fraction;
        var pose = PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true });
        var actor = new ActorPose(pose, Vector2f.Zero, 1);
        var parts = PaintParts(model, pose);
        var worn = new List<Attachment>(PersonBody.Standard);
        var equipment = Catalog.Parts[character].Equipment;
        Assert.NotEmpty(equipment);
        foreach (var item in equipment)
        {
            var socket = model.Base.Sockets.Single(s => s.Id == item.Socket);
            var prop = Catalog.Parts[item.Prop];
            parts.AddAttachment(prop, actor, socket);
            var mount = skeleton.Mounts.IndexOf(skeleton.Mounts.Single(m => m.Name == item.Socket));
            worn.AddRange(PersonWardrobe.ByName[item.Prop](mount, 0));
        }
        AssertSamePicture(parts.Mesh, Paint(clip, time, [.. worn]), $"{character}-dressed-{clip}");
    }
}
