using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;

namespace App2d.Tests.Characters;

/// <summary>
/// PersonWardrobe.cs is generated from the authored worn and held parts. These tests keep it up to date and hold each thing,
/// as attachments on a mount, to its authored part drawn on the socket of the same name, until those parts are retired.
/// </summary>
public sealed class WornPartTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);
    private static readonly string[] Usages = ["clothing", "glove", "hair", "prop", "shoe"];

    private static List<Part> Parts() => [.. Catalog.Parts.Values.Where(p => Usages.Contains(p.Usage)).OrderBy(p => p.Id, StringComparer.Ordinal)];

    /// <summary>
    /// After changing a JSON part, run this test once with the environment variable APP2D_WRITE_CLIPS set to 1 to rewrite
    /// PersonWardrobe.cs, then build again.
    /// </summary>
    [Fact]
    public void GeneratedWardrobeIsUpToDateWithTheAuthoredParts()
    {
        var source = LegacyPartEmitter.Emit("PersonWardrobe",
            "What a person wears and holds: each function returns one thing's pieces as attachments on a mount of <see cref=\"PersonSkeleton\"/>, at a layer.",
            nameof(WornPartTests), Parts());

        Assert.True(LegacyRigs.UpToDate("PersonWardrobe.cs", source),
            "PersonWardrobe.cs no longer matches the authored JSON parts. Set APP2D_WRITE_CLIPS=1, run WornPartTests, and build again.");
    }

    /// <summary>The sockets a part is tried on: where it says it goes, or the places its kind of thing is put.</summary>
    private static string[] Sockets(Part part) => part.Attachment is { } own ? [own] : part.Usage switch
    {
        "shoe" => ["left-foot-art", "right-foot-art"],
        "glove" => ["left-hand-art", "right-hand-art"],
        _ => ["right-grip", "sword-hand", "back"],
    };

    /// <summary>
    /// A held thing on a mount that a clip turns out of the plane: a swung sword, an aimed pistol, a thrown rock. Every clip
    /// that turns a mount is played with the things that mount holds, and compared with the part on the turned socket.
    /// </summary>
    [Fact]
    public void HeldThingsTurnOutOfThePlaneWithTheirMount()
    {
        var model = Catalog.Resolve("person");
        var skeleton = PersonSkeleton.Skeleton;
        var held = new Dictionary<int, string[]>
        {
            [PersonSkeleton.SwordHand] = ["sword", "hammer", "wooden-club"],
            [PersonSkeleton.GunHand] = ["pistol"],
            [PersonSkeleton.RightGrip] = ["throwing-rock", "spear"],
        };
        var local = new RigidTransform2D[skeleton.Count];
        var world = new RigidTransform2D[skeleton.Count];
        var turns = new Vector3f[skeleton.Mounts.Length];
        float[] keyed = new float[skeleton.Count], layers = new float[skeleton.Count];
        int compared = 0, turned = 0;
        foreach (var clip in PersonClips.All.Where(c => c.Mounts.Length > 0))
        {
            var authored = Catalog.Animations[clip.Name];
            for (var step = 0; step <= 6; step++)
            {
                var time = clip.Duration * step / 6;
                var pose = PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true });
                var actor = new ActorPose(pose, Vector2f.Zero, 1);
                clip.Pose(skeleton, time, local);
                skeleton.ToWorld(local, world);
                clip.SampleMounts(time, turns);
                clip.SampleLayers(time, keyed);
                skeleton.ToLayers(keyed, layers);
                foreach (var track in clip.Mounts)
                {
                    var socket = model.Base.Sockets.Single(s => s.Id == skeleton.Mounts[track.Mount].Name);
                    foreach (var id in held[track.Mount])
                    {
                        var drawing = new PuppetDrawing();
                        drawing.AddAttachment(Catalog.Parts[id], actor, socket);
                        var mesh = new LayeredMesh();
                        AttachmentPainter.Paint(mesh, new(skeleton, world, layers, turns), PersonWardrobe.ByName[id](track.Mount, 0));

                        LegacyRigs.AssertSameMesh(drawing.Mesh.Vertices, mesh.Vertices, $"{id} on {socket.Id}, {clip.Name} at {time:0.###}s, turned {turns[track.Mount]}");
                        compared++;
                        if (turns[track.Mount] != Vector3f.Zero) turned++;
                    }
                }
            }
        }
        Assert.True(compared > 150 && turned > 100, $"Compared {compared} held things, {turned} of them turned.");
    }

    /// <summary>
    /// Vertex for vertex: the same triangles, colours and layers, including the shading and edges of solids. The poses come
    /// from clips that do not turn a mount out of the plane, which attachments do not do yet.
    /// </summary>
    [Fact]
    public void EveryWornAndHeldPartPaintsTheSameOnAMountAsOnItsSocket()
    {
        var model = Catalog.Resolve("person");
        var skeleton = PersonSkeleton.Skeleton;
        var parts = Parts();
        Assert.Equal(parts.Select(p => p.Id), PersonWardrobe.ByName.Keys.Order(StringComparer.Ordinal));
        Assert.True(parts.Count >= 34, $"Expected the worn and held parts, found {parts.Count}.");
        (string Clip, float Fraction)[] poses = [("person-idle", .3f), ("person-walk", .4f), ("person-run", .7f), ("player-jump", .5f)];
        var local = new RigidTransform2D[skeleton.Count];
        var world = new RigidTransform2D[skeleton.Count];
        int compared = 0, solids = 0;

        foreach (var (clip, fraction) in poses)
        {
            var authored = Catalog.Animations[clip];
            var time = authored.Duration * fraction;
            var pose = PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true });
            var actor = new ActorPose(pose, Vector2f.Zero, 1);
            PersonClips.All.Single(c => c.Name == clip).Pose(skeleton, time, local);
            skeleton.ToWorld(local, world);
            foreach (var part in parts)
            {
                foreach (var id in Sockets(part))
                {
                    var socket = model.Base.Sockets.Single(s => s.Id == id);
                    Assert.Equal(Vector3f.Zero, pose.SocketAngles.GetValueOrDefault(id));
                    var drawing = new PuppetDrawing();
                    drawing.AddAttachment(part, actor, socket);
                    var expected = drawing.Mesh.Vertices;

                    var mount = skeleton.Mounts.IndexOf(skeleton.Mounts.Single(m => m.Name == id));
                    var attachments = PersonWardrobe.ByName[part.Id](mount, 0);
                    var mesh = new LayeredMesh();
                    AttachmentPainter.Paint(mesh, new(skeleton, world), attachments);
                    var actual = mesh.Vertices;

                    LegacyRigs.AssertSameMesh(expected, actual, $"{part.Id} on {id}, {clip}");
                    compared++;
                    solids += attachments.Count(a => a is MountedSolid);
                }
            }
        }
        Assert.True(compared > 150 && solids > 300, $"Compared {compared} placements and {solids} solids.");
    }
}
