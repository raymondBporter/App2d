using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Tests.Characters;

/// <summary>Holds the C# person skeleton to the rig the authored JSON resolves to, until the JSON rig is retired.</summary>
public sealed class PersonSkeletonTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestAssetPath.AuthoredRoot);

    private static RigidTransform2D[] RestWorld(Skeleton2D skeleton) => LegacyRigs.RestWorld(skeleton);

    [Fact]
    public void StandardBuildRestsWhereTheAuthoredRigRests() => LegacyRigs.AssertRestMatches(Catalog.Resolve("person"), PersonSkeleton.Skeleton);

    [Fact]
    public void EveryAuthoredVariantIsTheSameSkeletonWithScaledProportions()
    {
        var variants = Directory.EnumerateFiles(Path.Combine(TestAssetPath.AuthoredRoot, "variants"), "*.json")
            .Select(path => ModelVariant.FromJson(File.ReadAllText(path))).Where(v => v.Base == "person").ToList();
        Assert.NotEmpty(variants);
        foreach (var variant in variants)
        {
            float Value(string id) => variant.Parameters.GetValueOrDefault(id, 1);
            LegacyRigs.AssertRestMatches(Catalog.Resolve(variant.Id), PersonSkeleton.Build(Value("legs"), Value("torso"), Value("arms"), Value("width")));
        }
    }

    [Fact]
    public void IndexConstantsNameTheirJoints()
    {
        (int Index, string Name)[] joints =
        [
            (PersonSkeleton.Hips, "hips"), (PersonSkeleton.Chest, "chest"), (PersonSkeleton.Head, "head"),
            (PersonSkeleton.LeftShoulder, "left-shoulder"), (PersonSkeleton.LeftUpperArm, "left-upper-arm"), (PersonSkeleton.LeftElbow, "left-elbow"), (PersonSkeleton.LeftHand, "left-hand"),
            (PersonSkeleton.LeftHip, "left-hip"), (PersonSkeleton.LeftKnee, "left-knee"), (PersonSkeleton.LeftFoot, "left-foot"),
            (PersonSkeleton.RightShoulder, "right-shoulder"), (PersonSkeleton.RightUpperArm, "right-upper-arm"), (PersonSkeleton.RightElbow, "right-elbow"), (PersonSkeleton.RightHand, "right-hand"),
            (PersonSkeleton.RightHip, "right-hip"), (PersonSkeleton.RightKnee, "right-knee"), (PersonSkeleton.RightFoot, "right-foot"),
        ];
        Assert.Equal(PersonSkeleton.Skeleton.Count, joints.Length);
        foreach (var (index, name) in joints)
        {
            Assert.Equal(name, PersonSkeleton.Skeleton.Joints[index].Name);
        }
    }

    private static readonly (int End, int Bend)[] Limbs =
    [
        (PersonSkeleton.LeftHand, PersonSkeleton.ElbowBend), (PersonSkeleton.RightHand, PersonSkeleton.ElbowBend),
        (PersonSkeleton.LeftFoot, PersonSkeleton.KneeBend), (PersonSkeleton.RightFoot, PersonSkeleton.KneeBend),
    ];

    [Fact]
    public void BendsMatchTheAuthoredChains()
    {
        var model = Catalog.Resolve("person");
        foreach (var (end, bend) in Limbs)
        {
            Assert.Equal(model.IkConstraints.Single(c => c.End == PersonSkeleton.Skeleton.Joints[end].Name).Bend, bend);
        }
    }

    /// <summary>
    /// The rig never shows its authored rest: every limb is always solved, which folds the elbows to the other side. Solving
    /// each limb for where its end rests must therefore give the pose the current evaluator calls rest.
    /// </summary>
    [Fact]
    public void PlacingEachEndWhereItRestsGivesTheEvaluatedRestPose()
    {
        var model = Catalog.Resolve("person");
        var evaluated = PoseEvaluator.Rest(model);
        var skeleton = PersonSkeleton.Skeleton;
        var rest = RestWorld(skeleton);
        var local = skeleton.RestPose();
        foreach (var (end, bend) in Limbs)
        {
            Assert.True(SkeletonIk2D.Place(skeleton, local, end, rest[end], bend));
        }
        var world = new RigidTransform2D[skeleton.Count];
        skeleton.ToWorld(local, world);

        for (var joint = 0; joint < skeleton.Count; joint++)
        {
            var name = skeleton.Joints[joint].Name;
            Assert.True(Vector2f.Distance(evaluated.Points[name].XY, world[joint].Translation) < 1e-4f, $"{name}: evaluator {evaluated.Points[name].XY}, skeleton {world[joint].Translation}.");
            Assert.True(MathF.Abs(Angles.WrapSigned(evaluated.Angles[name] - world[joint].Rotation)) < 1e-4f, $"{name}: evaluator angle {evaluated.Angles[name]}, skeleton {world[joint].Rotation}.");
        }
        Assert.True(Vector2f.Distance(rest[PersonSkeleton.LeftElbow].Translation, world[PersonSkeleton.LeftElbow].Translation) > .05f, "The authored elbow rest is expected to differ from the solved one.");
    }
}
