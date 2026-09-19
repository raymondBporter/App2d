using System.Numerics;

namespace App2d.Noodle;

internal static class StandardBones2D
{
    public const string Torso = "torso";
    public const string Head = "head";
    public const string UpperArmLeft = "upper_arm_l";
    public const string ForearmLeft = "forearm_l";
    public const string HandLeft = "hand_l";
    public const string UpperArmRight = "upper_arm_r";
    public const string ForearmRight = "forearm_r";
    public const string HandRight = "hand_r";
    public const string ThighLeft = "thigh_l";
    public const string ShinLeft = "shin_l";
    public const string FootLeft = "foot_l";
    public const string ThighRight = "thigh_r";
    public const string ShinRight = "shin_r";
    public const string FootRight = "foot_r";

    public const string HandSocketRight = "hand_r";
    public const string HeadTopSocket = "head_top";
}

internal readonly record struct BoneSegment2D(Vector2 Start, Vector2 End)
{
    public float Length => Vector2.Distance(Start, End);
    public float Angle => MathF.Atan2(End.Y - Start.Y, End.X - Start.X);
}

internal readonly record struct RigidSocket2D(Vector2 Position, float Angle);

internal sealed class SolvedRigidPose2D
{
    public int Facing { get; init; } = 1;
    public Dictionary<string, BoneSegment2D> Bones { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, RigidSocket2D> Sockets { get; } = new(StringComparer.Ordinal);
}

/// <summary>One fixed skeleton. Art sets are never allowed to change these proportions.</summary>
internal static class StandardSkeleton2D
{
    public const float UpperArmLength = 78f;
    public const float ForearmLength = 70f;
    public const float ThighLength = 103f;
    public const float ShinLength = 96f;
    public const float TorsoLength = 126f;
    public const float HeadRadius = 37f;

    public static SolvedRigidPose2D Solve(Vector2 hips, RigidPose2D pose, int facing)
    {
        facing = facing < 0 ? -1 : 1;
        var solved = new SolvedRigidPose2D { Facing = facing };
        var torsoAngle = pose.TorsoAngle * facing;
        var torsoStart = hips + new Vector2(0f, 5f);
        var chest = torsoStart + Rotate(new Vector2(0f, TorsoLength), torsoAngle);
        solved.Bones.Add(StandardBones2D.Torso, new BoneSegment2D(torsoStart, chest));

        var headAngle = torsoAngle + pose.HeadAngle * facing;
        var headCenter = chest + Rotate(new Vector2(0f, 48f), headAngle);
        // Unit length keeps a meaningful rigid transform for an eventual asymmetric
        // head PNG while the circle stand-in remains visually unchanged.
        solved.Bones.Add(StandardBones2D.Head,
            new BoneSegment2D(headCenter, headCenter + Rotate(Vector2.UnitX, headAngle)));

        var shoulderCenter = chest + Rotate(new Vector2(0f, -20f), torsoAngle);
        var shoulderLeft = shoulderCenter + new Vector2(-5f * facing, -2f);
        var shoulderRight = shoulderCenter + new Vector2(5f * facing, 2f);
        var hipLeft = hips + new Vector2(-7f * facing, 0f);
        var hipRight = hips + new Vector2(7f * facing, 0f);

        AddIkChain(
            solved,
            StandardBones2D.UpperArmLeft,
            StandardBones2D.ForearmLeft,
            StandardBones2D.HandLeft,
            shoulderLeft,
            ToWorld(hips, pose.HandLeft, facing),
            UpperArmLength,
            ForearmLength,
            -facing,
            handLength: 29f,
            facing);
        AddIkChain(
            solved,
            StandardBones2D.UpperArmRight,
            StandardBones2D.ForearmRight,
            StandardBones2D.HandRight,
            shoulderRight,
            ToWorld(hips, pose.HandRight, facing),
            UpperArmLength,
            ForearmLength,
            facing,
            handLength: 31f,
            facing);
        AddIkChain(
            solved,
            StandardBones2D.ThighLeft,
            StandardBones2D.ShinLeft,
            StandardBones2D.FootLeft,
            hipLeft,
            ToWorld(hips, pose.FootLeft, facing),
            ThighLength,
            ShinLength,
            facing,
            handLength: 45f,
            facing,
            foot: true);
        AddIkChain(
            solved,
            StandardBones2D.ThighRight,
            StandardBones2D.ShinRight,
            StandardBones2D.FootRight,
            hipRight,
            ToWorld(hips, pose.FootRight, facing),
            ThighLength,
            ShinLength,
            facing,
            handLength: 45f,
            facing,
            foot: true);

        var rightHand = solved.Bones[StandardBones2D.HandRight];
        solved.Sockets.Add(StandardBones2D.HandSocketRight,
            new RigidSocket2D(rightHand.End, rightHand.Angle));
        solved.Sockets.Add(StandardBones2D.HeadTopSocket,
            new RigidSocket2D(headCenter + Rotate(new Vector2(0f, HeadRadius), headAngle), headAngle));
        return solved;
    }

    private static void AddIkChain(
        SolvedRigidPose2D solved,
        string upperName,
        string lowerName,
        string endName,
        Vector2 root,
        Vector2 target,
        float upperLength,
        float lowerLength,
        int bendDirection,
        float handLength,
        int facing,
        bool foot = false)
    {
        var ik = TwoBoneIk2D.Solve(root, target, upperLength, lowerLength, bendDirection);
        solved.Bones.Add(upperName, new BoneSegment2D(ik.Shoulder, ik.Elbow));
        solved.Bones.Add(lowerName, new BoneSegment2D(ik.Elbow, ik.Wrist));
        if (foot)
        {
            var heel = ik.Wrist - new Vector2(9f * facing, 0f);
            solved.Bones.Add(endName, new BoneSegment2D(heel, heel + new Vector2(handLength * facing, 0f)));
            return;
        }

        var direction = ik.Wrist - ik.Elbow;
        direction = direction.LengthSquared() > 0.001f ? Vector2.Normalize(direction) : new Vector2(facing, 0f);
        solved.Bones.Add(endName, new BoneSegment2D(ik.Wrist, ik.Wrist + direction * handLength));
    }

    public static Vector2 ToWorld(Vector2 hips, Vector2 local, int facing) =>
        hips + new Vector2(local.X * facing, local.Y);

    public static Vector2 ToLocal(Vector2 hips, Vector2 world, int facing) =>
        new((world.X - hips.X) * facing, world.Y - hips.Y);

    private static Vector2 Rotate(Vector2 value, float angle) => Vector2.Transform(value, Matrix3x2.CreateRotation(angle));
}
