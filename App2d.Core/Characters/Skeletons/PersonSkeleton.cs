using App2d.Core.Animation;
using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

/// <summary>
/// The person skeleton, seen from the side and facing +X, in the same units as the art (about 1.8 tall). The constants are
/// joint indices into <see cref="Skeleton"/>. Each limb is a root that IK turns, a middle joint, and an end; a shoulder
/// is the pivot the arm hangs from and stays where the keys put it.
/// </summary>
public static class PersonSkeleton
{
    public const int Hips = 0, Chest = 1, Head = 2;
    public const int LeftShoulder = 3, LeftUpperArm = 4, LeftElbow = 5, LeftHand = 6;
    public const int LeftHip = 7, LeftKnee = 8, LeftFoot = 9;
    public const int RightShoulder = 10, RightUpperArm = 11, RightElbow = 12, RightHand = 13;
    public const int RightHip = 14, RightKnee = 15, RightFoot = 16;

    /// <summary>Indices into the skeleton's limbs. An arm's target rides on its shoulder; a leg's stays with the ground.</summary>
    public const int LeftArm = 0, LeftLeg = 1, RightArm = 2, RightLeg = 3;

    /// <summary>
    /// Indices into the skeleton's mounts. A grip sits in a hand and turns with the chest; a held sword or gun sits in the right
    /// hand and turns with the shoulder; the art mounts aim along the limb they dress, and the head's stays upright.
    /// </summary>
    public const int RightGrip = 0, LeftGrip = 1, Back = 2, BackView = 3, SwordHand = 4, GunHand = 5, HeadArt = 6, BodyArt = 7;
    public const int LeftHandArt = 8, RightHandArt = 9, LeftFootArt = 10, RightFootArt = 11, LeftShinArt = 12, RightShinArt = 13;

    /// <summary>What an upper-body action, such as a sword swing, takes over from whatever the legs are doing.</summary>
    public static SkeletonMask2D UpperBody { get; } = new([Chest, Head, LeftShoulder, RightShoulder], [LeftArm, RightArm],
        [RightGrip, LeftGrip, Back, BackView, SwordHand, GunHand, HeadArt, LeftHandArt, RightHandArt]);
    /// <summary>The hips and legs: what a gait owns underneath an upper-body action.</summary>
    public static SkeletonMask2D LowerBody { get; } = new([Hips, LeftHip, RightHip], [LeftLeg, RightLeg],
        [BodyArt, LeftFootArt, RightFootArt, LeftShinArt, RightShinArt]);

    /// <summary>The side each middle joint folds to.</summary>
    public const int ElbowBend = -1, KneeBend = 1;

    // Rest turns: the limbs hang slightly bent. Hands and feet turn back by the same amount, so they rest level.
    private const float ArmTurn = -1.4158f, ElbowTurn = -0.2155f, LegTurn = -1.3891f, KneeTurn = -0.3651f;

    /// <summary>The standard build.</summary>
    public static Skeleton2D Skeleton { get; } = Build();

    /// <summary>
    /// A person with scaled proportions; 1 is the standard build, which the clips are made for. Feet stay on the ground as the
    /// legs change. Keyed motion scales with the part it belongs to: hips, feet and stride with the legs, the upper body with
    /// the torso, and hands with the arms. The left limbs sit in front of the body in the drawing order and the right ones
    /// behind it; a wider build spreads them further apart.
    /// </summary>
    public static Skeleton2D Build(float legs = 1, float torso = 1, float arms = 1, float width = 1)
    {
        float upperArm = 0.3239f * arms, forearm = 0.3306f * arms, thigh = 0.3986f * legs, shin = 0.3946f * legs;
        return new(
            new("hips", Skeleton2D.NoParent, new(new(0, 0.025f + 0.78f * legs))) { MoveScale = legs },
            new("chest", Hips, new(new(0, 0.65f * torso))) { MoveScale = torso },
            new("head", Chest, new(new(0, 0.38f * torso))) { MoveScale = torso },

            new("left-shoulder", Chest, new(new(-0.1117f * width, -0.08f * torso))) { MoveScale = torso, Layer = -0.1434f * width },
            new("left-upper-arm", LeftShoulder, new(default, ArmTurn)),
            new("left-elbow", LeftUpperArm, new(new(upperArm, 0), ElbowTurn)),
            new("left-hand", LeftElbow, new(new(forearm, 0), -ArmTurn - ElbowTurn)),

            new("left-hip", Hips, new(new(-0.06f * width, 0), LegTurn)) { MoveScale = legs, Layer = -0.1039f * width },
            new("left-knee", LeftHip, new(new(thigh, 0), KneeTurn)),
            new("left-foot", LeftKnee, new(new(shin, 0), -LegTurn - KneeTurn)),

            new("right-shoulder", Chest, new(new(0.0683f * width, -0.08f * torso))) { MoveScale = torso, Layer = 0.1684f * width },
            new("right-upper-arm", RightShoulder, new(default, ArmTurn)),
            new("right-elbow", RightUpperArm, new(new(upperArm, 0), ElbowTurn)),
            new("right-hand", RightElbow, new(new(forearm, 0), -ArmTurn - ElbowTurn)),

            new("right-hip", Hips, new(new(0.06f * width, 0), LegTurn)) { MoveScale = legs, Layer = 0.1039f * width },
            new("right-knee", RightHip, new(new(thigh, 0), KneeTurn)),
            new("right-foot", RightKnee, new(new(shin, 0), -LegTurn - KneeTurn)))
        {
            Limbs =
            [
                new(LeftHand, ElbowBend, LeftShoulder, arms), new(LeftFoot, KneeBend, TargetScale: legs),
                new(RightHand, ElbowBend, RightShoulder, arms), new(RightFoot, KneeBend, TargetScale: legs),
            ],
            TravelScale = legs,
            Mounts =
            [
                Mount2D.Turning("right-grip", RightHand, Chest, new(default, 0.12f)), Mount2D.Turning("left-grip", LeftHand, Chest, new(default, 0.12f)),
                Mount2D.On("back", Chest, new(new(-0.1f, -0.08f), -1.6908f), 0.2f), Mount2D.On("back-view", Chest, new(new(-0.13f, 0.12f), -1.0208f), -0.2f),
                Mount2D.Turning("sword-hand", RightHand, RightShoulder), Mount2D.Turning("gun-hand", RightHand, RightShoulder),
                Mount2D.Upright("head-art", Head), Mount2D.Aimed("body-art", Hips, Chest),
                Mount2D.Aimed("left-hand-art", LeftHand, LeftElbow), Mount2D.Aimed("right-hand-art", RightHand, RightElbow),
                Mount2D.Aimed("left-foot-art", LeftFoot, LeftKnee), Mount2D.Aimed("right-foot-art", RightFoot, RightKnee),
                Mount2D.Aimed("left-shin-art", LeftFoot, LeftKnee, layer: -0.01f), Mount2D.Aimed("right-shin-art", RightFoot, RightKnee, layer: -0.01f),
            ],
        };
    }
}
