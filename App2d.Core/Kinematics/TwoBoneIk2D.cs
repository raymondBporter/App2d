using System.Numerics;

namespace App2d.Core.Kinematics;

public readonly record struct TwoBoneIkPose2D(
    Vector2 Root,
    Vector2 Joint,
    Vector2 End,
    float FirstAngle,
    float SecondAngle,
    bool ReachesTarget);

public static class TwoBoneIk2D
{
    private const float Epsilon = 0.001f;

    public static TwoBoneIkPose2D Solve(
        Vector2 root,
        Vector2 target,
        float firstLength,
        float secondLength,
        int bendDirection)
    {
        ArgGuard.ThrowIfNotFinite(root);
        ArgGuard.ThrowIfNotFinite(target);
        ArgGuard.ThrowIfNotPositive(firstLength);
        ArgGuard.ThrowIfNotPositive(secondLength);
        if (bendDirection == 0)
            throw new ArgumentOutOfRangeException(nameof(bendDirection), "Bend direction cannot be zero.");

        var toTarget = target - root;
        var targetDistance = toTarget.Length();
        var direction = targetDistance > Epsilon ? toTarget / targetDistance : Vector2.UnitX;
        var minimumReach = MathF.Abs(firstLength - secondLength) + Epsilon;
        var maximumReach = firstLength + secondLength - Epsilon;
        var solvedDistance = Math.Clamp(targetDistance, minimumReach, maximumReach);

        var along = (firstLength * firstLength - secondLength * secondLength + solvedDistance * solvedDistance) /
            (2f * solvedDistance);
        var perpendicularDistance = MathF.Sqrt(MathF.Max(0f, firstLength * firstLength - along * along));
        var perpendicular = new Vector2(-direction.Y, direction.X) * MathF.Sign(bendDirection);
        var joint = root + direction * along + perpendicular * perpendicularDistance;
        var end = root + direction * solvedDistance;

        return new TwoBoneIkPose2D(
            root,
            joint,
            end,
            MathF.Atan2(joint.Y - root.Y, joint.X - root.X),
            MathF.Atan2(end.Y - joint.Y, end.X - joint.X),
            MathF.Abs(targetDistance - solvedDistance) <= 0.01f);
    }
}

