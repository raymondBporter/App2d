using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Characters;

/// <summary>Holds the C# person clips and their sampler to the authored JSON clips and the current evaluator.</summary>
public sealed class PersonClipTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);
    private static readonly List<MotionClip> Authored = [.. Catalog.Animations.Values.Where(c => c.Model == "person").OrderBy(c => c.Id, StringComparer.Ordinal)];
    private const int Steps = 24;

    /// <summary>
    /// PersonClips.cs is generated from the JSON clips for as long as those exist. After changing a JSON clip, run this test
    /// once with the environment variable APP2D_WRITE_CLIPS set to 1 to rewrite the file, then build again.
    /// </summary>
    [Fact]
    public void GeneratedSourceIsUpToDateWithTheAuthoredClips()
    {
        var model = Catalog.Resolve("person");
        var skeleton = PersonSkeleton.Skeleton;
        var clips = Authored.Select(c => LegacyClips.Convert(c, model, skeleton, out _)).ToList();
        string[] limbs = ["LeftArm", "LeftLeg", "RightArm", "RightLeg"];
        var source = LegacyClipEmitter.Emit("PersonClips", "PersonSkeleton",
            "The person animations, for <see cref=\"PersonSkeleton\"/>. Values are changes from rest, in the skeleton's own units.", "PersonClipTests", skeleton, limbs, clips);

        Assert.True(LegacyRigs.UpToDate("PersonClips.cs", source),
            "PersonClips.cs no longer matches the authored JSON clips. Set APP2D_WRITE_CLIPS=1, run PersonClipTests, and build again.");
    }

    private static (float Distance, float Angle, string Where) Compare(ResolvedModel model, Skeleton2D skeleton, int steps) =>
        LegacyRigs.Compare(model, skeleton, Authored, authored => PersonClips.All.Single(c => c.Name == authored.Id), steps);

    [Fact]
    public void EveryPersonClipPosesTheSkeletonAsTheCurrentEvaluatorDoes()
    {
        Assert.True(Authored.Count >= 41, $"Expected the person clips, found {Authored.Count}.");
        Assert.Equal(Authored.Select(c => c.Id), PersonClips.All.Select(c => c.Name).Order(StringComparer.Ordinal));
        foreach (var authored in Authored)
        {
            var clip = PersonClips.All.Single(c => c.Name == authored.Id);
            Assert.Equal(authored.Duration, clip.Duration);
            Assert.Equal(authored.Loop, clip.Loop);
            Assert.Equal(authored.Markers.Select(m => new ClipMarker2D(m.Id, m.Time)), clip.Markers);
        }

        var worst = Compare(Catalog.Resolve("person"), PersonSkeleton.Skeleton, Steps);

        Assert.True(worst.Distance < 1e-3f && worst.Angle < 2e-3f, $"Worst difference {worst.Distance} / {worst.Angle} rad: {worst.Where}");
    }

    /// <summary>The same clips on every authored build: scaled proportions scale the keyed motion as the current rig's measures do.</summary>
    [Fact]
    public void EveryVariantPlaysTheSameClipsWithItsOwnProportions()
    {
        var variants = Directory.EnumerateFiles(Path.Combine(TestAssetPath.AuthoredRoot, "variants"), "*.json")
            .Select(path => ModelVariant.FromJson(File.ReadAllText(path))).Where(v => v.Base == "person").ToList();
        Assert.True(variants.Count >= 7, $"Expected the person variants, found {variants.Count}.");
        foreach (var variant in variants)
        {
            float Value(string id) => variant.Parameters.GetValueOrDefault(id, 1);
            var skeleton = PersonSkeleton.Build(Value("legs"), Value("torso"), Value("arms"), Value("width"));

            var worst = Compare(Catalog.Resolve(variant.Id), skeleton, 8);

            Assert.True(worst.Distance < 1e-3f && worst.Angle < 2e-3f, $"Worst difference {worst.Distance} / {worst.Angle} rad: {worst.Where}");
        }
    }

    /// <summary>
    /// The game lays an action over a gait: an upper-body clip over the legs, or a gait's legs under a pose. Each pairing is
    /// played at several weights and compared with the current evaluator's overlay.
    /// </summary>
    [Fact]
    public void LayeredClipsPoseTheSkeletonAsTheCurrentEvaluatorDoes()
    {
        var model = Catalog.Resolve("person");
        var skeleton = PersonSkeleton.Skeleton;
        IReadOnlySet<string> lower = model.Base.Groups.Single(g => g.Id == "lower").Targets.ToHashSet(StringComparer.Ordinal);
        (string[] Under, string[] Over, SkeletonMask2D Mask, IReadOnlySet<string> Targets)[] cases =
        [
            (["person-idle", "person-walk", "person-run", "player-jump", "player-fall", "player-climb"],
                ["player-gun-shot", "player-gun-aim", "player-gun-charge", "player-sword-forehand", "player-sword-backhand", "player-sword-put-away"],
                PersonSkeleton.UpperBody, PersonLoadout.UpperBody),
            (["player-sword-forehand", "person-idle", "player-gun-aim"], ["person-walk", "person-run", "player-dash"], PersonSkeleton.LowerBody, lower),
        ];
        float[] weights = [1, .75f, .5f, .25f];
        const int steps = 4;
        RigidTransform2D[] local = new RigidTransform2D[skeleton.Count], layer = new RigidTransform2D[skeleton.Count], world = new RigidTransform2D[skeleton.Count];
        LimbSample2D[] limbs = new LimbSample2D[skeleton.Limbs.Length], layerLimbs = new LimbSample2D[skeleton.Limbs.Length];
        Vector3f[] turns = new Vector3f[skeleton.Mounts.Length], layerTurns = new Vector3f[skeleton.Mounts.Length];
        float[] keyed = new float[skeleton.Count], overKeyed = new float[skeleton.Count], layers = new float[skeleton.Count];
        var layered = 0;
        var turned = 0;
        var worst = (Distance: 0f, Angle: 0f, Where: "");
        var compared = 0;

        foreach (var (unders, overs, mask, targets) in cases)
        {
            foreach (var under in unders.Select(id => (Authored: Catalog.Animations[id], Clip: PersonClips.All.Single(c => c.Name == id))))
            {
                foreach (var over in overs.Select(id => (Authored: Catalog.Animations[id], Clip: PersonClips.All.Single(c => c.Name == id))))
                {
                    foreach (var weight in weights)
                    {
                        for (var step = 0; step < steps; step++)
                        {
                            // Times inside both clips, off their key times and ends.
                            var underTime = under.Authored.Duration * (step + .37f) / steps;
                            var overTime = over.Authored.Duration * (steps - step - .41f) / steps;
                            var expected = PoseEvaluator.Sample(model, under.Authored, underTime, repeat: false,
                                new PoseInput { InPlace = true, Overlay = new PoseLayer(over.Authored, overTime, targets, weight) });
                            under.Clip.Sample(skeleton, underTime, local, limbs);
                            over.Clip.Sample(skeleton, overTime, layer, layerLimbs);
                            SkeletonClip2D.Overlay(local, limbs, layer, layerLimbs, mask, weight);
                            SkeletonClip2D.Place(skeleton, local, limbs);
                            skeleton.ToWorld(local, world);
                            under.Clip.SampleMounts(underTime, turns);
                            over.Clip.SampleMounts(overTime, layerTurns);
                            SkeletonClip2D.OverlayMounts(turns, layerTurns, mask, weight);
                            under.Clip.SampleLayers(underTime, keyed);
                            over.Clip.SampleLayers(overTime, overKeyed);
                            SkeletonClip2D.OverlayLayers(keyed, overKeyed, mask, weight);
                            skeleton.ToLayers(keyed, layers);
                            for (var joint = 0; joint < skeleton.Count; joint++)
                            {
                                var name = skeleton.Joints[joint].Name;
                                Assert.True(MathF.Abs(expected.Points[name].Z - layers[joint]) < 1e-5f,
                                    $"{over.Authored.Id} over {under.Authored.Id} at weight {weight}, {name}: evaluator layer {expected.Points[name].Z}, skeleton {layers[joint]}.");
                                if (keyed[joint] != 0) layered++;
                            }
                            for (var mount = 0; mount < turns.Length; mount++)
                            {
                                var socket = expected.SocketAngles.GetValueOrDefault(skeleton.Mounts[mount].Name);
                                Assert.True((socket - turns[mount]).Length() < 1e-4f,
                                    $"{over.Authored.Id} over {under.Authored.Id} at weight {weight}, mount {skeleton.Mounts[mount].Name}: socket turned {socket}, mount {turns[mount]}.");
                                if (turns[mount] != Vector3f.Zero) turned++;
                            }
                            compared++;
                            for (var joint = 0; joint < skeleton.Count; joint++)
                            {
                                var name = skeleton.Joints[joint].Name;
                                var distance = Vector2f.Distance(expected.Points[name].XY, world[joint].Translation);
                                var angle = MathF.Abs(Angles.WrapSigned(expected.Angles[name] - world[joint].Rotation));
                                if (distance > worst.Distance || angle > worst.Angle)
                                {
                                    worst = (MathF.Max(distance, worst.Distance), MathF.Max(angle, worst.Angle), $"{over.Authored.Id} over {under.Authored.Id} at weight {weight}, {name}: evaluator {expected.Points[name].XY} / {expected.Angles[name]:0.####}, skeleton {world[joint].Translation} / {world[joint].Rotation:0.####}");
                                }
                            }
                        }
                    }
                }
            }
        }

        Assert.True(compared > 700 && turned > 100 && layered > 100, $"Compared {compared} overlaid poses, {turned} turned mounts, {layered} keyed layers.");
        Assert.True(worst.Distance < 1e-3f && worst.Angle < 2e-3f, $"Worst difference {worst.Distance} / {worst.Angle} rad: {worst.Where}");
    }

    [Fact]
    public void ConversionAccountsForEverythingItLeavesBehind()
    {
        var model = Catalog.Resolve("person");
        var plays = 0;
        foreach (var authored in Authored)
        {
            var clip = LegacyClips.Convert(authored, model, PersonSkeleton.Skeleton, out var skipped);
            plays += skipped.PlayTracks;
            Assert.Equal(authored.Contacts.Count, clip.Contacts.Length);
        }
        // Not skeleton motion, so not in the skeleton clip yet: the face's expression plays.
        Assert.True(plays > 0, $"plays {plays}");
    }
}
