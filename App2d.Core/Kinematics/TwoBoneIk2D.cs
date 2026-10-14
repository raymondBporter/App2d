using App2d.Core.Mathematics;
using App2d.Core.Validation;
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

    /// <summary>
    /// Rigid segments with exact inner/outer reach boundaries, including straight and fully folded poses.
    /// Uses double intermediates for unequal lengths. Solve retains the historical inset used by point rigs and demos.
    /// </summary>
    public static TwoBoneIkPose2D SolveExact(Vector2 root, Vector2 target, float firstLength, float secondLength, int bendDirection, Vector2? zeroTargetDirection = null)
    {
        ArgGuard.ThrowIfNotFinite(root); ArgGuard.ThrowIfNotFinite(target);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(firstLength); ArgGuard.ThrowIfNotFiniteOrNotPositive(secondLength);
        ArgGuard.ThrowIfZero(bendDirection);
        var fallback = zeroTargetDirection ?? Vector2.UnitX;
        ArgGuard.ThrowIfNotFinite(fallback);
        var fallbackLength = Math.Sqrt((double)fallback.X * fallback.X + (double)fallback.Y * fallback.Y);
        if (fallbackLength == 0) throw new ArgumentOutOfRangeException(nameof(zeroTargetDirection), "A fallback direction must be nonzero.");
        var dx = (double)target.X - root.X; var dy = (double)target.Y - root.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var x = distance > 0 ? dx / distance : fallback.X / fallbackLength; var y = distance > 0 ? dy / distance : fallback.Y / fallbackLength;
        var first = (double)firstLength; var second = (double)secondLength;
        var minimum = Math.Abs(first - second); var maximum = first + second;
        var reach = Math.Clamp(distance, minimum, maximum);
        // Float-authored rotations can put a straight tip a fraction of an ulp inside the circle. Without snapping,
        // the square root amplifies that noise into a visible bend. Bound the tolerance by the shorter segment too.
        var roundoff = Math.Min(maximum * 2e-7, Math.Min(first, second) * 1e-5);
        if (Math.Abs(distance - maximum) <= roundoff) reach = maximum;
        else if (Math.Abs(distance - minimum) <= roundoff) reach = minimum;
        // Equal segments can fold onto their origin. The perpendicular fallback fixes the otherwise free bend direction.
        var along = reach == 0 ? 0 : ((first - second) * (first + second) + reach * reach) / (2 * reach);
        var height = Math.Sqrt(Math.Max(0, first * first - along * along)) * Math.Sign(bendDirection);
        var joint = new Vector2((float)(root.X + x * along - y * height), (float)(root.Y + y * along + x * height));
        var end = new Vector2((float)(root.X + x * reach), (float)(root.Y + y * reach));
        ArgGuard.ThrowIfNotFinite(joint); ArgGuard.ThrowIfNotFinite(end);
        return new(root, joint, end, (joint - root).AngleRadians, (end - joint).AngleRadians, Math.Abs(distance - reach) <= .01);
    }

    public static TwoBoneIkPose2D Solve(
        Vector2 root,
        Vector2 target,
        float firstLength,
        float secondLength,
        int bendDirection)
    {
        ArgGuard.ThrowIfNotFinite(root);
        ArgGuard.ThrowIfNotFinite(target);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(firstLength);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(secondLength);
        ArgGuard.ThrowIfZero(bendDirection);

        var toTarget = target - root;
        var targetDistance = toTarget.Length();
        var direction = targetDistance > Epsilon ? toTarget / targetDistance : Vector2.UnitX;
        var minimumReach = MathF.Abs(firstLength - secondLength) + Epsilon;
        // A relative margin: a fixed one visibly bends a short limb at full reach (0.001 on a 0.66 arm is 6 degrees).
        var maximumReach = (firstLength + secondLength) * (1 - 1e-5f);
        var solvedDistance = Math.Clamp(targetDistance, minimumReach, maximumReach);

        var along = (firstLength * firstLength - secondLength * secondLength + solvedDistance * solvedDistance) /
            (2f * solvedDistance);
        var perpendicularDistance = MathF.Sqrt(MathF.Max(0f, firstLength * firstLength - along * along));
        var perpendicular = direction.PerpCcw * MathF.Sign(bendDirection);
        var joint = root + direction * along + perpendicular * perpendicularDistance;
        var end = root + direction * solvedDistance;

        return new TwoBoneIkPose2D(
            root,
            joint,
            end,
            (joint - root).AngleRadians,
            (end - joint).AngleRadians,
            MathF.Abs(targetDistance - solvedDistance) <= 0.01f);
    }
}

