using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Kinematics;

/// <summary>
/// How far a joint may move from its rest, measured on the rest's own axes: slide along X, slide along Y, and turn in radians.
/// An interval holding one value locks that motion, so the kind of joint is just which motions are locked.
/// </summary>
public readonly record struct JointLimits2D(Interval<float> X, Interval<float> Y, Interval<float> Turn)
{
    private static readonly Interval<float> Any = Interval.Entire<float>(), None = Interval<float>.Singleton(0);

    /// <summary>Slides and turns without limit.</summary>
    public static JointLimits2D Free { get; } = new(Any, Any, Any);
    /// <summary>Cannot move at all.</summary>
    public static JointLimits2D Fixed { get; } = new(None, None, None);

    /// <summary>Turns about its origin and cannot slide.</summary>
    public static JointLimits2D Hinge() => new(None, None, Any);
    /// <summary>Turns about its origin within <paramref name="turn"/> and cannot slide.</summary>
    public static JointLimits2D Hinge(Interval<float> turn) => new(None, None, turn);
    /// <summary>Slides along the rest X axis within <paramref name="travel"/> and cannot turn.</summary>
    public static JointLimits2D Slider(Interval<float> travel) => new(travel, None, None);

    /// <summary>The nearest transform to <paramref name="local"/> that these limits allow around <paramref name="rest"/>.</summary>
    public RigidTransform2D Clamp(RigidTransform2D rest, RigidTransform2D local)
    {
        if (this == Free) return local;
        var slide = Vector2f.Rotate(local.Translation - rest.Translation, -rest.Rotation);
        slide = Vector2f.Clamp(slide, new(X.Min, Y.Min), new(X.Max, Y.Max));
        return new(rest.Translation + Vector2f.Rotate(slide, rest.Rotation), rest.Rotation + Turn.Clamp(local.Rotation - rest.Rotation));
    }
}

/// <summary>
/// One joint of a <see cref="Skeleton2D"/>: where it sits and how it is turned in its parent's axes at rest. A bone is the
/// offset from a joint to its child, so a bone's length is the length of the child's rest translation.
/// <see cref="MoveScale"/> multiplies the moves a clip keys on this joint: a build with longer legs than the one the clips
/// were made for raises and lowers its hips by more. <see cref="Layer"/> is the joint's place in the drawing order, added to
/// its parent's; smaller is nearer the viewer. Nothing in kinematics reads it: it travels with the joint so that whatever is
/// drawn on the joint knows where it sorts.
/// </summary>
public readonly record struct Joint2D(string Name, int Parent, RigidTransform2D Rest, JointLimits2D Limits, float MoveScale = 1, float Layer = 0)
{
    /// <summary>A joint that may slide and turn freely.</summary>
    public Joint2D(string name, int parent, RigidTransform2D rest) : this(name, parent, rest, JointLimits2D.Free) { }
}
