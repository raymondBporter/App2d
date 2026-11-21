using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Kinematics;
using System.Collections.Immutable;

namespace App2d.Tests.Characters;

/// <summary>
/// The quadruped, pterosaur and stalker skeletons and clips are generated from the authored JSON rigs. These tests keep the
/// generated files up to date and hold them to the current evaluator, until the JSON rigs are retired.
/// </summary>
public sealed class GeneratedRigTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    private sealed record Build(string Property, string Summary, string Model, Skeleton2D Generated);
    private sealed record ClipSet(string ClassName, string Summary, Build Build, ImmutableArray<SkeletonClip2D> Generated);
    private sealed record Rig(string ClassName, string Summary, Build[] Builds, ClipSet[] ClipSets);

    private static readonly Rig[] Rigs = CreateRigs();

    private static Rig[] CreateRigs()
    {
        var quadruped = new Build("Skeleton", "The standard quadruped. The triceratops is this same skeleton with different art.", "quadruped", QuadrupedSkeleton.Skeleton);
        var baby = new Build("Baby", "The baby triceratops: the same joints at seven tenths of the size. It has its own clips.", "baby-triceratops-rig", QuadrupedSkeleton.Baby);
        var pterosaur = new Build("Skeleton", "The pterosaur at rest, wings spread.", "pterosaur", PterosaurSkeleton.Skeleton);
        var stalker = new Build("Skeleton", "The stalker at rest on its three legs.", "stalker", StalkerSkeleton.Skeleton);
        return
        [
            new("QuadrupedSkeleton", "A four-legged body seen from the side, facing +X: body, head and tail, and a near and a far pair of legs that IK places on the ground.",
                [quadruped, baby],
                [
                    new("QuadrupedClips", "The quadruped animations, for <see cref=\"QuadrupedSkeleton.Skeleton\"/>. Values are changes from rest, in the skeleton's own units.", quadruped, QuadrupedClips.All),
                    new("BabyTriceratopsClips", "The baby triceratops animations, for <see cref=\"QuadrupedSkeleton.Baby\"/>. Values are changes from rest, in that skeleton's own units.", baby, BabyTriceratopsClips.All),
                ]),
            new("PterosaurSkeleton", "A flying pterosaur seen from the side, facing +X. Every joint is keyed; nothing is placed by IK.",
                [pterosaur],
                [new("PterosaurClips", "The pterosaur animations, for <see cref=\"PterosaurSkeleton\"/>. Values are changes from rest, in the skeleton's own units.", pterosaur, PterosaurClips.All)]),
            new("StalkerSkeleton", "A three-legged stalker seen from the side, facing +X: a body, and three legs that IK places on the ground.",
                [stalker],
                [new("StalkerClips", "The stalker animations, for <see cref=\"StalkerSkeleton\"/>. Values are changes from rest, in the skeleton's own units.", stalker, StalkerClips.All)]),
        ];
    }

    private static List<MotionClip> Authored(string model) => [.. Catalog.Animations.Values.Where(c => c.Model == model).OrderBy(c => c.Id, StringComparer.Ordinal)];

    /// <summary>
    /// After changing a JSON rig or clip, run this test once with the environment variable APP2D_WRITE_CLIPS set to 1 to rewrite
    /// the generated files, then build again.
    /// </summary>
    [Fact]
    public void GeneratedSourcesAreUpToDateWithTheAuthoredRigs()
    {
        var stale = new List<string>();
        foreach (var rig in Rigs)
        {
            var models = rig.Builds.ToDictionary(b => b, b => Catalog.Resolve(b.Model));
            var skeletons = models.ToDictionary(p => p.Key, p => LegacyRigs.Skeleton(p.Value));
            string[] limbs = [.. models[rig.Builds[0]].IkConstraints.Select(k => LegacyClipEmitter.Pascal(k.Id))];
            var source = LegacySkeletonEmitter.Emit(rig.ClassName, rig.Summary, nameof(GeneratedRigTests), limbs, [.. rig.Builds.Select(b => (b.Property, b.Summary, skeletons[b]))]);
            if (!LegacyRigs.UpToDate(rig.ClassName + ".cs", source)) stale.Add(rig.ClassName + ".cs");
            foreach (var set in rig.ClipSets)
            {
                var clips = Authored(set.Build.Model).Select(c => LegacyClips.Convert(c, models[set.Build], skeletons[set.Build], out _)).ToList();
                source = LegacyClipEmitter.Emit(set.ClassName, rig.ClassName, set.Summary, nameof(GeneratedRigTests), skeletons[set.Build], limbs, clips);
                if (!LegacyRigs.UpToDate(set.ClassName + ".cs", source)) stale.Add(set.ClassName + ".cs");
            }
        }

        Assert.True(stale.Count == 0, $"{string.Join(", ", stale)} no longer match the authored JSON. Set APP2D_WRITE_CLIPS=1, run GeneratedRigTests, and build again.");
    }

    [Fact]
    public void EveryGeneratedSkeletonRestsWhereItsAuthoredRigRests()
    {
        foreach (var build in Rigs.SelectMany(r => r.Builds))
        {
            LegacyRigs.AssertRestMatches(Catalog.Resolve(build.Model), build.Generated);
        }
    }

    [Fact]
    public void EveryGeneratedClipPosesItsSkeletonAsTheCurrentEvaluatorDoes()
    {
        foreach (var set in Rigs.SelectMany(r => r.ClipSets))
        {
            var authored = Authored(set.Build.Model);
            Assert.NotEmpty(authored);
            Assert.Equal(authored.Select(c => c.Id), set.Generated.Select(c => c.Name).Order(StringComparer.Ordinal));

            var worst = LegacyRigs.Compare(Catalog.Resolve(set.Build.Model), set.Build.Generated, authored, a => set.Generated.Single(c => c.Name == a.Id), 24);

            Assert.True(worst.Distance < 1e-3f && worst.Angle < 2e-3f, $"Worst difference {worst.Distance} / {worst.Angle} rad: {worst.Where}");
        }
    }

    /// <summary>The triceratops rig and its eight clips are copies of the quadruped's, so it needs neither a skeleton nor clips of its own.</summary>
    [Fact]
    public void TheTriceratopsIsTheQuadrupedPlayingTheQuadrupedClips()
    {
        var model = Catalog.Resolve("triceratops");
        var authored = Authored("triceratops");
        Assert.Equal(QuadrupedClips.All.Length, authored.Count);
        LegacyRigs.AssertRestMatches(model, QuadrupedSkeleton.Skeleton);

        var worst = LegacyRigs.Compare(model, QuadrupedSkeleton.Skeleton, authored,
            a => QuadrupedClips.All.Single(c => c.Name == "quadruped" + a.Id["triceratops".Length..]), 24);

        Assert.True(worst.Distance < 1e-3f && worst.Angle < 2e-3f, $"Worst difference {worst.Distance} / {worst.Angle} rad: {worst.Where}");
    }
}
