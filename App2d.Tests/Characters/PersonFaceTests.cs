using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;

namespace App2d.Tests.Characters;

/// <summary>
/// PersonFace.cs is generated from the authored face part. These tests keep it up to date and hold every expression, as
/// attachments on the head's mount, to the face the current rig draws, until that part is retired.
/// </summary>
public sealed class PersonFaceTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    /// <summary>
    /// After changing the JSON face part, run this test once with the environment variable APP2D_WRITE_CLIPS set to 1 to
    /// rewrite PersonFace.cs, then build again.
    /// </summary>
    [Fact]
    public void GeneratedFaceIsUpToDateWithTheAuthoredPart()
    {
        var source = LegacyFaceEmitter.Emit("PersonFace",
            "The person's face, for the head mount of <see cref=\"PersonSkeleton\"/>: eyes, brows and a mouth, and every expression they can make.",
            nameof(PersonFaceTests), Catalog.Resolve("person"), PersonSkeleton.Skeleton, "face", "blink");

        Assert.True(LegacyRigs.UpToDate("PersonFace.cs", source),
            "PersonFace.cs no longer matches the authored JSON face part. Set APP2D_WRITE_CLIPS=1, run PersonFaceTests, and build again.");
    }

    /// <summary>
    /// Every expression, on the standard head and on every variant's, on a still and a moving head, at several moments of the
    /// blink: vertex for vertex the same triangles, colours and layers as the authored face.
    /// </summary>
    [Fact]
    public void EveryExpressionPaintsTheSameAsTheAuthoredFace()
    {
        var builds = Directory.EnumerateFiles(Path.Combine(TestAssetPath.AuthoredRoot, "variants"), "*.json")
            .Select(path => ModelVariant.FromJson(File.ReadAllText(path))).Where(v => v.Base == "person")
            .Select(v => (v.Id, Values: (IReadOnlyDictionary<string, float>)v.Parameters)).Prepend(("person", new Dictionary<string, float>())).ToList();
        (string Clip, float Fraction)[] poses = [("person-idle", .3f), ("person-walk", .6f)];
        double[] clocks = [0, 1.3, 3.58, 3.66, 5];
        var compared = 0;
        Assert.True(PersonFace.Face.Expressions.Length >= 17, $"Expected the face's expressions, found {PersonFace.Face.Expressions.Length}.");

        foreach (var (id, values) in builds)
        {
            float Value(string name) => values.GetValueOrDefault(name, 1);
            var model = Catalog.Resolve(id);
            var skeleton = PersonSkeleton.Build(Value("legs"), Value("torso"), Value("arms"), Value("width"));
            var local = new RigidTransform2D[skeleton.Count];
            var world = new RigidTransform2D[skeleton.Count];
            foreach (var (clip, fraction) in poses)
            {
                var authored = Catalog.Animations[clip];
                var time = authored.Duration * fraction;
                PersonClips.All.Single(c => c.Name == clip).Pose(skeleton, time, local);
                skeleton.ToWorld(local, world);
                foreach (var expression in PersonFace.Face.Expressions)
                {
                    foreach (var clock in clocks)
                    {
                        var pose = PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput(expression.Name) { InPlace = true, Clock = clock });
                        var drawing = new PuppetDrawing();
                        drawing.AddAttachments(model, pose);
                        var mesh = new LayeredMesh();
                        AttachmentPainter.Paint(mesh, new(skeleton, world),
                            PersonFace.Face.Attachments(expression, PersonSkeleton.HeadArt, clock, Value("head"), PersonFace.Layer));

                        LegacyRigs.AssertSameMesh(drawing.Mesh.Vertices, mesh.Vertices, $"{id}, {expression.Name}, {clip}, clock {clock}");
                        compared++;
                    }
                }
            }
        }
        Assert.True(compared > 1000, $"Only {compared} faces were compared.");
    }
}
