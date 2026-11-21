using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Kinematics;

/// <summary>
/// A two-bone limb that IK places: its end joint, the side its middle joint folds to by default (1 or -1), and the joint
/// whose frame carries its target. An arm framed on its shoulder follows the shoulder; a leg with
/// <see cref="Skeleton2D.NoParent"/> stays where the ground is while the body moves. <see cref="TargetScale"/> multiplies
/// the moves a clip keys on its target, so a longer limb than the one the clips were made for reaches further.
/// </summary>
public readonly record struct Limb2D(int End, int Bend, int Frame = Skeleton2D.NoParent, float TargetScale = 1);

/// <summary>Where one limb's end should be, in the skeleton's own space, and the side its middle joint folds to.</summary>
public readonly record struct LimbGoal2D(RigidTransform2D Target, int Bend);
