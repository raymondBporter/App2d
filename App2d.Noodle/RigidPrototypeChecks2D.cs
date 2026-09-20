using App2d.Core.Curves;
using System.Numerics;

namespace App2d.Noodle;

/// <summary>Fast deterministic checks for the parts that a renderer smoke test cannot prove.</summary>
internal static class RigidPrototypeChecks2D
{
    public static void Run()
    {
        CheckPartSetsMatchSkeleton();
        CheckSkeletonMirrorsAndKeepsLengths();
        CheckSplineSilhouettes();
        CheckSideViewBlend();
        CheckPoseInterpolation();
        CheckStanceFootStaysInWorldSpace();
    }

    private static void CheckSplineSilhouettes()
    {
        var limb = SplineSilhouette2D.CreateLimb(80f, 16f, 12f, 8f);
        Require(limb.Area > 2_000f, "Spline limb should produce a substantial closed silhouette.");
        Require(limb.LocalBounds.Left < 0f && limb.LocalBounds.Right > 80f,
            "Spline limb joint caps should overlap both ends of its bone.");

        var torso = SplineSilhouette2D.CreateTorso(StandardSkeleton2D.TorsoLength);
        Require(torso.Area > limb.Area, "Spline torso should be larger than one limb.");

        var start = new Vector2(1f, 2f);
        var end = new Vector2(9f, -3f);
        var curve = new CubicBezier2D(start, new(3f, 8f), new(7f, 5f), end);
        var atStart = curve.Evaluate(0f);
        var atEnd = curve.Evaluate(1f);
        Require(Vector2.DistanceSquared(start, atStart) < 0.0001f, "Cubic evaluation must preserve its start point.");
        Require(Vector2.DistanceSquared(end, atEnd) < 0.0001f, "Cubic evaluation must preserve its end point.");
    }

    private static void CheckSideViewBlend()
    {
        var demo = new RigidCharacterDemo2D();
        var idleView = demo.SideViewAmount;
        demo.Update(0.1f, 1);
        Require(demo.SideViewAmount > idleView, "Movement should turn SplineMan toward a side profile.");
        demo.Update(0.2f, 0);
        RequireClose(demo.SideViewAmount, idleView, "idle three-quarter view blend");
    }

    private static void CheckPartSetsMatchSkeleton()
    {
        var expectedBones = new HashSet<string>(StringComparer.Ordinal)
        {
            StandardBones2D.Torso,
            StandardBones2D.Head,
            StandardBones2D.UpperArmLeft,
            StandardBones2D.ForearmLeft,
            StandardBones2D.HandLeft,
            StandardBones2D.UpperArmRight,
            StandardBones2D.ForearmRight,
            StandardBones2D.HandRight,
            StandardBones2D.ThighLeft,
            StandardBones2D.ShinLeft,
            StandardBones2D.FootLeft,
            StandardBones2D.ThighRight,
            StandardBones2D.ShinRight,
            StandardBones2D.FootRight
        };

        foreach (var set in RigidPartSet2D.PrototypeSets)
        {
            var mappedBones = set.Parts.Select(part => part.Bone).ToHashSet(StringComparer.Ordinal);
            Require(mappedBones.SetEquals(expectedBones), $"Part set {set.Name} does not map every standard bone exactly once.");
            Require(set.Parts.Select(part => part.Bone).Distinct(StringComparer.Ordinal).Count() == set.Parts.Count,
                $"Part set {set.Name} maps a bone more than once.");
        }
    }

    private static void CheckSkeletonMirrorsAndKeepsLengths()
    {
        var hips = new Vector2(31f, -70f);
        var right = StandardSkeleton2D.Solve(hips, RigidPose2D.Neutral, 1);
        var left = StandardSkeleton2D.Solve(hips, RigidPose2D.Neutral, -1);
        RequireClose(right.Bones[StandardBones2D.UpperArmRight].Length, StandardSkeleton2D.UpperArmLength, "upper arm length");
        RequireClose(right.Bones[StandardBones2D.ForearmRight].Length, StandardSkeleton2D.ForearmLength, "forearm length");
        RequireClose(right.Bones[StandardBones2D.ThighRight].Length, StandardSkeleton2D.ThighLength, "thigh length");
        RequireClose(right.Bones[StandardBones2D.ShinRight].Length, StandardSkeleton2D.ShinLength, "shin length");

        var rightHand = right.Sockets[StandardBones2D.HandSocketRight].Position;
        var leftHand = left.Sockets[StandardBones2D.HandSocketRight].Position;
        RequireClose(rightHand.X - hips.X, -(leftHand.X - hips.X), "mirrored hand X");
        RequireClose(rightHand.Y, leftHand.Y, "mirrored hand Y");
    }

    private static void CheckPoseInterpolation()
    {
        var start = RigidPose2D.Neutral;
        var end = start with { HandRight = new Vector2(160f, 100f), TorsoAngle = 0.2f };
        var clip = new PoseClip2D("check", false, new(0f, start), new(1f, end));
        var middle = clip.Sample(0.5f);
        RequireClose(middle.HandRight.X, (start.HandRight.X + end.HandRight.X) * 0.5f, "pose midpoint");
        RequireClose(middle.TorsoAngle, 0.1f, "angle midpoint");
    }

    private static void CheckStanceFootStaysInWorldSpace()
    {
        var locomotion = new ProceduralLocomotion2D();
        var hipsA = new Vector2(0f, -70f);
        locomotion.Reset(hipsA, RigidCharacterDemo2D.GroundY);
        var poseA = locomotion.SampleGrounded(hipsA, RigidCharacterDemo2D.GroundY, 120f, 0.001f);
        var worldA = hipsA + poseA.FootLeft;

        var hipsB = new Vector2(9f, -70f);
        var poseB = locomotion.SampleGrounded(hipsB, RigidCharacterDemo2D.GroundY, 120f, 0.001f);
        var worldB = hipsB + poseB.FootLeft;
        Require(locomotion.LeftFootPlanted, "Expected the checked foot to be in stance.");
        RequireClose(worldA.X, worldB.X, "planted foot world X");
        RequireClose(worldA.Y, worldB.Y, "planted foot world Y");
    }

    private static void RequireClose(float actual, float expected, string label)
    {
        if (MathF.Abs(actual - expected) > 0.02f)
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
