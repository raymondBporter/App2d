using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using System.Collections.Immutable;

namespace App2d.Tests.Characters;

/// <summary>Holds the person's body attachments to what the authored rig's parts draw, until those parts are retired.</summary>
public sealed class PersonBodyTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);
    private static readonly List<MotionClip> Authored = [.. Catalog.Animations.Values.Where(c => c.Model == "person").OrderBy(c => c.Id, StringComparer.Ordinal)];

    /// <summary>
    /// Every attachment against the part of the same name: its look and layer once, then its traced points against the
    /// part's contour for every clip at several times. Returns how many contours were compared.
    /// </summary>
    private static int AssertDrawsTheSame(ResolvedModel model, Skeleton2D skeleton, ImmutableArray<Attachment> body, int steps)
    {
        Assert.Equal(model.Parts.Select(p => p.Id), body.Select(a => a.Name));
        foreach (var (part, attachment) in model.Parts.Zip(body))
        {
            var material = part.RenderMaterial;
            Assert.Equal((material.Fill, material.StrokeWidth, material.Outline?.Color, material.Outline?.Width),
                (attachment.Material.Fill, attachment.Material.StrokeWidth, attachment.Material.Outline?.Color, attachment.Material.Outline?.Width));
            // A curve is drawn as a line through joints; anything else is a shape on a mount.
            Assert.Equal(part.Geometry is not ICurve2D, attachment is MountedShape);
            // A piece's layer is its offset from the joint it is on; the painted meshes are compared for where that puts it.
            Assert.Equal(part.Layer, attachment.Layer, 5);
        }

        var local = new RigidTransform2D[skeleton.Count];
        var world = new RigidTransform2D[skeleton.Count];
        var points = new List<Vector2f>();
        var compared = 0;
        foreach (var authored in Authored)
        {
            var clip = PersonClips.All.Single(c => c.Name == authored.Id);
            for (var step = 0; step <= steps; step++)
            {
                var time = authored.Duration * step / steps;
                var pose = PoseEvaluator.Sample(model, authored, time, repeat: false, new PoseInput { InPlace = true });
                clip.Pose(skeleton, time, local);
                skeleton.ToWorld(local, world);
                foreach (var (part, attachment) in model.Parts.Zip(body))
                {
                    var contour = PartContours2D.Contour(part, model, pose);
                    points.Clear();
                    if (attachment is MountedShape shape) shape.Trace(skeleton, world, points);
                    else ((JointPath)attachment).Trace(world, points);
                    Assert.Equal(contour.Count, points.Count);
                    for (var i = 0; i < points.Count; i++)
                    {
                        Assert.True(Vector2f.Distance(contour[i].XY, points[i]) < 2e-3f,
                            $"{model.Id}, {authored.Id} at {time:0.###}s, {part.Id} point {i}: part {contour[i].XY}, attachment {points[i]}.");
                    }
                    compared++;
                }
            }
        }
        return compared;
    }

    [Fact]
    public void TheStandardBodyTracesWhatTheAuthoredPartsDraw()
    {
        var compared = AssertDrawsTheSame(Catalog.Resolve("person"), PersonSkeleton.Skeleton, PersonBody.Standard, 12);

        Assert.True(compared > 5000, $"Only {compared} contours were compared.");
    }

    /// <summary>A variant is the same body with a build's proportions and its own skin and shirt colours.</summary>
    [Fact]
    public void EveryVariantIsTheSameBodyWithItsOwnProportionsAndColours()
    {
        var variants = Directory.EnumerateFiles(Path.Combine(TestAssetPath.AuthoredRoot, "variants"), "*.json")
            .Select(path => ModelVariant.FromJson(File.ReadAllText(path))).Where(v => v.Base == "person").ToList();
        Assert.True(variants.Count >= 7, $"Expected the person variants, found {variants.Count}.");
        foreach (var variant in variants)
        {
            float Value(string id) => variant.Parameters.GetValueOrDefault(id, 1);
            var model = Catalog.Resolve(variant.Id);
            string Fill(string part) => model.Parts.Single(p => p.Id == part).RenderMaterial.Fill!;
            var skeleton = PersonSkeleton.Build(Value("legs"), Value("torso"), Value("arms"), Value("width"));
            var body = PersonBody.Build(Value("torso"), Value("width"), Value("head"), Fill("head"), Fill("body"));

            AssertDrawsTheSame(model, skeleton, body, 4);
        }
    }
}
