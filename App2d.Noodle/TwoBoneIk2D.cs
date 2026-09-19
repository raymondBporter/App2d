using System.Numerics;

namespace App2d.Noodle;

internal readonly record struct TwoBoneIkPose(
    Vector2 Shoulder,
    Vector2 Elbow,
    Vector2 Wrist,
    float UpperAngle,
    float LowerAngle,
    bool ReachesTarget);

internal static class TwoBoneIk2D
{
    private const float Epsilon = 0.001f;

    public static TwoBoneIkPose Solve(
        Vector2 shoulder,
        Vector2 target,
        float upperLength,
        float lowerLength,
        int bendDirection)
    {
        var toTarget = target - shoulder;
        var targetDistance = toTarget.Length();
        var direction = targetDistance > Epsilon ? toTarget / targetDistance : Vector2.UnitX;
        var minimumReach = MathF.Abs(upperLength - lowerLength) + Epsilon;
        var maximumReach = upperLength + lowerLength - Epsilon;
        var solvedDistance = Math.Clamp(targetDistance, minimumReach, maximumReach);

        var along = (upperLength * upperLength - lowerLength * lowerLength + solvedDistance * solvedDistance) /
            (2f * solvedDistance);
        var perpendicularDistance = MathF.Sqrt(MathF.Max(0f, upperLength * upperLength - along * along));
        var perpendicular = new Vector2(-direction.Y, direction.X) * MathF.Sign(bendDirection);
        var elbow = shoulder + direction * along + perpendicular * perpendicularDistance;
        var wrist = shoulder + direction * solvedDistance;

        return new TwoBoneIkPose(
            shoulder,
            elbow,
            wrist,
            MathF.Atan2(elbow.Y - shoulder.Y, elbow.X - shoulder.X),
            MathF.Atan2(wrist.Y - elbow.Y, wrist.X - elbow.X),
            MathF.Abs(targetDistance - solvedDistance) <= 0.01f);
    }
}
