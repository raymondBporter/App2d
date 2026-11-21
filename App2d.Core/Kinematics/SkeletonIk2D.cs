using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Kinematics;

/// <summary>Inverse kinematics that writes joint turns into a <see cref="Skeleton2D"/> pose.</summary>
public static class SkeletonIk2D
{
    /// <summary>
    /// Turns the two joints above <paramref name="end"/> so that it lands on <paramref name="target"/>, given in the skeleton's
    /// own space. Only those two turns change. <paramref name="bend"/> is 1 or -1: which side of the line to the target the
    /// middle joint folds to. Returns false when the target is out of reach; the limb then points at it.
    /// </summary>
    public static bool Reach(Skeleton2D skeleton, Span<RigidTransform2D> local, int end, Vector2f target, int bend)
    {
        var middle = skeleton.Joints[end].Parent;
        var root = skeleton.Joints[middle].Parent;
        var above = skeleton.Joints[root].Parent;
        var carrier = above == Skeleton2D.NoParent ? RigidTransform2D.Identity : skeleton.WorldOf(local, above);
        var rootWorld = local[root].Then(carrier);
        // Each bone is the child's offset, in the axes of the joint that carries it.
        var upper = local[middle].Translation;
        var lower = local[end].Translation;
        var solved = TwoBoneIk2D.SolveExact(rootWorld.Translation, target, upper.Length(), lower.Length(), bend, rootWorld.TransformDirection(upper));
        var rootTurn = solved.FirstAngle - upper.AngleRadians;
        var middleTurn = solved.SecondAngle - lower.AngleRadians;
        local[root] = local[root] with { Rotation = Near(rootTurn - carrier.Rotation, local[root].Rotation) };
        local[middle] = local[middle] with { Rotation = Near(middleTurn - rootTurn, local[middle].Rotation) };
        return solved.ReachesTarget;
    }

    /// <summary>
    /// <see cref="Reach"/> for <paramref name="target"/>'s position, then turns the end joint to the target's rotation, also
    /// in the skeleton's own space. A hand or foot then holds its angle whatever the limb above it does.
    /// </summary>
    public static bool Place(Skeleton2D skeleton, Span<RigidTransform2D> local, int end, RigidTransform2D target, int bend)
    {
        var reached = Reach(skeleton, local, end, target.Translation, bend);
        var middle = skeleton.WorldOf(local, skeleton.Joints[end].Parent);
        local[end] = local[end] with { Rotation = Near(target.Rotation - middle.Rotation, local[end].Rotation) };
        return reached;
    }

    /// <summary>Places every limb of the skeleton at its goal; <paramref name="goals"/> is in the order of <see cref="Skeleton2D.Limbs"/>.</summary>
    public static void Place(Skeleton2D skeleton, Span<RigidTransform2D> local, ReadOnlySpan<LimbGoal2D> goals)
    {
        for (var i = 0; i < goals.Length; i++) Place(skeleton, local, skeleton.Limbs[i].End, goals[i].Target, goals[i].Bend);
    }

    /// <summary>The same direction as <paramref name="angle"/>, written within half a turn of <paramref name="current"/>.</summary>
    private static float Near(float angle, float current) => current + Angles.WrapSigned(angle - current);
}
