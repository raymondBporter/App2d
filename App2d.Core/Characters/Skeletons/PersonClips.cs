using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.Skeletons.PersonSkeleton;

namespace App2d.Core.Characters.Skeletons;

/// <summary>The person animations, for <see cref="PersonSkeleton"/>. Values are changes from rest, in the skeleton's own units.</summary>
public static class PersonClips
{
    private static readonly Interpolator<Vector2f, float> Straight = Interpolation.Default<Vector2f, float>();
    private static readonly Interpolator<Vector2f, float> Smooth = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth);
    private static readonly Interpolator<Vector2f, float> SmoothStraightX = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth).With(Vector2f.Channels.X, Interpolation.Linear<float>());
    private static readonly Interpolator<float, float> StraightTurn = Interpolation.Linear<float>();
    private static readonly Interpolator<float, float> SmoothTurn = Interpolation.Linear<float>().WithProgress(Easingf.Smooth);
    private static readonly Interpolator<Vector3f, float> StraightOrient = Interpolation.Default<Vector3f, float>();
    private static readonly Interpolator<Vector3f, float> SmoothOrient = Interpolation.Default<Vector3f, float>().WithProgress(Easingf.Smooth);

    public static SkeletonClip2D ClubCavemanSlam { get; } = new("club-caveman-slam", 1.8f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, -0.032f)), new(0.55f, new(-0.048f, -0.016f)), new(0.7f, new(-0.048f, -0.016f), Straight),
                    new(0.78f, new(0.064f, -0.176f)), new(0.95f, new(0.064f, -0.192f)), new(1.35f, new(0.064f, -0.192f)),
                    new(1.8f, new(-0.008f, -0.032f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.55f, new(-0.1608f, -0.0202f)),
                    new(0.7f, new(-0.1796f, -0.0253f), Straight), new(0.78f, new(0.2531f, -0.0513f)),
                    new(0.95f, new(0.265f, -0.0565f)), new(1.35f, new(0.265f, -0.0565f)), new(1.8f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.55f, 0.25f), new(0.7f, 0.28f, StraightTurn), new(0.78f, -0.4f), new(0.95f, -0.42f), new(1.35f, -0.42f),
                    new(1.8f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.55f, new(0.0076f, -0.0001f)),
                    new(0.7f, new(0.0114f, -0.0002f), Straight), new(0.78f, new(-0.0455f, -0.0027f)),
                    new(0.95f, new(-0.053f, -0.0037f)), new(1.35f, new(-0.053f, -0.0037f)),
                    new(1.8f, new(-0.0076f, -0.0001f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn,
                    new(0f, 0.33f), new(0.55f, 2.15f), new(0.7f, 2.27f, StraightTurn), new(0.78f, -0.5f), new(0.95f, -0.63f), new(1.35f, -0.63f),
                    new(1.8f, 0.33f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.55f, new(-0.0444f, 0.1892f)), new(0.7f, new(-0.0363f, 0.194f), Straight),
                    new(0.78f, new(-0.0584f, 0.2037f)), new(0.95f, new(-0.0678f, 0.1951f)), new(1.35f, new(-0.0678f, 0.1951f)),
                    new(1.8f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0138f, 0.0317f)), new(0.55f, new(0.3195f, 0.291f)), new(0.7f, new(0.2908f, 0.223f), Straight),
                    new(0.78f, new(0.3858f, 0.6457f)), new(0.95f, new(0.3978f, 0.6872f)), new(1.35f, new(0.3978f, 0.6872f)),
                    new(1.8f, new(0.0138f, 0.0317f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.4189f, 0f, 0f)), new(0.55f, new(0.733f, -0.2443f, 0f)), new(0.7f, new(0.733f, -0.2443f, 0f)),
                new(0.78f, new(0.3142f, 0f, 0f)), new(1.35f, new(0.3142f, 0f, 0f)), new(1.8f, new(0.4189f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.8f, new(-0.112f, 0f)), new(RightLeg, 0f, 1.8f, new(0.112f, 0f))],
        Markers = [new("windup", 0.1f), new("strike", 0.78f), new("recover", 0.95f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.5f, "determined"), new(0.78f, "angry")),
    };

    public static SkeletonClip2D PersonDeath { get; } = new("person-death", 1.1f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.036f, -0.02f), Straight), new(0.0167f, new(-0.104f, -0.048f), Straight), new(0.075f, new(-0.128f, -0.064f)),
                    new(0.25f, new(-0.16f, -0.16f)), new(0.75f, new(-0.224f, -0.648f)), new(1.1f, new(-0.24f, -0.632f))),
                Turn: new(SmoothTurn,
                    new(0f, 0.16f, StraightTurn), new(0.0167f, 0.34f, StraightTurn), new(0.075f, 0.37f), new(0.25f, 0.53f), new(0.75f, 1.42f),
                    new(1.1f, 1.45f))),
            new(Chest,
                Turn: new(SmoothTurn,
                    new(0f, 0.08f, StraightTurn), new(0.0167f, 0.18f, StraightTurn), new(0.075f, 0.18f), new(0.25f, 0.12f), new(0.75f, 0.05f),
                    new(1.1f, 0.05f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0.1f)), new(0.0167f, new(0.24f, 0.18f)), new(0.075f, new(0.24f, 0.18f), Smooth),
                    new(0.4f, new(0.15f, 0.25f), Smooth), new(1.1f, new(-0.1f, 0.05f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.05f, 0.06f)), new(0.0167f, new(-0.14f, 0.18f)), new(0.075f, new(-0.14f, 0.18f), Smooth),
                    new(0.4f, new(0.15f, 0.25f), Smooth), new(1.1f, new(-0.05f, 0f)))),
        ],
        Mounts =
        [
            new(RightGrip, new(SmoothOrient,
                new(0f, new(0f, 0f, 0f), StraightOrient), new(0.075f, new(0f, 0f, 0f)), new(0.75f, new(0f, 0f, -1.47f)),
                new(1.1f, new(0f, 0f, -1.5f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.1f, new(0f, 0f)), new(RightLeg, 0f, 1.1f, new(0f, 0f))],
        Markers = [new("impact", 0.75f)],
        Expression = Track.Held<string>(new(0f, "hurt"), new(0.75f, "knocked-out")),
    };

    public static SkeletonClip2D PersonHammerSlam { get; } = new("person-hammer-slam", 1.5f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, -0.032f)), new(0.55f, new(-0.048f, -0.016f)), new(0.7f, new(-0.048f, -0.016f), Straight),
                    new(0.78f, new(0.064f, -0.176f)), new(0.95f, new(0.064f, -0.192f)), new(1.5f, new(-0.008f, -0.032f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.55f, new(-0.1608f, -0.0202f)),
                    new(0.7f, new(-0.1796f, -0.0253f), Straight), new(0.78f, new(0.2531f, -0.0513f)),
                    new(0.95f, new(0.265f, -0.0565f)), new(1.5f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.55f, 0.25f), new(0.7f, 0.28f, StraightTurn), new(0.78f, -0.4f), new(0.95f, -0.42f), new(1.5f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.55f, new(0.0076f, -0.0001f)),
                    new(0.7f, new(0.0114f, -0.0002f), Straight), new(0.78f, new(-0.0455f, -0.0027f)),
                    new(0.95f, new(-0.053f, -0.0037f)), new(1.5f, new(-0.0076f, -0.0001f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn,
                    new(0f, 0.33f), new(0.55f, 2.15f), new(0.7f, 2.27f, StraightTurn), new(0.78f, -0.5f), new(0.95f, -0.63f), new(1.5f, 0.33f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.55f, new(-0.0444f, 0.1892f)), new(0.7f, new(-0.0363f, 0.194f), Straight),
                    new(0.78f, new(-0.0584f, 0.2037f)), new(0.95f, new(-0.0678f, 0.1951f)), new(1.5f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0138f, 0.0317f)), new(0.55f, new(0.3195f, 0.291f)), new(0.7f, new(0.2908f, 0.223f), Straight),
                    new(0.78f, new(0.3858f, 0.6457f)), new(0.95f, new(0.3978f, 0.6872f)), new(1.5f, new(0.0138f, 0.0317f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.4189f, 0f, 0f)), new(0.55f, new(0.733f, -0.2443f, 0f)), new(0.7f, new(0.733f, -0.2443f, 0f)),
                new(0.78f, new(0.3142f, 0f, 0f)), new(0.95f, new(0.3142f, 0f, 0f)), new(1.5f, new(0.4189f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.5f, new(-0.112f, 0f)), new(RightLeg, 0f, 1.5f, new(0.112f, 0f))],
        Markers = [new("windup", 0.1f), new("strike", 0.78f), new("recover", 0.95f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.5f, "determined"), new(0.78f, "angry")),
    };

    public static SkeletonClip2D PersonHeavyWalk { get; } = new("person-heavy-walk", 1.56f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.0681f)), new(0.0975f, new(0f, -0.0614f)), new(0.2925f, new(0f, -0.0288f)), new(0.39f, new(0f, -0.0221f)),
                    new(0.4875f, new(0f, -0.0288f)), new(0.6825f, new(0f, -0.0614f)), new(0.78f, new(0f, -0.0681f)), new(0.8775f, new(0f, -0.0614f)),
                    new(1.0725f, new(0f, -0.0288f)), new(1.17f, new(0f, -0.0221f)), new(1.2675f, new(0f, -0.0288f)), new(1.4625f, new(0f, -0.0614f)),
                    new(1.56f, new(0f, -0.0681f)))),
            new(Chest, Move: Track.Of<Vector2f>(new(0f, new(0.05f, 0f)), new(1.56f, new(0.05f, 0f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.0992f, 0.04f)), new(0.0975f, new(0.0896f, 0.04f)), new(0.195f, new(0.0623f, 0.04f)),
                    new(0.2925f, new(0.0214f, 0.04f)), new(0.4875f, new(-0.075f, 0.04f)), new(0.585f, new(-0.1159f, 0.04f)),
                    new(0.6825f, new(-0.1432f, 0.04f)), new(0.78f, new(-0.1528f, 0.04f)), new(0.8775f, new(-0.1432f, 0.04f)),
                    new(0.975f, new(-0.1159f, 0.04f)), new(1.0725f, new(-0.075f, 0.04f)), new(1.2675f, new(0.0214f, 0.04f)),
                    new(1.365f, new(0.0623f, 0.04f)), new(1.4625f, new(0.0896f, 0.04f)), new(1.56f, new(0.0992f, 0.04f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.0975f, new(-0.1078f, 0.0141f)), new(0.195f, new(-0.0875f, 0.048f)),
                    new(0.2925f, new(-0.0484f, 0.0819f)), new(0.39f, new(0f, 0.096f)), new(0.4875f, new(0.0484f, 0.0819f)),
                    new(0.585f, new(0.0875f, 0.048f)), new(0.6825f, new(0.1078f, 0.0141f)), new(0.78f, new(0.1f, 0f)), new(1.56f, new(-0.1f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1592f, 0.04f)), new(0.0975f, new(-0.1496f, 0.04f)), new(0.195f, new(-0.1223f, 0.04f)),
                    new(0.2925f, new(-0.0814f, 0.04f)), new(0.4875f, new(0.015f, 0.04f)), new(0.585f, new(0.0559f, 0.04f)),
                    new(0.6825f, new(0.0832f, 0.04f)), new(0.78f, new(0.0928f, 0.04f)), new(0.8775f, new(0.0832f, 0.04f)),
                    new(0.975f, new(0.0559f, 0.04f)), new(1.0725f, new(0.015f, 0.04f)), new(1.2675f, new(-0.0814f, 0.04f)),
                    new(1.365f, new(-0.1223f, 0.04f)), new(1.4625f, new(-0.1496f, 0.04f)), new(1.56f, new(-0.1592f, 0.04f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0f)), new(0.78f, new(-0.1f, 0f)), new(0.8775f, new(-0.1078f, 0.0141f)), new(0.975f, new(-0.0875f, 0.048f)),
                    new(1.0725f, new(-0.0484f, 0.0819f)), new(1.17f, new(0f, 0.096f)), new(1.2675f, new(0.0484f, 0.0819f)),
                    new(1.365f, new(0.0875f, 0.048f)), new(1.4625f, new(0.1078f, 0.0141f)), new(1.56f, new(0.1f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.78f, new(0.1f, 0f)), new(LeftLeg, 0.78f, 1.56f, new(0.3f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(1.56f, new(0.4f, 0f))),
    };

    public static SkeletonClip2D PersonHit { get; } = new("person-hit", 0.3f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.036f, -0.02f)), new(0.0167f, new(-0.08f, -0.044f)), new(0.05f, new(-0.08f, -0.044f), Smooth),
                    new(0.17f, new(-0.024f, -0.012f), Smooth), new(0.3f, new(0f, 0f))),
                Turn: Track.Linear(
                    new(0f, 0.16f), new(0.0167f, 0.32f), new(0.05f, 0.32f, SmoothTurn), new(0.17f, 0.08f, SmoothTurn), new(0.3f, 0f))),
            new(Chest,
                Turn: Track.Linear(
                    new(0f, 0.08f), new(0.0167f, 0.16f), new(0.05f, 0.16f, SmoothTurn), new(0.17f, 0.04f, SmoothTurn), new(0.3f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0.1f)), new(0.0167f, new(0.22f, 0.16f)), new(0.05f, new(0.22f, 0.16f), Smooth), new(0.3f, new(0f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.05f, 0.06f)), new(0.0167f, new(-0.12f, 0.16f)), new(0.05f, new(-0.12f, 0.16f), Smooth),
                    new(0.3f, new(0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.3f, new(0f, 0f)), new(RightLeg, 0f, 0.3f, new(0f, 0f))],
        Expression = Track.Constant("hurt"),
    };

    public static SkeletonClip2D PersonIdle { get; } = new("person-idle", 2f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(0f, -0.0096f)), new(2f, new(0f, 0f)))),
            new(Chest, Turn: new(SmoothTurn, new(0f, 0f), new(1f, 0.018f), new(2f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(-0.01f, 0.02f)), new(2f, new(0f, 0f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(0.01f, 0.02f)), new(2f, new(0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 2f, new(0f, 0f)), new(RightLeg, 0f, 2f, new(0f, 0f))],
    };

    public static SkeletonClip2D PersonJump { get; } = new("person-jump", 0.9f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.14f, new(0f, -0.112f)), new(0.24f, new(0f, 0.032f)), new(0.5f, new(0f, 0f)), new(0.9f, new(0f, 0f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.14f, new(-0.12f, 0.08f)), new(0.26f, new(0.2f, 0.45f)), new(0.9f, new(0.06f, 0.12f)))),
            new(LeftLeg,
                Target: new(Smooth, new(0f, new(0f, 0f)), new(0.2f, new(0f, 0f)), new(0.36f, new(0.096f, 0.256f)), new(0.9f, new(0.048f, 0.064f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.14f, new(-0.12f, 0.08f)), new(0.26f, new(0.22f, 0.42f)), new(0.9f, new(0.08f, 0.12f)))),
            new(RightLeg,
                Target: new(Smooth, new(0f, new(0f, 0f)), new(0.2f, new(0f, 0f)), new(0.36f, new(-0.032f, 0.192f)), new(0.9f, new(0.016f, 0.048f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.2f, new(0f, 0f)), new(RightLeg, 0f, 0.2f, new(0f, 0f))],
        Markers = [new("launch", 0.2f), new("apex", 0.5f)],
    };

    public static SkeletonClip2D PersonPistolShot { get; } = new("person-pistol-shot", 1.1f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, -0.032f)), new(0.35f, new(-0.016f, -0.024f)), new(0.6f, new(-0.016f, -0.028f), Straight),
                    new(0.85f, new(-0.016f, -0.024f)), new(1.1f, new(-0.008f, -0.032f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.35f, new(0f, 0f)), new(0.6f, new(0f, 0f), Straight),
                    new(0.63f, new(-0.039f, -0.0012f)), new(0.85f, new(0f, 0f)), new(1.1f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.35f, 0f), new(0.6f, 0f, StraightTurn), new(0.63f, 0.06f), new(0.85f, 0f), new(1.1f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.6f, new(-0.0076f, -0.0001f), Straight), new(0.63f, new(-0.019f, -0.0005f)),
                    new(0.85f, new(-0.0076f, -0.0001f)), new(1.1f, new(-0.0076f, -0.0001f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn,
                    new(0f, 0.03f), new(0.35f, 0f), new(0.6f, 0f, StraightTurn), new(0.63f, 0.32f), new(0.85f, 0f), new(1.1f, 0.03f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.35f, new(-0.01f, 0.05f)), new(0.6f, new(-0.01f, 0.05f), Straight),
                    new(0.63f, new(-0.056f, 0.0505f)), new(0.85f, new(-0.01f, 0.05f)), new(1.1f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.05f, 0.065f)), new(0.35f, new(0.615f, 0.63f)), new(0.6f, new(0.615f, 0.63f), Straight),
                    new(0.63f, new(0.4715f, 0.4497f)), new(0.85f, new(0.615f, 0.63f)), new(1.1f, new(0.05f, 0.065f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(
                new(0f, new(-0.2094f, 0.1745f, 0f)), new(0.58f, new(-0.2094f, 0.1745f, 0f)), new(0.65f, new(-0.4538f, 0.2443f, 0f)),
                new(1.1f, new(-0.2094f, 0.1745f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.1f, new(-0.052f, 0f)), new(RightLeg, 0f, 1.1f, new(0.052f, 0f))],
        Markers = [new("fire", 0.6f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.6f, "determined"), new(0.85f, "focused")),
    };

    public static SkeletonClip2D PersonRun { get; } = new("person-run", 0.72f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.044f)), new(0.02f, new(0f, -0.058f)), new(0.04f, new(0f, -0.0749f)), new(0.06f, new(0f, -0.0902f)),
                    new(0.08f, new(0f, -0.0992f)), new(0.1f, new(0f, -0.0995f)), new(0.12f, new(0f, -0.0955f)), new(0.14f, new(0f, -0.0885f)),
                    new(0.16f, new(0f, -0.0795f)), new(0.2f, new(0f, -0.0594f)), new(0.22f, new(0f, -0.0501f)), new(0.24f, new(0f, -0.0373f)),
                    new(0.26f, new(0f, -0.0231f)), new(0.28f, new(0f, -0.0134f)), new(0.3f, new(0f, -0.013f)), new(0.32f, new(0f, -0.0204f)),
                    new(0.34f, new(0f, -0.0319f)), new(0.36f, new(0f, -0.044f)), new(0.38f, new(0f, -0.058f)), new(0.4f, new(0f, -0.0749f)),
                    new(0.42f, new(0f, -0.0902f)), new(0.44f, new(0f, -0.0992f)), new(0.46f, new(0f, -0.0995f)), new(0.48f, new(0f, -0.0955f)),
                    new(0.5f, new(0f, -0.0885f)), new(0.52f, new(0f, -0.0795f)), new(0.56f, new(0f, -0.0594f)), new(0.58f, new(0f, -0.0501f)),
                    new(0.6f, new(0f, -0.0373f)), new(0.62f, new(0f, -0.0231f)), new(0.64f, new(0f, -0.0134f)), new(0.66f, new(0f, -0.013f)),
                    new(0.68f, new(0f, -0.0204f)), new(0.7f, new(0f, -0.0319f)), new(0.72f, new(0f, -0.044f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.12f, -0.0071f)), new(0.02f, new(0.1171f, -0.006f)),
                    new(0.04f, new(0.1164f, -0.0048f)), new(0.06f, new(0.118f, -0.0056f)),
                    new(0.08f, new(0.1217f, -0.0084f)), new(0.1f, new(0.1271f, -0.0123f)),
                    new(0.12f, new(0.1335f, -0.0152f)), new(0.14f, new(0.1401f, -0.0176f)),
                    new(0.16f, new(0.1461f, -0.0197f)), new(0.18f, new(0.1509f, -0.0211f)),
                    new(0.2f, new(0.1538f, -0.0219f)), new(0.22f, new(0.1545f, -0.0218f)),
                    new(0.24f, new(0.1529f, -0.0225f)), new(0.26f, new(0.1492f, -0.0221f)),
                    new(0.28f, new(0.1438f, -0.0195f)), new(0.3f, new(0.1375f, -0.015f)), new(0.32f, new(0.1309f, -0.0109f)),
                    new(0.34f, new(0.1248f, -0.0082f)), new(0.36f, new(0.12f, -0.0071f)),
                    new(0.38f, new(0.1171f, -0.006f)), new(0.4f, new(0.1164f, -0.0048f)),
                    new(0.42f, new(0.118f, -0.0056f)), new(0.44f, new(0.1217f, -0.0084f)),
                    new(0.46f, new(0.1271f, -0.0123f)), new(0.48f, new(0.1335f, -0.0152f)), new(0.5f, new(0.1401f, -0.0176f)),
                    new(0.52f, new(0.1461f, -0.0197f)), new(0.54f, new(0.1509f, -0.0211f)), new(0.56f, new(0.1538f, -0.0219f)),
                    new(0.58f, new(0.1545f, -0.0218f)), new(0.6f, new(0.1529f, -0.0225f)), new(0.62f, new(0.1492f, -0.0221f)),
                    new(0.64f, new(0.1438f, -0.0195f)), new(0.66f, new(0.1375f, -0.015f)), new(0.68f, new(0.1309f, -0.0109f)),
                    new(0.7f, new(0.1248f, -0.0082f)), new(0.72f, new(0.12f, -0.0071f)))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0889f, -0.0034f)), new(0.02f, new(0.0868f, -0.0024f)),
                    new(0.04f, new(0.0863f, -0.0005f)), new(0.06f, new(0.0875f, -0.0009f)),
                    new(0.08f, new(0.0902f, -0.0042f)), new(0.1f, new(0.0942f, -0.0097f)),
                    new(0.12f, new(0.0989f, -0.0142f)), new(0.14f, new(0.1037f, -0.0174f)),
                    new(0.16f, new(0.1081f, -0.02f)), new(0.18f, new(0.1116f, -0.0217f)),
                    new(0.2f, new(0.1137f, -0.0226f)), new(0.22f, new(0.1142f, -0.0225f)),
                    new(0.24f, new(0.1131f, -0.023f)), new(0.26f, new(0.1104f, -0.0238f)),
                    new(0.28f, new(0.1065f, -0.0212f)), new(0.3f, new(0.1018f, -0.0156f)), new(0.32f, new(0.0969f, -0.0093f)),
                    new(0.34f, new(0.0925f, -0.0052f)), new(0.36f, new(0.0889f, -0.0034f)),
                    new(0.38f, new(0.0868f, -0.0024f)), new(0.4f, new(0.0863f, -0.0005f)),
                    new(0.42f, new(0.0875f, -0.0009f)), new(0.44f, new(0.0902f, -0.0042f)),
                    new(0.46f, new(0.0942f, -0.0097f)), new(0.48f, new(0.0989f, -0.0142f)), new(0.5f, new(0.1037f, -0.0174f)),
                    new(0.52f, new(0.1081f, -0.02f)), new(0.54f, new(0.1116f, -0.0217f)), new(0.56f, new(0.1137f, -0.0226f)),
                    new(0.58f, new(0.1142f, -0.0225f)), new(0.6f, new(0.1131f, -0.023f)), new(0.62f, new(0.1104f, -0.0238f)),
                    new(0.64f, new(0.1065f, -0.0212f)), new(0.66f, new(0.1018f, -0.0156f)),
                    new(0.68f, new(0.0969f, -0.0093f)), new(0.7f, new(0.0925f, -0.0052f)),
                    new(0.72f, new(0.0889f, -0.0034f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0166f, 0.022f)), new(0.02f, new(0.0174f, 0.0214f)),
                    new(0.04f, new(0.0171f, 0.0213f)), new(0.06f, new(0.0157f, 0.0216f)),
                    new(0.08f, new(0.0132f, 0.0223f)), new(0.1f, new(0.0098f, 0.0234f)),
                    new(0.12f, new(0.0056f, 0.0246f)), new(0.14f, new(0.0007f, 0.0259f)),
                    new(0.16f, new(-0.0045f, 0.0272f)), new(0.18f, new(-0.0099f, 0.0281f)),
                    new(0.2f, new(-0.0153f, 0.0287f)), new(0.22f, new(-0.0206f, 0.0288f)),
                    new(0.24f, new(-0.0255f, 0.0285f)), new(0.26f, new(-0.03f, 0.0278f)),
                    new(0.28f, new(-0.0339f, 0.0267f)), new(0.3f, new(-0.0371f, 0.0254f)),
                    new(0.32f, new(-0.0396f, 0.0241f)), new(0.34f, new(-0.0413f, 0.0229f)),
                    new(0.36f, new(-0.0423f, 0.022f)), new(0.38f, new(-0.0426f, 0.0214f)),
                    new(0.4f, new(-0.0421f, 0.0213f)), new(0.42f, new(-0.041f, 0.0216f)),
                    new(0.44f, new(-0.0392f, 0.0223f)), new(0.46f, new(-0.0367f, 0.0234f)),
                    new(0.48f, new(-0.0337f, 0.0246f)), new(0.5f, new(-0.03f, 0.0259f)),
                    new(0.52f, new(-0.0258f, 0.0272f)), new(0.54f, new(-0.0211f, 0.0281f)),
                    new(0.56f, new(-0.0162f, 0.0287f)), new(0.58f, new(-0.011f, 0.0288f)),
                    new(0.6f, new(-0.0058f, 0.0285f)), new(0.62f, new(-0.0007f, 0.0278f)),
                    new(0.64f, new(0.004f, 0.0267f)), new(0.66f, new(0.0083f, 0.0254f)),
                    new(0.68f, new(0.0119f, 0.0241f)), new(0.7f, new(0.0148f, 0.0229f)), new(0.72f, new(0.0166f, 0.022f)))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.021f, 0f)), new(0.02f, new(-0.0185f, 0f)), new(0.04f, new(-0.0155f, 0f)), new(0.1f, new(-0.0053f, 0f)),
                    new(0.16f, new(0.0056f, 0f)), new(0.2f, new(0.0125f, 0f)), new(0.22f, new(0.0164f, 0f)), new(0.24f, new(0.0199f, 0f)),
                    new(0.26f, new(0.0224f, 0f)), new(0.28f, new(0.023f, 0f)), new(0.3f, new(0.0213f, 0f)), new(0.32f, new(0.0183f, 0f)),
                    new(0.34f, new(0.0147f, 0f)), new(0.36f, new(0.0115f, 0f)), new(0.6f, new(-0.0225f, 0f)), new(0.62f, new(-0.0251f, 0f)),
                    new(0.64f, new(-0.0267f, 0f)), new(0.66f, new(-0.0267f, 0f)), new(0.68f, new(-0.0254f, 0f)), new(0.7f, new(-0.0235f, 0f)),
                    new(0.72f, new(-0.021f, 0f)))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0454f, -0.0112f)), new(0.02f, new(-0.0455f, -0.011f)), new(0.04f, new(-0.045f, -0.0109f)),
                    new(0.06f, new(-0.044f, -0.0111f)), new(0.08f, new(-0.0424f, -0.0114f)), new(0.1f, new(-0.0402f, -0.0118f)),
                    new(0.12f, new(-0.0375f, -0.0123f)), new(0.14f, new(-0.0342f, -0.0129f)), new(0.16f, new(-0.0304f, -0.0133f)),
                    new(0.18f, new(-0.0261f, -0.0137f)), new(0.2f, new(-0.0213f, -0.0139f)), new(0.22f, new(-0.0162f, -0.014f)),
                    new(0.26f, new(-0.0056f, -0.0136f)), new(0.28f, new(-0.0004f, -0.0131f)), new(0.3f, new(0.0042f, -0.0126f)),
                    new(0.32f, new(0.0083f, -0.0121f)), new(0.34f, new(0.0114f, -0.0116f)), new(0.36f, new(0.0135f, -0.0112f)),
                    new(0.38f, new(0.0145f, -0.011f)), new(0.4f, new(0.0142f, -0.0109f)), new(0.42f, new(0.0127f, -0.0111f)),
                    new(0.44f, new(0.01f, -0.0114f)), new(0.46f, new(0.0063f, -0.0118f)), new(0.48f, new(0.0017f, -0.0123f)),
                    new(0.5f, new(-0.0035f, -0.0129f)), new(0.56f, new(-0.0204f, -0.0139f)), new(0.58f, new(-0.0258f, -0.014f)),
                    new(0.6f, new(-0.0306f, -0.0138f)), new(0.62f, new(-0.0348f, -0.0136f)), new(0.64f, new(-0.0384f, -0.0131f)),
                    new(0.66f, new(-0.0412f, -0.0126f)), new(0.68f, new(-0.0433f, -0.0121f)), new(0.7f, new(-0.0447f, -0.0116f)),
                    new(0.72f, new(-0.0454f, -0.0112f)))),
            new(RightHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0115f, 0f)), new(0.24f, new(-0.0225f, 0f)), new(0.26f, new(-0.0251f, 0f)), new(0.28f, new(-0.0267f, 0f)),
                    new(0.3f, new(-0.0267f, 0f)), new(0.32f, new(-0.0254f, 0f)), new(0.34f, new(-0.0235f, 0f)), new(0.38f, new(-0.0185f, 0f)),
                    new(0.4f, new(-0.0155f, 0f)), new(0.46f, new(-0.0053f, 0f)), new(0.52f, new(0.0056f, 0f)), new(0.56f, new(0.0125f, 0f)),
                    new(0.58f, new(0.0164f, 0f)), new(0.6f, new(0.0199f, 0f)), new(0.62f, new(0.0224f, 0f)), new(0.64f, new(0.023f, 0f)),
                    new(0.66f, new(0.0213f, 0f)), new(0.68f, new(0.0183f, 0f)), new(0.7f, new(0.0147f, 0f)), new(0.72f, new(0.0115f, 0f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.2278f, 0.3875f)), new(0.02f, new(0.2323f, 0.3806f)), new(0.04f, new(0.2281f, 0.3714f)),
                    new(0.06f, new(0.2153f, 0.36f)), new(0.08f, new(0.1942f, 0.3469f)),
                    new(0.1f, new(0.1655f, 0.3329f)), new(0.12f, new(0.1301f, 0.3186f)), new(0.14f, new(0.0893f, 0.3047f)),
                    new(0.16f, new(0.0444f, 0.2918f)), new(0.18f, new(-0.0028f, 0.2803f)),
                    new(0.2f, new(-0.0508f, 0.2703f)), new(0.22f, new(-0.0979f, 0.262f)),
                    new(0.24f, new(-0.1427f, 0.2552f)), new(0.26f, new(-0.1837f, 0.2499f)),
                    new(0.28f, new(-0.2199f, 0.2461f)), new(0.3f, new(-0.2504f, 0.244f)), new(0.32f, new(-0.2744f, 0.2437f)),
                    new(0.34f, new(-0.2916f, 0.2457f)), new(0.36f, new(-0.3016f, 0.2499f)), new(0.38f, new(-0.3043f, 0.2567f)),
                    new(0.4f, new(-0.2996f, 0.266f)), new(0.42f, new(-0.2878f, 0.2774f)),
                    new(0.44f, new(-0.269f, 0.2905f)), new(0.46f, new(-0.2437f, 0.3046f)),
                    new(0.48f, new(-0.2123f, 0.3191f)), new(0.5f, new(-0.1756f, 0.3331f)), new(0.52f, new(-0.1345f, 0.3462f)),
                    new(0.54f, new(-0.0902f, 0.3578f)), new(0.56f, new(-0.0441f, 0.3679f)), new(0.58f, new(0.0026f, 0.3762f)),
                    new(0.6f, new(0.0484f, 0.383f)), new(0.62f, new(0.0918f, 0.3882f)), new(0.64f, new(0.1313f, 0.3918f)),
                    new(0.66f, new(0.1657f, 0.3937f)), new(0.68f, new(0.1939f, 0.3938f)), new(0.7f, new(0.2148f, 0.3918f)),
                    new(0.72f, new(0.2278f, 0.3875f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.2629f, 0.3358f)), new(0.02f, new(-0.2309f, 0.3674f)), new(0.04f, new(-0.1942f, 0.3873f)),
                    new(0.06f, new(-0.1529f, 0.3984f)), new(0.08f, new(-0.1092f, 0.4022f)), new(0.1f, new(-0.0657f, 0.4002f)),
                    new(0.12f, new(-0.0218f, 0.3921f)), new(0.14f, new(0.0238f, 0.3772f)), new(0.16f, new(0.0694f, 0.3564f)),
                    new(0.18f, new(0.1131f, 0.3304f)), new(0.2f, new(0.1563f, 0.2988f)), new(0.22f, new(0.2047f, 0.2588f)),
                    new(0.24f, new(0.2493f, 0.2146f)), new(0.26f, new(0.2801f, 0.1709f)), new(0.28f, new(0.287f, 0.1323f)),
                    new(0.3f, new(0.2664f, 0.0976f)), new(0.32f, new(0.2282f, 0.0648f)), new(0.34f, new(0.1836f, 0.0327f)),
                    new(0.36f, new(0.144f, 0f)), new(0.56f, new(-0.2116f, 0f)), new(0.58f, new(-0.2464f, 0.0049f)), new(0.6f, new(-0.2815f, 0.0412f)),
                    new(0.62f, new(-0.3141f, 0.0899f)), new(0.64f, new(-0.3339f, 0.141f)), new(0.66f, new(-0.3332f, 0.1887f)),
                    new(0.68f, new(-0.318f, 0.2404f)), new(0.7f, new(-0.2932f, 0.2916f)), new(0.72f, new(-0.2629f, 0.3358f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.3016f, 0.2499f)), new(0.02f, new(-0.3043f, 0.2567f)),
                    new(0.04f, new(-0.2996f, 0.266f)), new(0.06f, new(-0.2878f, 0.2774f)),
                    new(0.08f, new(-0.269f, 0.2905f)), new(0.1f, new(-0.2437f, 0.3046f)),
                    new(0.12f, new(-0.2123f, 0.3191f)), new(0.14f, new(-0.1756f, 0.3331f)), new(0.16f, new(-0.1345f, 0.3462f)),
                    new(0.18f, new(-0.0902f, 0.3578f)), new(0.2f, new(-0.0441f, 0.3679f)),
                    new(0.22f, new(0.0026f, 0.3762f)), new(0.24f, new(0.0484f, 0.383f)),
                    new(0.26f, new(0.0918f, 0.3882f)), new(0.28f, new(0.1313f, 0.3918f)), new(0.3f, new(0.1657f, 0.3937f)),
                    new(0.32f, new(0.1939f, 0.3938f)), new(0.34f, new(0.2148f, 0.3918f)), new(0.36f, new(0.2278f, 0.3875f)),
                    new(0.38f, new(0.2323f, 0.3806f)), new(0.4f, new(0.2281f, 0.3714f)),
                    new(0.42f, new(0.2153f, 0.36f)), new(0.44f, new(0.1942f, 0.3469f)), new(0.46f, new(0.1655f, 0.3329f)),
                    new(0.48f, new(0.1301f, 0.3186f)), new(0.5f, new(0.0893f, 0.3047f)), new(0.52f, new(0.0444f, 0.2918f)),
                    new(0.54f, new(-0.0028f, 0.2803f)), new(0.56f, new(-0.0508f, 0.2703f)), new(0.58f, new(-0.0979f, 0.262f)),
                    new(0.6f, new(-0.1427f, 0.2552f)), new(0.62f, new(-0.1837f, 0.2499f)), new(0.64f, new(-0.2199f, 0.2461f)),
                    new(0.66f, new(-0.2504f, 0.244f)), new(0.68f, new(-0.2744f, 0.2437f)), new(0.7f, new(-0.2916f, 0.2457f)),
                    new(0.72f, new(-0.3016f, 0.2499f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.144f, 0f)), new(0.2f, new(-0.2116f, 0f)), new(0.22f, new(-0.2464f, 0.0049f)), new(0.24f, new(-0.2815f, 0.0412f)),
                    new(0.26f, new(-0.3141f, 0.0899f)), new(0.28f, new(-0.3339f, 0.141f)), new(0.3f, new(-0.3332f, 0.1887f)),
                    new(0.32f, new(-0.318f, 0.2404f)), new(0.34f, new(-0.2932f, 0.2916f)), new(0.36f, new(-0.2629f, 0.3358f)),
                    new(0.38f, new(-0.2309f, 0.3674f)), new(0.4f, new(-0.1942f, 0.3873f)), new(0.42f, new(-0.1529f, 0.3984f)),
                    new(0.44f, new(-0.1092f, 0.4022f)), new(0.46f, new(-0.0657f, 0.4002f)), new(0.48f, new(-0.0218f, 0.3921f)),
                    new(0.5f, new(0.0238f, 0.3772f)), new(0.52f, new(0.0694f, 0.3564f)), new(0.54f, new(0.1131f, 0.3304f)),
                    new(0.56f, new(0.1563f, 0.2988f)), new(0.58f, new(0.2047f, 0.2588f)), new(0.6f, new(0.2493f, 0.2146f)),
                    new(0.62f, new(0.2801f, 0.1709f)), new(0.64f, new(0.287f, 0.1323f)), new(0.66f, new(0.2664f, 0.0976f)),
                    new(0.68f, new(0.2282f, 0.0648f)), new(0.7f, new(0.1836f, 0.0327f)), new(0.72f, new(0.144f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.216f, new(0.144f, 0f)), new(LeftLeg, 0.36f, 0.576f, new(0.784f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(0.72f, new(1.28f, 0f))),
    };

    public static SkeletonClip2D PersonThrust { get; } = new("person-thrust", 0.9f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.32f, new(-0.032f, -0.024f)), new(0.42f, new(0.072f, -0.04f), Straight),
                    new(0.58f, new(0.072f, -0.04f)), new(0.9f, new(0f, 0f)))),
            new(Chest, Turn: new(SmoothTurn, new(0f, 0f), new(0.32f, 0.14f), new(0.42f, -0.16f, StraightTurn), new(0.58f, -0.16f), new(0.9f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.32f, new(0.12f, 0.1f)), new(0.42f, new(-0.18f, 0.12f), Straight), new(0.58f, new(-0.18f, 0.12f)),
                    new(0.9f, new(0f, 0f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.32f, new(-0.33f, 0.35f)), new(0.42f, new(0.57f, 0.5f), Straight), new(0.58f, new(0.57f, 0.5f)),
                    new(0.9f, new(0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.9f, new(0f, 0f)), new(RightLeg, 0f, 0.9f, new(0f, 0f))],
        Markers = [new("windup", 0.05f), new("strike", 0.4f), new("recover", 0.58f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.36f, "determined"), new(0.7f, "focused")),
    };

    public static SkeletonClip2D PersonWalk { get; } = new("person-walk", 1.2f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.02f)), new(0.075f, new(0f, -0.0158f)), new(0.225f, new(0f, 0.0046f)), new(0.3f, new(0f, 0.0088f)),
                    new(0.375f, new(0f, 0.0046f)), new(0.525f, new(0f, -0.0158f)), new(0.6f, new(0f, -0.02f)), new(0.675f, new(0f, -0.0158f)),
                    new(0.825f, new(0f, 0.0046f)), new(0.9f, new(0f, 0.0088f)), new(0.975f, new(0f, 0.0046f)), new(1.125f, new(0f, -0.0158f)),
                    new(1.2f, new(0f, -0.02f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.15f, 0.04f)), new(0.075f, new(0.1363f, 0.04f)), new(0.15f, new(0.0973f, 0.04f)), new(0.225f, new(0.0389f, 0.04f)),
                    new(0.375f, new(-0.0989f, 0.04f)), new(0.45f, new(-0.1573f, 0.04f)), new(0.525f, new(-0.1963f, 0.04f)),
                    new(0.6f, new(-0.21f, 0.04f)), new(0.675f, new(-0.1963f, 0.04f)), new(0.75f, new(-0.1573f, 0.04f)),
                    new(0.825f, new(-0.0989f, 0.04f)), new(0.975f, new(0.0389f, 0.04f)), new(1.05f, new(0.0973f, 0.04f)),
                    new(1.125f, new(0.1363f, 0.04f)), new(1.2f, new(0.15f, 0.04f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.075f, new(-0.1078f, 0.0141f)), new(0.15f, new(-0.0875f, 0.048f)),
                    new(0.225f, new(-0.0484f, 0.0819f)), new(0.3f, new(0f, 0.096f)), new(0.375f, new(0.0484f, 0.0819f)),
                    new(0.45f, new(0.0875f, 0.048f)), new(0.525f, new(0.1078f, 0.0141f)), new(0.6f, new(0.1f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.21f, 0.04f)), new(0.075f, new(-0.1963f, 0.04f)), new(0.15f, new(-0.1573f, 0.04f)),
                    new(0.225f, new(-0.0989f, 0.04f)), new(0.375f, new(0.0389f, 0.04f)), new(0.45f, new(0.0973f, 0.04f)),
                    new(0.525f, new(0.1363f, 0.04f)), new(0.6f, new(0.15f, 0.04f)), new(0.675f, new(0.1363f, 0.04f)), new(0.75f, new(0.0973f, 0.04f)),
                    new(0.825f, new(0.0389f, 0.04f)), new(0.975f, new(-0.0989f, 0.04f)), new(1.05f, new(-0.1573f, 0.04f)),
                    new(1.125f, new(-0.1963f, 0.04f)), new(1.2f, new(-0.21f, 0.04f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0f)), new(0.6f, new(-0.1f, 0f)), new(0.675f, new(-0.1078f, 0.0141f)), new(0.75f, new(-0.0875f, 0.048f)),
                    new(0.825f, new(-0.0484f, 0.0819f)), new(0.9f, new(0f, 0.096f)), new(0.975f, new(0.0484f, 0.0819f)),
                    new(1.05f, new(0.0875f, 0.048f)), new(1.125f, new(0.1078f, 0.0141f)), new(1.2f, new(0.1f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.6f, new(0.1f, 0f)), new(LeftLeg, 0.6f, 1.2f, new(0.3f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(1.2f, new(0.4f, 0f))),
    };

    public static SkeletonClip2D PlayerBalanceBackward { get; } = new("player-balance-backward", 1f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0.04f, -0.032f)), new(0.5f, new(0.056f, -0.04f)), new(1f, new(0.04f, -0.032f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.1545f, -0.0186f)), new(0.125f, new(0.1186f, -0.0109f)), new(0.25f, new(0.1036f, -0.0083f)),
                    new(0.375f, new(0.1186f, -0.0109f)), new(0.5f, new(0.1545f, -0.0186f)), new(0.625f, new(0.19f, -0.0284f)),
                    new(0.75f, new(0.2045f, -0.033f)), new(0.875f, new(0.19f, -0.0284f)), new(1f, new(0.1545f, -0.0186f))),
                Turn: Track.Linear(
                    new(0f, -0.24f), new(0.125f, -0.1834f), new(0.25f, -0.16f), new(0.375f, -0.1834f), new(0.625f, -0.2966f), new(0.75f, -0.32f),
                    new(0.875f, -0.2966f), new(1f, -0.24f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0379f, -0.0019f)), new(0.125f, new(0.0486f, -0.0031f)), new(0.25f, new(0.053f, -0.0037f)),
                    new(0.375f, new(0.0486f, -0.0031f)), new(0.625f, new(0.0272f, -0.001f)), new(0.75f, new(0.0228f, -0.0007f)),
                    new(0.875f, new(0.0272f, -0.001f)), new(1f, new(0.0379f, -0.0019f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.3306f, 0.8037f)), new(0.125f, new(0.3997f, 0.4674f)), new(0.25f, new(0.209f, 0.1954f)),
                    new(0.375f, new(-0.096f, 0.1402f)), new(0.5f, new(-0.3431f, 0.302f)), new(0.625f, new(-0.4191f, 0.5916f)),
                    new(0.75f, new(-0.2755f, 0.8708f)), new(0.875f, new(0.0365f, 0.9727f)), new(1f, new(0.3306f, 0.8037f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.3431f, 0.302f)), new(0.125f, new(-0.4232f, 0.6359f)), new(0.25f, new(-0.2372f, 0.9071f)),
                    new(0.375f, new(0.0725f, 0.9631f)), new(0.5f, new(0.3306f, 0.8037f)), new(0.625f, new(0.4176f, 0.5171f)),
                    new(0.75f, new(0.2784f, 0.2393f)), new(0.875f, new(-0.038f, 0.136f)), new(1f, new(-0.3431f, 0.302f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1f, new(0.016f, 0f)), new(RightLeg, 0f, 1f, new(0.032f, 0f))],
        Expression = Track.Constant("worried"),
    };

    public static SkeletonClip2D PlayerBalanceForward { get; } = new("player-balance-forward", 1f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.048f, -0.024f)), new(0.5f, new(-0.064f, -0.032f)), new(1f, new(-0.048f, -0.024f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0778f, -0.0047f)), new(0.125f, new(-0.1142f, -0.0101f)), new(0.25f, new(-0.1291f, -0.013f)),
                    new(0.375f, new(-0.1142f, -0.0101f)), new(0.5f, new(-0.0778f, -0.0047f)), new(0.625f, new(-0.0412f, -0.0013f)),
                    new(0.75f, new(-0.026f, -0.0005f)), new(0.875f, new(-0.0412f, -0.0013f)),
                    new(1f, new(-0.0778f, -0.0047f))),
                Turn: Track.Linear(
                    new(0f, 0.12f), new(0.125f, 0.1766f), new(0.25f, 0.2f), new(0.375f, 0.1766f), new(0.625f, 0.0634f), new(0.75f, 0.04f),
                    new(0.875f, 0.0634f), new(1f, 0.12f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.105f, -0.0148f)), new(0.125f, new(0.1153f, -0.0179f)), new(0.25f, new(0.1195f, -0.0193f)),
                    new(0.375f, new(0.1153f, -0.0179f)), new(0.625f, new(0.0946f, -0.012f)), new(0.75f, new(0.0903f, -0.0109f)),
                    new(0.875f, new(0.0946f, -0.012f)), new(1f, new(0.105f, -0.0148f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.3617f, 0.6668f)), new(0.125f, new(0.1762f, 0.907f)), new(0.25f, new(-0.1333f, 0.9636f)),
                    new(0.375f, new(-0.403f, 0.7754f)), new(0.5f, new(-0.4456f, 0.4346f)), new(0.625f, new(-0.2186f, 0.1718f)),
                    new(0.75f, new(0.114f, 0.157f)), new(0.875f, new(0.3421f, 0.368f)), new(1f, new(0.3617f, 0.6668f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.4456f, 0.4346f)), new(0.125f, new(-0.2714f, 0.1961f)), new(0.25f, new(0.0336f, 0.1404f)),
                    new(0.375f, new(0.3078f, 0.3278f)), new(0.5f, new(0.3617f, 0.6668f)), new(0.625f, new(0.1459f, 0.9286f)),
                    new(0.75f, new(-0.182f, 0.9432f)), new(0.875f, new(-0.4147f, 0.7324f)), new(1f, new(-0.4456f, 0.4346f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1f, new(-0.016f, 0f)), new(RightLeg, 0f, 1f, new(0f, 0f))],
        Expression = Track.Constant("panic"),
    };

    public static SkeletonClip2D PlayerCelebrate { get; } = new("player-celebrate", 1.4f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, 0.0096f)), new(0.16f, new(-0.008f, -0.128f)), new(0.4f, new(0f, 0.096f), Straight),
                    new(0.62f, new(-0.008f, -0.096f)), new(0.86f, new(-0.008f, 0.0096f)), new(1.4f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.16f, new(0.1164f, -0.0105f)),
                    new(0.4f, new(-0.039f, -0.0012f), Straight), new(0.62f, new(0.0519f, -0.0021f)),
                    new(0.86f, new(-0.0195f, -0.0003f)), new(1.4f, new(-0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.16f, -0.18f), new(0.4f, 0.06f, StraightTurn), new(0.62f, -0.08f), new(0.86f, 0.03f), new(1.4f, 0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.16f, new(-0.0379f, -0.0019f)),
                    new(0.4f, new(-0.0304f, -0.0012f), Straight), new(0.62f, new(-0.0228f, -0.0007f)),
                    new(0.86f, new(-0.0304f, -0.0012f)), new(1.4f, new(-0.0304f, -0.0012f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.16f, new(-0.0585f, 0.1366f)), new(0.4f, new(-0.3194f, 1.1683f), Straight),
                    new(0.62f, new(-0.3626f, 1.0447f)), new(0.86f, new(-0.3349f, 1.1594f)), new(1.02f, new(-0.3003f, 0.9783f)),
                    new(1.18f, new(-0.3349f, 1.1594f)), new(1.4f, new(-0.3352f, 1.1494f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.052f, 0f)), new(0.16f, new(-0.052f, 0f)), new(0.4f, new(-0.048f, 0.124f), Straight), new(0.62f, new(-0.052f, 0f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675f, 0.0677f)), new(0.16f, new(-0.0156f, 0.1241f)), new(0.4f, new(0.2994f, 1.1311f), Straight),
                    new(0.62f, new(0.2155f, 1.091f)), new(0.86f, new(0.2849f, 1.1408f)), new(1.02f, new(0.2395f, 0.9621f)),
                    new(1.18f, new(0.2849f, 1.1408f)), new(1.4f, new(0.2846f, 1.1308f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.052f, 0f)), new(0.16f, new(0.052f, 0f)), new(0.4f, new(0.032f, 0.172f), Straight), new(0.62f, new(0.052f, 0f)))),
        ],
        Contacts = [
            new(LeftLeg, 0f, 0.3f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.3f, new(0.052f, 0f)), new(LeftLeg, 0.62f, 1.4f, new(-0.052f, 0f)),
            new(RightLeg, 0.62f, 1.4f, new(0.052f, 0f))],
        Expression = Track.Held<string>(new(0f, "happy"), new(0.3f, "delighted")),
    };

    public static SkeletonClip2D PlayerClimb { get; } = new("player-climb", 0.8f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.08f)), new(0.2f, new(0f, -0.064f)), new(0.4f, new(0f, -0.08f)), new(0.6f, new(0f, -0.064f)),
                    new(0.8f, new(0f, -0.08f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(new(0f, new(0.013f, -0.0001f)), new(0.8f, new(0.013f, -0.0001f))),
                Turn: Track.Linear(new(0f, -0.02f), new(0.8f, -0.02f)),
                Layer: Track.Linear(new(0f, -0.35f), new(0.8f, -0.35f))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(new(0f, new(0.2917f, 0f)), new(0.8f, new(0.2917f, 0f))),
                Layer: Track.Linear(new(0f, 0.1184f), new(0.8f, 0.1184f))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(new(0f, new(0.144f, 0f)), new(0.8f, new(0.144f, 0f))),
                Layer: Track.Linear(new(0f, 0.0539f), new(0.8f, 0.0539f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(new(0f, new(-0.2483f, 0f)), new(0.8f, new(-0.2483f, 0f))),
                Layer: Track.Linear(new(0f, -0.1934f), new(0.8f, -0.1934f))),
            new(RightHip,
                Move: Track.Of<Vector2f>(new(0f, new(-0.144f, 0f)), new(0.8f, new(-0.144f, 0f))),
                Layer: Track.Linear(new(0f, -0.1539f), new(0.8f, -0.1539f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1289f, 1.137f)), new(0.2f, new(0.1343f, 0.867f)), new(0.4f, new(0.1389f, 0.6371f)),
                    new(0.6f, new(0.2123f, 0.9686f)), new(0.8f, new(0.1289f, 1.137f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.128f, 0.02f)), new(0.2f, new(0.176f, 0.284f)), new(0.4f, new(0.128f, 0.46f)), new(0.8f, new(0.128f, 0.02f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.2209f, 0.6227f)), new(0.2f, new(-0.3075f, 0.951f)), new(0.4f, new(-0.2309f, 1.1226f)),
                    new(0.6f, new(-0.2255f, 0.8526f)), new(0.8f, new(-0.2209f, 0.6227f))),
                Bend: Track.Constant(1)),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.128f, 0.46f)), new(0.4f, new(-0.128f, 0.02f)), new(0.6f, new(-0.176f, 0.284f)), new(0.8f, new(-0.128f, 0.46f))),
                Bend: Track.Constant(-1)),
        ],
        Markers = [new("view-back", 0f), new("rung-right", 0f), new("rung-left", 0.4f)],
        Expression = Track.Constant(""),
    };

    public static SkeletonClip2D PlayerClimbOff { get; } = new("player-climb-off", 0.3f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.08f)), new(0.08f, new(0f, -0.056f)), new(0.15f, new(0f, -0.024f)), new(0.21f, new(0f, 0.008f)),
                    new(0.3f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.013f, -0.0001f)), new(0.15f, new(0.013f, -0.0001f)), new(0.18f, new(0.0121f, -0.0001f)),
                    new(0.24f, new(0.0017f, 0f)), new(0.27f, new(0f, 0f)), new(0.3f, new(0f, 0f))),
                Turn: Track.Linear(
                    new(0f, -0.02f), new(0.15f, -0.02f), new(0.18f, -0.0187f), new(0.24f, -0.0027f), new(0.27f, 0f), new(0.3f, 0f)),
                Layer: Track.Linear(
                    new(0f, -0.35f), new(0.15f, -0.35f), new(0.18f, -0.3267f), new(0.24f, -0.0467f), new(0.27f, 0f), new(0.3f, 0f))),
            new(Head, Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.15f, new(0f, 0f)), new(0.3f, new(-0.0076f, -0.0001f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.2917f, 0f)), new(0.15f, new(0.2917f, 0f)), new(0.18f, new(0.2659f, 0f)),
                    new(0.21f, new(0.2135f, 0f)), new(0.24f, new(0.1435f, 0f)), new(0.27f, new(0.068f, 0f)),
                    new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0.1184f), new(0.15f, 0.1184f), new(0.18f, 0.0473f), new(0.21f, -0.0071f),
                    new(0.24f, -0.0355f), new(0.27f, -0.033f), new(0.3f, 0f))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.144f, 0f)), new(0.15f, new(0.144f, 0f)), new(0.18f, new(0.1357f, 0f)), new(0.21f, new(0.1122f, 0f)),
                    new(0.24f, new(0.0777f, 0f)), new(0.27f, new(0.038f, 0f)), new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0.0539f), new(0.15f, 0.0539f), new(0.18f, 0.0084f), new(0.21f, -0.0119f),
                    new(0.24f, -0.0169f), new(0.27f, -0.0154f), new(0.3f, 0f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.2483f, 0f)), new(0.15f, new(-0.2483f, 0f)), new(0.18f, new(-0.243f, 0f)),
                    new(0.21f, new(-0.2074f, 0f)), new(0.24f, new(-0.1477f, 0f)), new(0.27f, new(-0.0744f, 0f)),
                    new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.1934f), new(0.15f, -0.1934f), new(0.18f, -0.118f), new(0.21f, -0.0513f),
                    new(0.24f, -0.0049f), new(0.27f, 0.0132f), new(0.3f, 0f))),
            new(RightHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.144f, 0f)), new(0.15f, new(-0.144f, 0f)), new(0.18f, new(-0.1357f, 0f)),
                    new(0.21f, new(-0.1122f, 0f)), new(0.24f, new(-0.0777f, 0f)), new(0.27f, new(-0.038f, 0f)),
                    new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.1539f), new(0.15f, -0.1539f), new(0.18f, -0.1018f), new(0.21f, -0.0414f),
                    new(0.24f, 0.0035f), new(0.27f, 0.0154f), new(0.3f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1289f, 1.137f)), new(0.08f, new(0.4213f, 1.0891f)), new(0.0917f, new(0.5563f, 0.9067f)),
                    new(0.1033f, new(0.6099f, 0.6628f)), new(0.115f, new(0.5661f, 0.417f)),
                    new(0.1267f, new(0.4315f, 0.2066f)), new(0.1383f, new(0.2267f, 0.0637f)),
                    new(0.15f, new(0.0628f, 0.0117f)), new(0.21f, new(-0.0231f, 0.005f)), new(0.3f, new(0.02f, 0.05f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.128f, 0.02f)), new(0.08f, new(0.128f, 0.004f)), new(0.15f, new(0.128f, 0f)),
                    new(0.21f, new(0.112f, 0f)), new(0.3f, new(-0.052f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.2209f, 0.6227f)), new(0.08f, new(-0.4975f, 1.0207f)), new(0.0917f, new(-0.6261f, 0.883f)),
                    new(0.1033f, new(-0.6699f, 0.6372f)), new(0.115f, new(-0.6163f, 0.3933f)),
                    new(0.1267f, new(-0.4734f, 0.1885f)), new(0.1383f, new(-0.263f, 0.0539f)),
                    new(0.15f, new(-0.0972f, 0.0085f)), new(0.21f, new(-0.0231f, 0.005f)), new(0.3f, new(0.05f, 0.065f))),
                Bend: Track.Held<int>(new(0f, 1), new(0.21f, -1))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.128f, 0.46f)), new(0.08f, new(-0.128f, 0.22f)), new(0.15f, new(-0.128f, 0f)),
                    new(0.21f, new(-0.112f, 0f)), new(0.3f, new(0.052f, 0f))),
                Bend: Track.Held<int>(new(0f, -1), new(0.21f, 1))),
        ],
        Markers = [new("view-back", 0f), new("view-profile", 0.21f)],
        Expression = Track.Held<string>(new(0f, ""), new(0.24f, "relaxed")),
    };

    public static SkeletonClip2D PlayerClimbOn { get; } = new("player-climb-on", 0.3f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.008f, 0.0096f)), new(0.09f, new(0f, 0.008f)), new(0.15f, new(0f, -0.024f)), new(0.22f, new(0f, -0.056f)),
                    new(0.3f, new(0f, -0.08f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(0f, 0f)), new(0.06f, new(0.0017f, 0f)), new(0.12f, new(0.0121f, -0.0001f)),
                    new(0.15f, new(0.013f, -0.0001f)), new(0.3f, new(0.013f, -0.0001f))),
                Turn: Track.Linear(
                    new(0f, 0f), new(0.03f, 0f), new(0.06f, -0.0027f), new(0.12f, -0.0187f), new(0.15f, -0.02f), new(0.3f, -0.02f)),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, 0f), new(0.06f, -0.0467f), new(0.12f, -0.3267f), new(0.15f, -0.35f), new(0.3f, -0.35f))),
            new(Head, Move: Track.Of<Vector2f>(new(0f, new(-0.0076f, -0.0001f)), new(0.15f, new(0f, 0f)), new(0.3f, new(0f, 0f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(0.068f, 0f)), new(0.06f, new(0.1435f, 0f)), new(0.09f, new(0.2135f, 0f)),
                    new(0.12f, new(0.2659f, 0f)), new(0.15f, new(0.2917f, 0f)), new(0.3f, new(0.2917f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, -0.033f), new(0.06f, -0.0355f), new(0.09f, -0.0071f), new(0.12f, 0.0473f),
                    new(0.15f, 0.1184f), new(0.3f, 0.1184f))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(0.038f, 0f)), new(0.06f, new(0.0777f, 0f)), new(0.09f, new(0.1122f, 0f)),
                    new(0.12f, new(0.1357f, 0f)), new(0.15f, new(0.144f, 0f)), new(0.3f, new(0.144f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, -0.0154f), new(0.06f, -0.0169f), new(0.09f, -0.0119f), new(0.12f, 0.0084f),
                    new(0.15f, 0.0539f), new(0.3f, 0.0539f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(-0.0744f, 0f)), new(0.06f, new(-0.1477f, 0f)), new(0.09f, new(-0.2074f, 0f)),
                    new(0.12f, new(-0.243f, 0f)), new(0.15f, new(-0.2483f, 0f)), new(0.3f, new(-0.2483f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, 0.0132f), new(0.06f, -0.0049f), new(0.09f, -0.0513f), new(0.12f, -0.118f),
                    new(0.15f, -0.1934f), new(0.3f, -0.1934f))),
            new(RightHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(-0.038f, 0f)), new(0.06f, new(-0.0777f, 0f)), new(0.09f, new(-0.1122f, 0f)),
                    new(0.12f, new(-0.1357f, 0f)), new(0.15f, new(-0.144f, 0f)), new(0.3f, new(-0.144f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, 0.0154f), new(0.06f, 0.0035f), new(0.09f, -0.0414f), new(0.12f, -0.1018f),
                    new(0.15f, -0.1539f), new(0.3f, -0.1539f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.02f, 0.05f)), new(0.09f, new(-0.0231f, 0.005f)), new(0.15f, new(0.0628f, 0.0117f)),
                    new(0.1617f, new(0.2267f, 0.0637f)), new(0.1733f, new(0.4315f, 0.2066f)),
                    new(0.185f, new(0.5661f, 0.417f)), new(0.1967f, new(0.6099f, 0.6628f)), new(0.2083f, new(0.5563f, 0.9067f)),
                    new(0.22f, new(0.4213f, 1.0891f)), new(0.3f, new(0.1289f, 1.137f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.052f, 0f)), new(0.09f, new(0.112f, 0f)), new(0.15f, new(0.128f, 0f)), new(0.22f, new(0.128f, 0.004f)),
                    new(0.3f, new(0.128f, 0.02f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.05f, 0.065f)), new(0.09f, new(-0.0231f, 0.005f)), new(0.15f, new(-0.0972f, 0.0085f)),
                    new(0.1617f, new(-0.263f, 0.0539f)), new(0.1733f, new(-0.4734f, 0.1885f)),
                    new(0.185f, new(-0.6163f, 0.3933f)), new(0.1967f, new(-0.6699f, 0.6372f)),
                    new(0.2083f, new(-0.6261f, 0.883f)), new(0.22f, new(-0.4975f, 1.0207f)), new(0.3f, new(-0.2209f, 0.6227f))),
                Bend: Track.Held<int>(new(0f, -1), new(0.09f, 1))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.052f, 0f)), new(0.09f, new(-0.112f, 0f)), new(0.15f, new(-0.128f, 0f)), new(0.22f, new(-0.128f, 0.22f)),
                    new(0.3f, new(-0.128f, 0.46f))),
                Bend: Track.Held<int>(new(0f, 1), new(0.09f, -1))),
        ],
        Markers = [new("view-profile", 0f), new("view-back", 0.09f)],
        Expression = Track.Held<string>(new(0f, "relaxed"), new(0.06f, "")),
    };

    public static SkeletonClip2D PlayerDash { get; } = new("player-dash", 0.28f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.016f, -0.08f)), new(0.04f, new(0.048f, -0.208f)), new(0.16f, new(0.056f, -0.2f)), new(0.28f, new(0.016f, -0.056f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.2229f, -0.0394f)), new(0.04f, new(0.4286f, -0.1613f)), new(0.16f, new(0.4087f, -0.1446f)),
                    new(0.28f, new(0.1291f, -0.013f))),
                Turn: new(SmoothTurn, new(0f, -0.35f), new(0.04f, -0.72f), new(0.16f, -0.68f), new(0.28f, -0.2f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.068f, -0.0061f)), new(0.04f, new(-0.1409f, -0.0271f)), new(0.16f, new(-0.1339f, -0.0244f)),
                    new(0.28f, new(-0.0379f, -0.0019f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.0934f, 0.0946f)), new(0.04f, new(-0.1799f, 0.0929f)), new(0.16f, new(-0.2427f, 0.1179f)),
                    new(0.28f, new(-0.0168f, 0.0813f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.152f, 0.028f)), new(0.04f, new(-0.4f, 0.076f)), new(0.16f, new(-0.384f, 0.06f)), new(0.28f, new(-0.112f, 0.012f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(-0.0276f, 0.1186f)), new(0.04f, new(-0.1611f, 0.1626f)), new(0.16f, new(-0.2149f, 0.1789f)),
                    new(0.28f, new(0.1009f, 0.1051f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.072f, 0.02f)), new(0.04f, new(0.192f, 0.028f)), new(0.16f, new(0.2f, 0.028f)), new(0.28f, new(0.072f, 0.004f)))),
        ],
        Markers = [new("dash-end", 0.16f)],
        Expression = Track.Constant("determined"),
    };

    public static SkeletonClip2D PlayerDeath { get; } = new("player-death", 1.6f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, 0.0096f)), new(0.07f, new(-0.064f, -0.048f), Straight), new(0.34f, new(-0.192f, -0.144f)),
                    new(0.68f, new(-0.368f, -0.448f), Straight), new(0.92f, new(-0.48f, -0.664f), Straight), new(1.06f, new(-0.48f, -0.608f)),
                    new(1.3f, new(-0.48f, -0.664f)), new(1.6f, new(-0.48f, -0.664f))),
                Turn: new(SmoothTurn,
                    new(0f, 0f), new(0.07f, 0f, StraightTurn), new(0.34f, 0.12f), new(0.68f, 0.85f, StraightTurn), new(0.92f, 1.45f, StraightTurn),
                    new(1.06f, 1.38f), new(1.3f, 1.5f), new(1.6f, 1.5f))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.07f, new(-0.2531f, -0.0513f), Straight),
                    new(0.34f, new(-0.1921f, -0.029f)), new(0.68f, new(-0.1418f, -0.0157f), Straight),
                    new(0.92f, new(-0.0519f, -0.0021f), Straight), new(1.06f, new(-0.0778f, -0.0047f)),
                    new(1.3f, new(-0.039f, -0.0012f)), new(1.6f, new(-0.039f, -0.0012f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.07f, 0.4f, StraightTurn), new(0.34f, 0.3f), new(0.68f, 0.22f, StraightTurn),
                    new(0.92f, 0.08f, StraightTurn), new(1.06f, 0.12f), new(1.3f, 0.06f), new(1.6f, 0.06f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.07f, new(-0.1195f, -0.0193f), Straight),
                    new(0.34f, new(-0.0755f, -0.0076f)), new(0.68f, new(-0.053f, -0.0037f), Straight),
                    new(0.92f, new(-0.0455f, -0.0027f), Straight), new(1.06f, new(-0.0755f, -0.0076f)),
                    new(1.3f, new(-0.0379f, -0.0019f)), new(1.6f, new(-0.0379f, -0.0019f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.07f, new(0.1869f, 0.3412f), Straight), new(0.34f, new(0.173f, 0.6141f)),
                    new(0.68f, new(0.2812f, 0.7063f), Straight), new(0.92f, new(0.0974f, 0.4309f), Straight),
                    new(1.06f, new(-0.025f, 0.4534f)), new(1.3f, new(-0.0429f, 0.4987f)), new(1.6f, new(-0.0429f, 0.4987f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.052f, 0f)), new(0.07f, new(-0.052f, 0f), Straight), new(0.34f, new(-0.256f, 0f)),
                    new(0.68f, new(-0.032f, 0.012f), Straight), new(0.92f, new(0.256f, 0.044f), Straight), new(1.06f, new(0.272f, 0.06f)),
                    new(1.3f, new(0.288f, 0.012f)), new(1.6f, new(0.288f, 0.012f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675f, 0.0677f)), new(0.07f, new(0.2655f, 0.3731f), Straight), new(0.34f, new(0.2664f, 0.6271f)),
                    new(0.68f, new(0.3356f, 0.5651f), Straight), new(0.92f, new(0.0474f, 0.186f), Straight),
                    new(1.06f, new(-0.047f, 0.2039f)), new(1.3f, new(-0.229f, 0.1386f)), new(1.6f, new(-0.229f, 0.1386f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.052f, 0f)), new(0.07f, new(0.052f, 0f), Straight), new(0.34f, new(-0.032f, 0.028f)),
                    new(0.68f, new(0.128f, 0.14f), Straight), new(0.92f, new(0.216f, 0.092f), Straight), new(1.06f, new(0.224f, 0.116f)),
                    new(1.3f, new(0.24f, 0.036f)), new(1.6f, new(0.24f, 0.036f)))),
        ],
        Markers = [new("impact", 0.92f)],
        Expression = Track.Held<string>(new(0f, "hurt"), new(0.92f, "knocked-out")),
    };

    public static SkeletonClip2D PlayerFall { get; } = new("player-fall", 0.6f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0f, 0.016f)), new(0.3f, new(0f, 0.024f)), new(0.6f, new(0f, 0.016f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(-0.039f, -0.0012f)), new(0.3f, new(-0.0519f, -0.0021f)), new(0.6f, new(-0.039f, -0.0012f))),
                Turn: new(SmoothTurn, new(0f, 0.06f), new(0.3f, 0.08f), new(0.6f, 0.06f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(0.0379f, -0.0019f)), new(0.3f, new(0.0493f, -0.0032f)), new(0.6f, new(0.0379f, -0.0019f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth, new(0f, new(-0.3394f, 1.1695f)), new(0.3f, new(-0.2859f, 1.2123f)), new(0.6f, new(-0.3394f, 1.1695f)))),
            new(LeftLeg, Target: new(Smooth, new(0f, new(-0.048f, 0.076f)), new(0.3f, new(-0.032f, 0.116f)), new(0.6f, new(-0.048f, 0.076f)))),
            new(RightArm,
                Target: new(Smooth, new(0f, new(0.3969f, 1.0852f)), new(0.3f, new(0.3688f, 1.1196f)), new(0.6f, new(0.3969f, 1.0852f)))),
            new(RightLeg, Target: new(Smooth, new(0f, new(0.072f, 0.188f)), new(0.3f, new(0.056f, 0.148f)), new(0.6f, new(0.072f, 0.188f)))),
        ],
        Expression = Track.Constant("focused"),
    };

    public static SkeletonClip2D PlayerGunAim { get; } = new("player-gun-aim", 2f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.016f, 0.0096f)), new(1f, new(-0.016f, 0.0032f)), new(2f, new(-0.016f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(-0.0097f, -0.0001f)), new(2f, new(0f, 0f))),
                Turn: new(SmoothTurn, new(0f, 0f), new(1f, 0.015f), new(2f, 0f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(1f, new(-0.0038f, 0f)), new(2f, new(-0.0076f, -0.0001f)))),
            new(RightShoulder, Turn: new(SmoothTurn, new(0f, 0f), new(1f, -0.005f), new(2f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(-0.01f, 0.05f)), new(1f, new(-0.0292f, 0.0399f)), new(2f, new(-0.01f, 0.05f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0.615f, 0.63f)), new(1f, new(0.6147f, 0.6136f)), new(2f, new(0.615f, 0.63f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(new(0f, new(-0.2094f, 0.1745f, 0f)), new(2f, new(-0.2094f, 0.1745f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 2f, new(-0.052f, 0f)), new(RightLeg, 0f, 2f, new(0.052f, 0f))],
        Expression = Track.Constant("focused"),
    };

    public static SkeletonClip2D PlayerGunCharge { get; } = new("player-gun-charge", 1f)
    {
        Joints =
        [
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.45f, new(0.0114f, -0.0002f)), new(1f, new(0.0228f, -0.0007f)))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.01f, 0.15f)), new(0.45f, new(0.32f, 0.53f)), new(1f, new(0.41f, 0.59f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0.615f, 0.63f)), new(1f, new(0.615f, 0.63f)))),
        ],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.7f, "determined")),
    };

    public static SkeletonClip2D PlayerGunShot { get; } = new("player-gun-shot", 0.2f)
    {
        Joints =
        [
            new(Chest,
                Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.03f, new(-0.039f, -0.0012f), Smooth), new(0.2f, new(0f, 0f))),
                Turn: Track.Linear(new(0f, 0f), new(0.03f, 0.06f, SmoothTurn), new(0.2f, 0f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0076f, -0.0001f)), new(0.03f, new(-0.019f, -0.0005f), Smooth),
                    new(0.2f, new(-0.0076f, -0.0001f)))),
            new(RightShoulder, Turn: Track.Linear(new(0f, 0f), new(0.03f, 0.32f, SmoothTurn), new(0.2f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.01f, 0.05f)), new(0.03f, new(-0.056f, 0.0505f), Smooth), new(0.2f, new(-0.01f, 0.05f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.615f, 0.63f)), new(0.03f, new(0.4715f, 0.4497f), Smooth), new(0.2f, new(0.615f, 0.63f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(
                new(0f, new(-0.2094f, 0.1745f, 0f)), new(0.03f, new(-0.4538f, 0.2443f, 0f)), new(0.2f, new(-0.2094f, 0.1745f, 0f)))),
        ],
        Markers = [new("fire", 0f)],
        Expression = Track.Held<string>(new(0f, "determined"), new(0.14f, "focused")),
    };

    public static SkeletonClip2D PlayerGunWallShot { get; } = new("player-gun-wall-shot", 0.3f)
    {
        Joints =
        [
            new(Hips, Move: Track.Of<Vector2f>(new(0f, new(-0.08f, -0.16f)), new(0.3f, new(-0.08f, -0.16f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0519f, -0.0021f)), new(0.03f, new(-0.0843f, -0.0055f), Smooth),
                    new(0.3f, new(-0.0519f, -0.0021f))),
                Turn: Track.Linear(new(0f, 0.08f), new(0.03f, 0.13f, SmoothTurn), new(0.3f, 0.08f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0076f, -0.0001f)), new(0.03f, new(-0.0076f, -0.0001f), Smooth),
                    new(0.3f, new(0.0076f, -0.0001f)))),
            new(RightShoulder, Turn: Track.Linear(new(0f, -0.08f), new(0.03f, 0.27f, SmoothTurn), new(0.3f, -0.08f))),
        ],
        Limbs =
        [
            new(LeftArm, Target: Track.Of<Vector2f>(new(0f, new(-0.0877f, 0.5952f)), new(0.3f, new(-0.0877f, 0.5952f)))),
            new(LeftLeg, Target: Track.Of<Vector2f>(new(0f, new(-0.192f, 0.284f)), new(0.3f, new(-0.192f, 0.284f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.615f, 0.63f)), new(0.03f, new(0.4528f, 0.4567f), Smooth), new(0.3f, new(0.615f, 0.63f)))),
            new(RightLeg, Target: Track.Of<Vector2f>(new(0f, new(-0.192f, 0.028f)), new(0.3f, new(-0.192f, 0.028f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(
                new(0f, new(-0.2094f, 0.1745f, 0f)), new(0.03f, new(-0.4538f, 0.2443f, 0f)), new(0.3f, new(-0.2094f, 0.1745f, 0f)))),
        ],
        Markers = [new("fire", 0f)],
        Expression = Track.Constant("determined"),
    };

    public static SkeletonClip2D PlayerHealGather { get; } = new("player-heal-gather", 1f)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0f, 0.0096f)), new(0.25f, new(0f, -0.0224f)), new(1f, new(0f, -0.0464f)))),
            new(Chest,
                Move: new(Smooth, new(0f, new(0f, 0f)), new(0.25f, new(0.0649f, -0.0032f)), new(1f, new(0.0907f, -0.0064f))),
                Turn: new(SmoothTurn, new(0f, 0f), new(0.25f, -0.1f), new(1f, -0.14f))),
            new(Head, Move: new(Smooth, new(0f, new(0f, 0f)), new(0.25f, new(0.0605f, -0.0049f)), new(1f, new(0.0755f, -0.0076f)))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.15f, 0.3f)), new(0.25f, new(0.2248f, 0.5148f)), new(1f, new(0.2173f, 0.5435f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0.09f, 0.3f)), new(0.25f, new(0.189f, 0.471f)), new(1f, new(0.1833f, 0.4983f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1f, new(-0.052f, 0f)), new(RightLeg, 0f, 1f, new(0.052f, 0f))],
        Expression = Track.Constant("focused"),
    };

    public static SkeletonClip2D PlayerHit { get; } = new("player-hit", 0.35f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, 0.0096f)), new(0.05f, new(-0.056f, -0.048f), Straight), new(0.18f, new(-0.04f, -0.048f)),
                    new(0.35f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.05f, new(-0.229f, -0.0417f), Straight),
                    new(0.18f, new(-0.0907f, -0.0064f)), new(0.35f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn, new(0f, -0.03f), new(0.05f, 0.36f, StraightTurn), new(0.18f, 0.14f), new(0.35f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.05f, new(-0.1123f, -0.017f), Straight),
                    new(0.18f, new(-0.0379f, -0.0019f)), new(0.35f, new(-0.0076f, -0.0001f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.05f, new(0.1733f, 0.3384f), Straight), new(0.18f, new(0.0896f, 0.209f)),
                    new(0.35f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675f, 0.0677f)), new(0.05f, new(0.2506f, 0.3734f), Straight), new(0.18f, new(0.1348f, 0.243f)),
                    new(0.35f, new(0.0675f, 0.0677f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.35f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.35f, new(0.052f, 0f))],
        Expression = Track.Held<string>(new(0f, "hurt"), new(0.24f, "strained")),
    };

    public static SkeletonClip2D PlayerIdle { get; } = new("player-idle", 2.4f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, 0.0096f), SmoothStraightX), new(1.2f, new(-0.008f, 0.0032f)), new(2.4f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(1.2f, new(0.0032f, 0f)), new(2.4f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn, new(0f, -0.03f), new(1.2f, -0.005f), new(2.4f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(1.2f, new(0.0038f, 0f)),
                    new(2.4f, new(-0.0076f, -0.0001f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.35f, new(0.0349f, 0.0514f)), new(1.55f, new(0.0112f, 0.0354f)),
                    new(2.4f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675f, 0.0677f)), new(0.35f, new(0.0645f, 0.0672f)), new(1.55f, new(0.0411f, 0.0507f)),
                    new(2.4f, new(0.0675f, 0.0677f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 2.4f, new(-0.052f, 0f)), new(RightLeg, 0f, 2.4f, new(0.052f, 0f))],
    };

    public static SkeletonClip2D PlayerJump { get; } = new("player-jump", 0.5f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.016f, -0.064f)), new(0.08f, new(0f, 0.016f), Straight), new(0.3f, new(0f, 0.048f)), new(0.5f, new(0f, 0.04f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.1418f, -0.0157f)), new(0.08f, new(0.026f, -0.0005f), Straight),
                    new(0.3f, new(0.0649f, -0.0032f)), new(0.5f, new(0.0519f, -0.0021f))),
                Turn: new(SmoothTurn, new(0f, -0.22f), new(0.08f, -0.04f, StraightTurn), new(0.3f, -0.1f), new(0.5f, -0.08f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0455f, -0.0027f)), new(0.08f, new(-0.0076f, -0.0001f), Straight),
                    new(0.3f, new(-0.0228f, -0.0007f)), new(0.5f, new(-0.019f, -0.0005f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.1985f, 0.1205f)), new(0.08f, new(0.0819f, 0.8546f), Straight), new(0.3f, new(0.1691f, 0.469f)),
                    new(0.5f, new(0.1303f, 0.402f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.112f, 0.028f)), new(0.08f, new(-0.064f, -0.004f), Straight), new(0.3f, new(-0.096f, 0.348f)),
                    new(0.5f, new(-0.072f, 0.284f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(-0.1356f, 0.114f)), new(0.08f, new(0.161f, 0.8778f), Straight), new(0.3f, new(0.2248f, 0.5148f)),
                    new(0.5f, new(0.1869f, 0.4467f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0f, 0.004f)), new(0.08f, new(-0.032f, 0.06f), Straight), new(0.3f, new(0.048f, 0.396f)),
                    new(0.5f, new(0.032f, 0.332f)))),
        ],
        Markers = [new("launch", 0f), new("apex", 0.3f)],
        Expression = Track.Held<string>(new(0f, "determined"), new(0.3f, "focused")),
    };

    public static SkeletonClip2D PlayerLand { get; } = new("player-land", 0.36f)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.016f, -0.144f)), new(0.09f, new(-0.016f, -0.192f)), new(0.36f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.1796f, -0.0253f)), new(0.09f, new(0.2168f, -0.0372f)), new(0.36f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn, new(0f, -0.28f), new(0.09f, -0.34f), new(0.36f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0605f, -0.0049f)), new(0.09f, new(-0.0755f, -0.0076f)),
                    new(0.36f, new(-0.0076f, -0.0001f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.3222f, 0.2935f)), new(0.09f, new(0.3485f, 0.3596f)), new(0.36f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.3689f, 0.3485f)), new(0.09f, new(0.3917f, 0.4173f)), new(0.36f, new(0.0675f, 0.0677f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.36f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.36f, new(0.052f, 0f))],
        Markers = [new("impact", 0f)],
        Expression = Track.Held<string>(new(0f, "strained"), new(0.2f, "relaxed")),
    };

    public static SkeletonClip2D PlayerSwordBackhand { get; } = new("player-sword-backhand", 0.25f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.048f, -0.128f)), new(0.0333f, new(0.016f, -0.064f)), new(0.1f, new(0.016f, -0.064f), Straight),
                    new(0.1167f, new(0f, -0.064f), Straight), new(0.1333f, new(0.048f, -0.104f), Straight),
                    new(0.1667f, new(0.04f, -0.128f)), new(0.2333f, new(0.032f, -0.112f)), new(0.25f, new(0.032f, -0.112f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.2351f, -0.044f)), new(0.0333f, new(0.0325f, -0.0008f)),
                    new(0.1f, new(0.0325f, -0.0008f), Straight), new(0.1167f, new(-0.013f, -0.0001f), Straight),
                    new(0.1333f, new(0.1921f, -0.029f), Straight), new(0.1667f, new(0.2229f, -0.0394f)),
                    new(0.2333f, new(0.2106f, -0.0351f)), new(0.25f, new(0.2106f, -0.0351f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.37f), new(0.0333f, -0.05f), new(0.1f, -0.05f, StraightTurn), new(0.1167f, 0.02f, StraightTurn),
                    new(0.1333f, -0.3f, StraightTurn), new(0.1667f, -0.35f), new(0.2333f, -0.33f), new(0.25f, -0.33f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.068f, -0.0061f)), new(0.0333f, new(-0.0379f, -0.0019f)),
                    new(0.1f, new(-0.0379f, -0.0019f), Straight), new(0.1167f, new(-0.0304f, -0.0012f), Straight),
                    new(0.1333f, new(-0.068f, -0.0061f), Straight), new(0.1667f, new(-0.068f, -0.0061f)),
                    new(0.2333f, new(-0.0605f, -0.0049f)), new(0.25f, new(-0.0605f, -0.0049f)))),
            new(LeftShoulder,
                Move: new(Smooth,
                    new(0f, new(-0.07f, 0f)), new(0.0333f, new(-0.0423f, 0f)), new(0.1f, new(-0.0423f, 0f), Straight),
                    new(0.1167f, new(-0.0621f, 0f), Straight), new(0.1333f, new(0f, 0f), Straight),
                    new(0.1667f, new(0.0867f, 0f)), new(0.2333f, new(0.1024f, 0f)), new(0.25f, new(0.1024f, 0f))),
                Layer: new(SmoothTurn,
                    new(0f, 0.1367f), new(0.0333f, 0.0468f), new(0.1f, 0.0468f, StraightTurn),
                    new(0.1167f, 0.09f, StraightTurn), new(0.1333f, 0f, StraightTurn), new(0.1667f, -0.0366f),
                    new(0.2333f, -0.0381f), new(0.25f, -0.0381f))),
            new(RightShoulder,
                Move: new(Smooth,
                    new(0f, new(0.1046f, 0f)), new(0.0333f, new(0.0535f, 0f)), new(0.1f, new(0.0535f, 0f), Straight),
                    new(0.1167f, new(0.0842f, 0f), Straight), new(0.1333f, new(0f, 0f), Straight),
                    new(0.1667f, new(-0.0933f, 0f)), new(0.2333f, new(-0.1089f, 0f)), new(0.25f, new(-0.1089f, 0f))),
                Turn: new(SmoothTurn,
                    new(0f, -5.7523f), new(0.0333f, -3.8612f), new(0.1f, -3.8612f, StraightTurn),
                    new(0.1167f, -3.9312f, StraightTurn), new(0.1333f, -5.5638f, StraightTurn), new(0.1667f, -5.7477f),
                    new(0.2333f, -5.7065f), new(0.25f, -5.7065f)),
                Layer: new(SmoothTurn,
                    new(0f, -0.1125f), new(0.0333f, -0.0335f), new(0.1f, -0.0335f, StraightTurn),
                    new(0.1167f, -0.0697f, StraightTurn), new(0.1333f, 0f, StraightTurn), new(0.1667f, 0.0116f),
                    new(0.2333f, 0.0088f), new(0.25f, 0.0088f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.091f, 0.1437f)), new(0.0333f, new(-0.1868f, 0.1816f)),
                    new(0.1f, new(-0.1868f, 0.1816f), Straight), new(0.1167f, new(-0.2392f, 0.1941f), Straight),
                    new(0.1333f, new(0.1537f, 0.2253f), Straight), new(0.1667f, new(0.2405f, 0.259f)),
                    new(0.2333f, new(0.2137f, 0.2472f)), new(0.25f, new(0.2137f, 0.2472f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.2201f, 0.0471f)), new(0.0333f, new(0.1096f, 0.0387f)),
                    new(0.1f, new(0.1654f, 0.0665f), Straight), new(0.1167f, new(0.3629f, 0.508f), Straight),
                    new(0.1333f, new(0.172f, 0.0274f), Straight), new(0.1667f, new(0.1801f, 0.0339f)),
                    new(0.2333f, new(0.189f, 0.0353f)), new(0.25f, new(0.189f, 0.0353f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.3491f, 0.518f, 0f)), new(0.0333f, new(0.1745f, 0.3931f, 0f)),
                new(0.1f, new(0.1745f, 0.3931f, 0f)), new(0.1167f, new(0.6981f, 0.3931f, 0f)),
                new(0.1333f, new(1.309f, 0.4026f, 0f)), new(0.1667f, new(0.9599f, -0.3435f, 0f)),
                new(0.2333f, new(0.3491f, -0.1355f, 0f)), new(0.25f, new(0.3491f, -0.1355f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.25f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.25f, new(0.08f, 0f))],
        Markers = [
            new("sword-draw", 0f), new("swoosh", 0.1167f), new("strike", 0.1333f), new("swoosh-end", 0.15f),
            new("recover", 0.2167f)],
    };

    public static SkeletonClip2D PlayerSwordDownAttack { get; } = new("player-sword-down-attack", 0.25f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0f, 0.032f), Straight), new(0.04f, new(0f, 0.048f)), new(0.18f, new(0f, 0.048f)), new(0.25f, new(0f, 0.04f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0f, 0f), Straight), new(0.04f, new(0.1291f, -0.013f)), new(0.18f, new(0.1418f, -0.0157f)),
                    new(0.25f, new(0.0778f, -0.0047f))),
                Turn: new(SmoothTurn, new(0f, 0f, StraightTurn), new(0.04f, -0.2f), new(0.18f, -0.22f), new(0.25f, -0.12f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(0f, 0f), Straight), new(0.04f, new(0.0605f, -0.0049f)), new(0.18f, new(0.068f, -0.0061f)),
                    new(0.25f, new(0.0304f, -0.0012f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn, new(0f, -1.6908f, StraightTurn), new(0.04f, -1.3708f), new(0.18f, -1.3508f), new(0.25f, -1.13f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.07f, 0.25f), Straight), new(0.04f, new(0.199f, 0.2058f)), new(0.18f, new(0.2008f, 0.1886f)),
                    new(0.25f, new(0.0894f, 0.1608f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.064f, 0.268f), Straight), new(0.04f, new(-0.096f, 0.396f)), new(0.18f, new(-0.096f, 0.412f)),
                    new(0.25f, new(-0.072f, 0.316f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(-0.0098f, 0.4829f), Straight), new(0.04f, new(0.4951f, 0.6298f)), new(0.18f, new(0.5113f, 0.6189f)),
                    new(0.25f, new(0.434f, 0.5632f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.032f, 0.316f), Straight), new(0.04f, new(0.016f, 0.428f)), new(0.18f, new(0.016f, 0.444f)),
                    new(0.25f, new(0.032f, 0.348f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(1.1345f, 0.2618f, 0f)), new(0.04f, new(0.2618f, 0f, 0f)), new(0.18f, new(0.2618f, 0f, 0f)),
                new(0.25f, new(0.5236f, 0f, 0f)))),
        ],
        Markers = [new("sword-draw", 0f), new("strike", 0.02f), new("recover", 0.083f)],
        Expression = Track.Constant("determined"),
    };

    public static SkeletonClip2D PlayerSwordForehand { get; } = new("player-sword-forehand", 0.25f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.032f, -0.112f), Smooth), new(0.0333f, new(-0.016f, -0.048f), Smooth), new(0.1f, new(-0.016f, -0.048f)),
                    new(0.1167f, new(0.008f, -0.064f)), new(0.1333f, new(0.048f, -0.088f)), new(0.1667f, new(0.048f, -0.136f), Smooth),
                    new(0.2333f, new(0.048f, -0.128f), Smooth), new(0.25f, new(0.048f, -0.128f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.2106f, -0.0351f), Smooth), new(0.0333f, new(-0.0325f, -0.0008f), Smooth), new(0.1f, new(-0.0325f, -0.0008f)),
                    new(0.1167f, new(0.0649f, -0.0032f)), new(0.1333f, new(0.2045f, -0.033f)), new(0.1667f, new(0.2531f, -0.0513f), Smooth),
                    new(0.2333f, new(0.2351f, -0.044f), Smooth), new(0.25f, new(0.2351f, -0.044f))),
                Turn: Track.Linear(
                    new(0f, -0.33f, SmoothTurn), new(0.0333f, 0.05f, SmoothTurn), new(0.1f, 0.05f), new(0.1167f, -0.1f), new(0.1333f, -0.32f),
                    new(0.1667f, -0.4f, SmoothTurn), new(0.2333f, -0.37f, SmoothTurn), new(0.25f, -0.37f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0605f, -0.0049f), Smooth), new(0.0333f, new(-0.0228f, -0.0007f), Smooth), new(0.1f, new(-0.0228f, -0.0007f)),
                    new(0.1167f, new(-0.0379f, -0.0019f)), new(0.1333f, new(-0.068f, -0.0061f)), new(0.1667f, new(-0.0755f, -0.0076f), Smooth),
                    new(0.2333f, new(-0.068f, -0.0061f), Smooth), new(0.25f, new(-0.068f, -0.0061f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.1024f, 0f), Smooth), new(0.0333f, new(0.1183f, 0f), Smooth), new(0.1f, new(0.1183f, 0f)),
                    new(0.1167f, new(0.0558f, 0f)), new(0.1333f, new(-0.066f, 0f)), new(0.1667f, new(-0.0698f, 0f), Smooth),
                    new(0.2333f, new(-0.07f, 0f), Smooth), new(0.25f, new(-0.07f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.0381f, SmoothTurn), new(0.0333f, -0.0382f, SmoothTurn), new(0.1f, -0.0382f), new(0.1167f, -0.0295f),
                    new(0.1333f, 0.1053f), new(0.1667f, 0.1526f, SmoothTurn), new(0.2333f, 0.1367f, SmoothTurn), new(0.25f, 0.1367f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.1089f, 0f), Smooth), new(0.0333f, new(-0.1242f, 0f), Smooth), new(0.1f, new(-0.1242f, 0f)),
                    new(0.1167f, new(-0.0617f, 0f)), new(0.1333f, new(0.0922f, 0f)), new(0.1667f, new(0.1088f, 0f), Smooth),
                    new(0.2333f, new(0.1046f, 0f), Smooth), new(0.25f, new(0.1046f, 0f))),
                Turn: Track.Linear(
                    new(0f, -5.7065f, SmoothTurn), new(0.0333f, -3.9943f, SmoothTurn), new(0.1f, -3.9943f), new(0.1167f, -3.7921f),
                    new(0.1333f, -5.7407f), new(0.1667f, -5.8149f, SmoothTurn), new(0.2333f, -5.7523f, SmoothTurn), new(0.25f, -5.7523f)),
                Layer: Track.Linear(
                    new(0f, 0.0088f, SmoothTurn), new(0.0333f, 0.0045f, SmoothTurn), new(0.1f, 0.0045f), new(0.1167f, 0.0132f),
                    new(0.1333f, -0.0833f), new(0.1667f, -0.1278f, SmoothTurn), new(0.2333f, -0.1125f, SmoothTurn), new(0.25f, -0.1125f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.2137f, 0.2472f), Smooth), new(0.0333f, new(0.1278f, 0.2016f), Smooth), new(0.1f, new(0.1278f, 0.2016f)),
                    new(0.1167f, new(0.0657f, 0.1973f)), new(0.1333f, new(-0.0941f, 0.1441f)), new(0.1667f, new(-0.0889f, 0.1474f), Smooth),
                    new(0.2333f, new(-0.091f, 0.1437f), Smooth), new(0.25f, new(-0.091f, 0.1437f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.189f, 0.0353f), Smooth), new(0.0333f, new(0.5464f, 0.7757f), Smooth), new(0.1f, new(0.5464f, 0.7757f)),
                    new(0.1167f, new(-0.0739f, 0.5311f)), new(0.1333f, new(0.2697f, 0.0682f)), new(0.1667f, new(0.2098f, 0.044f), Smooth),
                    new(0.2333f, new(0.2201f, 0.0471f), Smooth), new(0.25f, new(0.2201f, 0.0471f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.3491f, -0.1355f, 0f)), new(0.0333f, new(0.1745f, -0.184f, 0f)),
                new(0.0833f, new(0.5236f, -0.184f, 0f)), new(0.1f, new(0.5236f, -0.184f, 0f)),
                new(0.1167f, new(1.2217f, -0.9025f, 0f)), new(0.1333f, new(1.309f, -0.3411f, 0f)),
                new(0.1667f, new(0.9599f, 0.697f, 0f)), new(0.2333f, new(0.3491f, 0.518f, 0f)),
                new(0.25f, new(0.3491f, 0.518f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.25f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.25f, new(0.08f, 0f))],
        Markers = [
            new("sword-draw", 0f), new("swoosh", 0.1f), new("strike", 0.1333f), new("swoosh-end", 0.15f),
            new("recover", 0.2167f)],
    };

    public static SkeletonClip2D PlayerSwordPutAway { get; } = new("player-sword-put-away", 0.4833f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.048f, -0.128f)), new(0.1833f, new(0.048f, -0.128f)), new(0.2633f, new(0.008f, -0.04f), Straight),
                    new(0.3333f, new(-0.008f, 0.0096f), Straight), new(0.4833f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.2351f, -0.044f)), new(0.1833f, new(0.2351f, -0.044f)),
                    new(0.2633f, new(0.013f, -0.0001f), Straight), new(0.3333f, new(-0.026f, -0.0005f), Straight),
                    new(0.4033f, new(-0.013f, -0.0001f)), new(0.4833f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.37f), new(0.1833f, -0.37f), new(0.2633f, -0.02f, StraightTurn), new(0.3333f, 0.04f, StraightTurn),
                    new(0.4033f, 0.02f), new(0.4833f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.068f, -0.0061f)), new(0.1833f, new(-0.068f, -0.0061f)), new(0.2633f, new(-0.0228f, -0.0007f), Straight),
                    new(0.4033f, new(-0.0076f, -0.0001f)), new(0.4833f, new(-0.0076f, -0.0001f)))),
            new(LeftShoulder,
                Move: new(Smooth,
                    new(0f, new(-0.07f, 0f)), new(0.1833f, new(-0.07f, 0f)), new(0.2633f, new(0.0266f, 0f), Straight),
                    new(0.3333f, new(0.0867f, 0f), Straight), new(0.4033f, new(0.0558f, 0f)), new(0.4833f, new(0f, 0f))),
                Layer: new(SmoothTurn,
                    new(0f, 0.1367f), new(0.1833f, 0.1367f), new(0.2633f, -0.0172f, StraightTurn),
                    new(0.3333f, -0.0366f, StraightTurn), new(0.4033f, -0.0295f), new(0.4833f, 0f))),
            new(RightShoulder,
                Move: new(Smooth,
                    new(0f, new(0.1046f, 0f)), new(0.1833f, new(0.1046f, 0f)), new(0.2633f, new(-0.0303f, 0f), Straight),
                    new(0.3333f, new(-0.0933f, 0f), Straight), new(0.4033f, new(-0.0617f, 0f)), new(0.4833f, new(0f, 0f))),
                Turn: new(SmoothTurn,
                    new(0f, -5.7523f), new(0.1833f, -5.7523f), new(0.2633f, -4.6632f, StraightTurn),
                    new(0.3333f, -2.8232f, StraightTurn), new(0.4033f, -1.6908f), new(0.4833f, -1.6408f)),
                Layer: new(SmoothTurn,
                    new(0f, -0.1125f), new(0.1833f, -0.1125f), new(0.2633f, 0.0093f, StraightTurn),
                    new(0.3333f, 0.0116f, StraightTurn), new(0.4033f, 0.0132f), new(0.4833f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.091f, 0.1437f)), new(0.1833f, new(-0.091f, 0.1437f)),
                    new(0.2633f, new(-0.0184f, 0.0701f), Straight), new(0.3333f, new(-0.014f, 0.0489f), Straight),
                    new(0.4033f, new(0.008f, 0.0491f)), new(0.4833f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.2201f, 0.0471f)), new(0.1833f, new(0.2201f, 0.0471f)),
                    new(0.2633f, new(0.4483f, 0.5555f), Straight), new(0.3333f, new(-0.1539f, 0.1417f), Straight),
                    new(0.4033f, new(-0.0172f, 0.5441f)), new(0.4833f, new(0.5441f, 0.788f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.3491f, 0.518f, 0f)), new(0.1833f, new(0.3491f, 0.518f, 0f)), new(0.2633f, new(0f, 0f, 0f)),
                new(0.4833f, new(0f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.4833f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.4833f, new(0.08f, 0f))],
        Markers = [new("sword-sheathe", 0.4033f)],
    };

    public static SkeletonClip2D PlayerSwordPutAwayBackhand { get; } = new("player-sword-put-away-backhand", 0.4833f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.032f, -0.112f)), new(0.1833f, new(0.032f, -0.112f)), new(0.2633f, new(0.008f, -0.04f), Straight),
                    new(0.3333f, new(-0.008f, 0.0096f), Straight), new(0.4833f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.2106f, -0.0351f)), new(0.1833f, new(0.2106f, -0.0351f)),
                    new(0.2633f, new(0.013f, -0.0001f), Straight), new(0.3333f, new(-0.026f, -0.0005f), Straight),
                    new(0.4033f, new(-0.013f, -0.0001f)), new(0.4833f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.33f), new(0.1833f, -0.33f), new(0.2633f, -0.02f, StraightTurn), new(0.3333f, 0.04f, StraightTurn),
                    new(0.4033f, 0.02f), new(0.4833f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0605f, -0.0049f)), new(0.1833f, new(-0.0605f, -0.0049f)), new(0.2633f, new(-0.0228f, -0.0007f), Straight),
                    new(0.4033f, new(-0.0076f, -0.0001f)), new(0.4833f, new(-0.0076f, -0.0001f)))),
            new(LeftShoulder,
                Move: new(Smooth,
                    new(0f, new(0.1024f, 0f)), new(0.1833f, new(0.1024f, 0f)), new(0.2633f, new(0.0266f, 0f), Straight),
                    new(0.3333f, new(0.0867f, 0f), Straight), new(0.4033f, new(0.0558f, 0f)), new(0.4833f, new(0f, 0f))),
                Layer: new(SmoothTurn,
                    new(0f, -0.0381f), new(0.1833f, -0.0381f), new(0.2633f, -0.0172f, StraightTurn),
                    new(0.3333f, -0.0366f, StraightTurn), new(0.4033f, -0.0295f), new(0.4833f, 0f))),
            new(RightShoulder,
                Move: new(Smooth,
                    new(0f, new(-0.1089f, 0f)), new(0.1833f, new(-0.1089f, 0f)), new(0.2633f, new(-0.0303f, 0f), Straight),
                    new(0.3333f, new(-0.0933f, 0f), Straight), new(0.4033f, new(-0.0617f, 0f)), new(0.4833f, new(0f, 0f))),
                Turn: new(SmoothTurn,
                    new(0f, -5.7065f), new(0.1833f, -5.7065f), new(0.2633f, -4.6632f, StraightTurn),
                    new(0.3333f, -2.8232f, StraightTurn), new(0.4033f, -1.6908f), new(0.4833f, -1.6408f)),
                Layer: new(SmoothTurn,
                    new(0f, 0.0088f), new(0.1833f, 0.0088f), new(0.2633f, 0.0093f, StraightTurn),
                    new(0.3333f, 0.0116f, StraightTurn), new(0.4033f, 0.0132f), new(0.4833f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.2137f, 0.2472f)), new(0.1833f, new(0.2137f, 0.2472f)),
                    new(0.2633f, new(-0.0184f, 0.0701f), Straight), new(0.3333f, new(-0.014f, 0.0489f), Straight),
                    new(0.4033f, new(0.008f, 0.0491f)), new(0.4833f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.189f, 0.0353f)), new(0.1833f, new(0.189f, 0.0353f)),
                    new(0.2633f, new(0.4483f, 0.5555f), Straight), new(0.3333f, new(-0.1539f, 0.1417f), Straight),
                    new(0.4033f, new(-0.0172f, 0.5441f)), new(0.4833f, new(0.5441f, 0.788f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.3491f, -0.1355f, 0f)), new(0.1833f, new(0.3491f, -0.1355f, 0f)), new(0.2633f, new(0f, 0f, 0f)),
                new(0.4833f, new(0f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.4833f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.4833f, new(0.08f, 0f))],
        Markers = [new("sword-sheathe", 0.4033f)],
    };

    public static SkeletonClip2D PlayerSwordSheathe { get; } = new("player-sword-sheathe", 0.42f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.024f, -0.056f), Smooth), new(0.12f, new(0.008f, -0.04f)), new(0.22f, new(-0.008f, 0.0096f)),
                    new(0.42f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0778f, -0.0047f), Smooth), new(0.12f, new(0.013f, -0.0001f)),
                    new(0.22f, new(-0.026f, -0.0005f)), new(0.3f, new(-0.013f, -0.0001f), Smooth),
                    new(0.42f, new(0.0195f, -0.0003f))),
                Turn: Track.Linear(
                    new(0f, -0.12f, SmoothTurn), new(0.12f, -0.02f), new(0.22f, 0.04f), new(0.3f, 0.02f, SmoothTurn), new(0.42f, -0.03f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0304f, -0.0012f), Smooth), new(0.12f, new(-0.0228f, -0.0007f)),
                    new(0.22f, new(-0.0152f, -0.0003f)), new(0.3f, new(-0.0076f, -0.0001f), Smooth),
                    new(0.42f, new(-0.0076f, -0.0001f)))),
            new(RightShoulder,
                Turn: Track.Linear(
                    new(0f, -0.1f, SmoothTurn), new(0.12f, 1.62f), new(0.22f, 3.46f), new(0.3f, 4.5924f, SmoothTurn), new(0.42f, 4.6424f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.0448f, 0.1043f), Smooth), new(0.12f, new(-0.0184f, 0.0701f)), new(0.22f, new(-0.014f, 0.0489f)),
                    new(0.3f, new(0.008f, 0.0491f), Smooth), new(0.42f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.2025f, 0.4072f), Smooth), new(0.12f, new(0.4498f, 0.5857f)), new(0.22f, new(-0.0653f, 0.1125f)),
                    new(0.3f, new(-0.0098f, 0.4829f), Smooth), new(0.42f, new(0.5441f, 0.788f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.3491f, 0f, 0f)), new(0.12f, new(1.1345f, -0.3142f, 0f)), new(0.22f, new(0.4363f, -0.1745f, 0f)),
                new(0.3f, new(0f, 0f, 0f)), new(0.42f, new(0f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.42f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.42f, new(0.052f, 0f))],
        Markers = [new("sword-sheathe", 0.3f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.3f, "relaxed")),
    };

    public static SkeletonClip2D PlayerSwordSideCut { get; } = new("player-sword-side-cut", 0.1667f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.024f, -0.032f)), new(0.0167f, new(-0.024f, -0.04f)), new(0.0333f, new(-0.016f, -0.048f)),
                    new(0.05f, new(0.048f, -0.088f)), new(0.0833f, new(0.048f, -0.136f), Smooth),
                    new(0.15f, new(0.048f, -0.128f), Smooth), new(0.1667f, new(0.048f, -0.128f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0649f, -0.0032f)), new(0.0167f, new(-0.0195f, -0.0003f)),
                    new(0.0333f, new(-0.0325f, -0.0008f)), new(0.05f, new(0.2045f, -0.033f)),
                    new(0.0833f, new(0.2531f, -0.0513f), Smooth), new(0.15f, new(0.2351f, -0.044f), Smooth),
                    new(0.1667f, new(0.2351f, -0.044f))),
                Turn: Track.Linear(
                    new(0f, 0.1f), new(0.0167f, 0.03f), new(0.0333f, 0.05f), new(0.05f, -0.32f), new(0.0833f, -0.4f, SmoothTurn),
                    new(0.15f, -0.37f, SmoothTurn), new(0.1667f, -0.37f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0076f, -0.0001f)), new(0.0167f, new(-0.0076f, -0.0001f)),
                    new(0.0333f, new(-0.0228f, -0.0007f)), new(0.05f, new(-0.068f, -0.0061f)),
                    new(0.0833f, new(-0.0755f, -0.0076f), Smooth), new(0.15f, new(-0.068f, -0.0061f), Smooth),
                    new(0.1667f, new(-0.068f, -0.0061f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0867f, 0f)), new(0.0167f, new(0.1183f, 0f)), new(0.0333f, new(0.1183f, 0f)),
                    new(0.05f, new(-0.066f, 0f)), new(0.0833f, new(-0.0698f, 0f), Smooth),
                    new(0.15f, new(-0.07f, 0f), Smooth), new(0.1667f, new(-0.07f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.0366f), new(0.0167f, -0.0382f), new(0.0333f, -0.0382f), new(0.05f, 0.1053f),
                    new(0.0833f, 0.1526f, SmoothTurn), new(0.15f, 0.1367f, SmoothTurn), new(0.1667f, 0.1367f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0933f, 0f)), new(0.0167f, new(-0.1242f, 0f)), new(0.0333f, new(-0.1242f, 0f)),
                    new(0.05f, new(0.0922f, 0f)), new(0.0833f, new(0.1088f, 0f), Smooth),
                    new(0.15f, new(0.1046f, 0f), Smooth), new(0.1667f, new(0.1046f, 0f))),
                Turn: Track.Linear(
                    new(0f, -1.6908f), new(0.0167f, -1.7708f), new(0.0333f, -3.9943f), new(0.05f, -5.7407f),
                    new(0.0833f, -5.8149f, SmoothTurn), new(0.15f, -5.7523f, SmoothTurn), new(0.1667f, -5.7523f)),
                Layer: Track.Linear(
                    new(0f, 0.0116f), new(0.0167f, 0.0045f), new(0.0333f, 0.0045f), new(0.05f, -0.0833f),
                    new(0.0833f, -0.1278f, SmoothTurn), new(0.15f, -0.1125f, SmoothTurn), new(0.1667f, -0.1125f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.0375f, 0.1206f)), new(0.0167f, new(0.1161f, 0.1854f)), new(0.0333f, new(0.1278f, 0.2016f)),
                    new(0.05f, new(-0.0941f, 0.1441f)), new(0.0833f, new(-0.0889f, 0.1474f), Smooth),
                    new(0.15f, new(-0.091f, 0.1437f), Smooth), new(0.1667f, new(-0.091f, 0.1437f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.021f, 0.5755f)), new(0.0167f, new(-0.5105f, 0.2458f)), new(0.0333f, new(0.5464f, 0.7757f)),
                    new(0.05f, new(0.2697f, 0.0682f)), new(0.0833f, new(0.2098f, 0.044f), Smooth),
                    new(0.15f, new(0.2201f, 0.0471f), Smooth), new(0.1667f, new(0.2201f, 0.0471f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0f, 0f, 0f)), new(0.0167f, new(0f, 0f, 0f)), new(0.0333f, new(0.5236f, -0.184f, 0f)),
                new(0.05f, new(1.309f, -0.3411f, 0f)), new(0.0833f, new(0.9599f, 0.697f, 0f)),
                new(0.15f, new(0.3491f, 0.518f, 0f)), new(0.1667f, new(0.3491f, 0.518f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.1667f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.1667f, new(0.08f, 0f))],
        Markers = [
            new("sword-draw", 0.0167f), new("swoosh", 0.0333f), new("strike", 0.05f), new("swoosh-end", 0.0667f),
            new("recover", 0.1333f)],
    };

    public static SkeletonClip2D PlayerWallGrip { get; } = new("player-wall-grip", 1.2f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.064f, -0.08f)), new(0.6f, new(-0.064f, -0.092f)), new(1.2f, new(-0.064f, -0.08f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.3116f, -0.0796f)), new(0.6f, new(0.3002f, -0.0735f)), new(1.2f, new(0.3116f, -0.0796f))),
                Turn: new(SmoothTurn, new(0f, -0.5f), new(0.6f, -0.48f), new(1.2f, -0.5f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.1123f, -0.017f)), new(0.6f, new(-0.105f, -0.0148f)), new(1.2f, new(-0.1123f, -0.017f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth, new(0f, new(-1.2734f, 1.1629f)), new(0.6f, new(-1.2492f, 1.1854f)), new(1.2f, new(-1.2734f, 1.1629f)))),
            new(LeftLeg, Target: new(Smooth, new(0f, new(-0.272f, 0.028f)), new(0.6f, new(-0.272f, 0.02f)), new(1.2f, new(-0.272f, 0.028f)))),
            new(RightArm,
                Target: new(Smooth, new(0f, new(0.3852f, 0.3071f)), new(0.6f, new(0.3953f, 0.3528f)), new(1.2f, new(0.3852f, 0.3071f)))),
            new(RightLeg, Target: new(Smooth, new(0f, new(-0.048f, 0.14f)), new(0.6f, new(-0.032f, 0.124f)), new(1.2f, new(-0.048f, 0.14f)))),
        ],
        Expression = Track.Constant("strained"),
    };

    public static SkeletonClip2D RockThrowerThrow { get; } = new("rock-thrower-throw", 1.85f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.008f, -0.032f)), new(0.8f, new(0f, 0f)), new(1.05f, new(0f, 0f), Straight), new(1.14f, new(0.064f, -0.064f)),
                    new(1.3f, new(0.064f, -0.064f)), new(1.85f, new(-0.008f, -0.032f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.0195f, -0.0003f)), new(0.8f, new(0f, 0f)), new(1.05f, new(0f, 0f), Straight),
                    new(1.14f, new(0.2531f, -0.0513f)), new(1.3f, new(0.265f, -0.0565f)), new(1.85f, new(0.0195f, -0.0003f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.8f, 0f), new(1.05f, 0f, StraightTurn), new(1.14f, -0.28f), new(1.3f, -0.28f), new(1.85f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0076f, -0.0001f)), new(0.8f, new(0f, 0f)), new(1.05f, new(0f, 0f), Straight),
                    new(1.14f, new(-0.0455f, -0.0027f)), new(1.3f, new(-0.053f, -0.0037f)),
                    new(1.85f, new(-0.0076f, -0.0001f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.038f, 0.0518f)), new(0.8f, new(0.03f, 1.5f)), new(1.05f, new(0.03f, 1.5f), Straight),
                    new(1.14f, new(-0.0584f, 0.2037f)), new(1.3f, new(-0.0678f, 0.1951f)), new(1.85f, new(0.038f, 0.0518f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0138f, 0.0317f)), new(0.35f, new(-0.32f, -0.18f)), new(0.8f, new(-0.03f, 1.5f)),
                    new(1.05f, new(-0.03f, 1.5f), Straight), new(1.14f, new(0.3858f, 0.6457f)), new(1.3f, new(0.3978f, 0.6872f)),
                    new(1.85f, new(0.0138f, 0.0317f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.85f, new(-0.112f, 0f)), new(RightLeg, 0f, 1.85f, new(0.112f, 0f))],
        Markers = [new("search", 0.15f), new("lift", 0.55f), new("aim", 0.8f), new("release", 1.14f), new("recover", 1.3f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.8f, "determined"), new(1.14f, "angry")),
    };

    public static SkeletonClip2D ShieldDefenderBash { get; } = new("shield-defender-bash", 1.4f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.45f, new(-0.028f, -0.032f)), new(0.55f, new(0.048f, -0.048f)), new(0.7f, new(0.048f, -0.048f)),
                    new(1.4f, new(0f, 0f)))),
            new(Chest, Turn: new(SmoothTurn, new(0f, 0f), new(0.45f, 0.1f), new(0.55f, -0.16f), new(0.7f, -0.16f), new(1.4f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.5f, 0.12f)), new(0.25f, new(-0.35f, 0.22f)), new(0.45f, new(-0.35f, 0.22f)), new(0.55f, new(0.78f, 0.3f)),
                    new(0.7f, new(0.78f, 0.3f)), new(0.95f, new(-0.25f, 0.12f)), new(1.2f, new(-0.25f, 0.12f)), new(1.4f, new(0.5f, 0.12f)))),
            new(RightArm,
                Target: new(Smooth, new(0f, new(0f, 0f)), new(0.45f, new(0.1f, 0.15f)), new(0.55f, new(0.15f, 0.2f)), new(1.4f, new(0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.4f, new(0f, 0f)), new(RightLeg, 0f, 1.4f, new(0f, 0f))],
        Markers = [new("pullback", 0.2f), new("strike", 0.55f), new("recover", 0.7f)],
        Expression = Track.Held<string>(new(0f, "focused"), new(0.45f, "determined"), new(0.55f, "angry")),
    };

    public static SkeletonClip2D ShieldDefenderIdle { get; } = new("shield-defender-idle", 2f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(0f, -0.0096f)), new(2f, new(0f, 0f)))),
            new(Chest, Turn: new(SmoothTurn, new(0f, 0f), new(1f, 0.018f), new(2f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.5f, 0.12f)), new(2f, new(0.5f, 0.12f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(0.01f, 0.02f)), new(2f, new(0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 2f, new(0f, 0f)), new(RightLeg, 0f, 2f, new(0f, 0f))],
    };

    public static SkeletonClip2D ShieldDefenderWalk { get; } = new("shield-defender-walk", 1.2f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.02f)), new(0.075f, new(0f, -0.0158f)), new(0.225f, new(0f, 0.0046f)), new(0.3f, new(0f, 0.0088f)),
                    new(0.375f, new(0f, 0.0046f)), new(0.525f, new(0f, -0.0158f)), new(0.6f, new(0f, -0.02f)), new(0.675f, new(0f, -0.0158f)),
                    new(0.825f, new(0f, 0.0046f)), new(0.9f, new(0f, 0.0088f)), new(0.975f, new(0f, 0.0046f)), new(1.125f, new(0f, -0.0158f)),
                    new(1.2f, new(0f, -0.02f)))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.5f, 0.12f)), new(1.2f, new(0.5f, 0.12f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.075f, new(-0.1078f, 0.0141f)), new(0.15f, new(-0.0875f, 0.048f)),
                    new(0.225f, new(-0.0484f, 0.0819f)), new(0.3f, new(0f, 0.096f)), new(0.375f, new(0.0484f, 0.0819f)),
                    new(0.45f, new(0.0875f, 0.048f)), new(0.525f, new(0.1078f, 0.0141f)), new(0.6f, new(0.1f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.21f, 0.04f)), new(0.075f, new(-0.1963f, 0.04f)), new(0.15f, new(-0.1573f, 0.04f)),
                    new(0.225f, new(-0.0989f, 0.04f)), new(0.375f, new(0.0389f, 0.04f)), new(0.45f, new(0.0973f, 0.04f)),
                    new(0.525f, new(0.1363f, 0.04f)), new(0.6f, new(0.15f, 0.04f)), new(0.675f, new(0.1363f, 0.04f)), new(0.75f, new(0.0973f, 0.04f)),
                    new(0.825f, new(0.0389f, 0.04f)), new(0.975f, new(-0.0989f, 0.04f)), new(1.05f, new(-0.1573f, 0.04f)),
                    new(1.125f, new(-0.1963f, 0.04f)), new(1.2f, new(-0.21f, 0.04f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0f)), new(0.6f, new(-0.1f, 0f)), new(0.675f, new(-0.1078f, 0.0141f)), new(0.75f, new(-0.0875f, 0.048f)),
                    new(0.825f, new(-0.0484f, 0.0819f)), new(0.9f, new(0f, 0.096f)), new(0.975f, new(0.0484f, 0.0819f)),
                    new(1.05f, new(0.0875f, 0.048f)), new(1.125f, new(0.1078f, 0.0141f)), new(1.2f, new(0.1f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.6f, new(0.1f, 0f)), new(LeftLeg, 0.6f, 1.2f, new(0.3f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(1.2f, new(0.4f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        ClubCavemanSlam, PersonDeath, PersonHammerSlam, PersonHeavyWalk, PersonHit, PersonIdle, PersonJump, PersonPistolShot, PersonRun,
        PersonThrust, PersonWalk, PlayerBalanceBackward, PlayerBalanceForward, PlayerCelebrate, PlayerClimb, PlayerClimbOff, PlayerClimbOn,
        PlayerDash, PlayerDeath, PlayerFall, PlayerGunAim, PlayerGunCharge, PlayerGunShot, PlayerGunWallShot, PlayerHealGather, PlayerHit,
        PlayerIdle, PlayerJump, PlayerLand, PlayerSwordBackhand, PlayerSwordDownAttack, PlayerSwordForehand, PlayerSwordPutAway,
        PlayerSwordPutAwayBackhand, PlayerSwordSheathe, PlayerSwordSideCut, PlayerWallGrip, RockThrowerThrow, ShieldDefenderBash, ShieldDefenderIdle,
        ShieldDefenderWalk];
}
