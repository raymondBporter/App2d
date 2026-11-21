// Generated from the authored JSON clips by LegacyClipEmitter in App2d.Tests. While those clips exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named PersonClipTests) instead of editing this file.
using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.PersonSkeleton;

namespace App2d.Core.Characters;

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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.55f, new(-0.160813f, -0.0202069f)),
                    new(0.7f, new(-0.179631f, -0.025314f), Straight), new(0.78f, new(0.253122f, -0.0513104f)),
                    new(0.95f, new(0.265044f, -0.0564922f)), new(1.35f, new(0.265044f, -0.0564922f)), new(1.8f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.55f, 0.25f), new(0.7f, 0.28f, StraightTurn), new(0.78f, -0.4f), new(0.95f, -0.42f), new(1.35f, -0.42f),
                    new(1.8f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.55f, new(0.00759949f, -7.59959E-05f)),
                    new(0.7f, new(0.0113983f, -0.000170976f), Straight), new(0.78f, new(-0.0454906f, -0.00273272f)),
                    new(0.95f, new(-0.0530264f, -0.0037179f)), new(1.35f, new(-0.0530264f, -0.0037179f)),
                    new(1.8f, new(-0.00759949f, -7.59959E-05f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn,
                    new(0f, 0.33f), new(0.55f, 2.15f), new(0.7f, 2.27f, StraightTurn), new(0.78f, -0.5f), new(0.95f, -0.63f), new(1.35f, -0.63f),
                    new(1.8f, 0.33f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.55f, new(-0.0444405f, 0.189249f)), new(0.7f, new(-0.0362698f, 0.193973f), Straight),
                    new(0.78f, new(-0.0584449f, 0.203692f)), new(0.95f, new(-0.0677754f, 0.195057f)), new(1.35f, new(-0.0677754f, 0.195057f)),
                    new(1.8f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0138332f, 0.0317393f)), new(0.55f, new(0.31949f, 0.290976f)), new(0.7f, new(0.290796f, 0.223032f), Straight),
                    new(0.78f, new(0.385794f, 0.64568f)), new(0.95f, new(0.397833f, 0.687225f)), new(1.35f, new(0.397833f, 0.687225f)),
                    new(1.8f, new(0.0138332f, 0.0317393f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.418879f, 0f, 0f)), new(0.55f, new(0.733038f, -0.244346f, 0f)), new(0.7f, new(0.733038f, -0.244346f, 0f)),
                new(0.78f, new(0.314159f, 0f, 0f)), new(0.95f, new(0.314159f, 0f, 0f)), new(1.35f, new(0.314159f, 0f, 0f)),
                new(1.8f, new(0.418879f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.8f, new(-0.112f, 0f)), new(RightLeg, 0f, 1.8f, new(0.112f, 0f))],
        Markers = [new("windup", 0.1f), new("strike", 0.78f), new("recover", 0.95f)],
    };

    public static SkeletonClip2D PersonDeath { get; } = new("person-death", 1.1f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(-0.036f, -0.02f), Straight), new(0.016666668f, new(-0.104f, -0.048f), Straight), new(0.075f, new(-0.128f, -0.064f)),
                    new(0.25f, new(-0.16f, -0.16f)), new(0.75f, new(-0.224f, -0.648f)), new(1.1f, new(-0.24f, -0.632f))),
                Turn: new(SmoothTurn,
                    new(0f, 0.16f, StraightTurn), new(0.016666668f, 0.34f, StraightTurn), new(0.075f, 0.37f), new(0.25f, 0.53f), new(0.75f, 1.42f),
                    new(1.1f, 1.45f))),
            new(Chest,
                Turn: new(SmoothTurn,
                    new(0f, 0.08f, StraightTurn), new(0.016666668f, 0.18f, StraightTurn), new(0.075f, 0.18f), new(0.25f, 0.12f), new(0.75f, 0.05f),
                    new(1.1f, 0.05f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0.1f)), new(0.016666668f, new(0.24f, 0.18f)), new(0.075f, new(0.24f, 0.18f), Smooth),
                    new(0.4f, new(0.15f, 0.25f), Smooth), new(1.1f, new(-0.1f, 0.05f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.05f, 0.06f)), new(0.016666668f, new(-0.14f, 0.18f)), new(0.075f, new(-0.14f, 0.18f), Smooth),
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.55f, new(-0.160813f, -0.0202069f)),
                    new(0.7f, new(-0.179631f, -0.025314f), Straight), new(0.78f, new(0.253122f, -0.0513104f)),
                    new(0.95f, new(0.265044f, -0.0564922f)), new(1.5f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.55f, 0.25f), new(0.7f, 0.28f, StraightTurn), new(0.78f, -0.4f), new(0.95f, -0.42f), new(1.5f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.55f, new(0.00759949f, -7.59959E-05f)),
                    new(0.7f, new(0.0113983f, -0.000170976f), Straight), new(0.78f, new(-0.0454906f, -0.00273272f)),
                    new(0.95f, new(-0.0530264f, -0.0037179f)), new(1.5f, new(-0.00759949f, -7.59959E-05f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn,
                    new(0f, 0.33f), new(0.55f, 2.15f), new(0.7f, 2.27f, StraightTurn), new(0.78f, -0.5f), new(0.95f, -0.63f), new(1.5f, 0.33f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.55f, new(-0.0444405f, 0.189249f)), new(0.7f, new(-0.0362698f, 0.193973f), Straight),
                    new(0.78f, new(-0.0584449f, 0.203692f)), new(0.95f, new(-0.0677754f, 0.195057f)), new(1.5f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0138332f, 0.0317393f)), new(0.55f, new(0.31949f, 0.290976f)), new(0.7f, new(0.290796f, 0.223032f), Straight),
                    new(0.78f, new(0.385794f, 0.64568f)), new(0.95f, new(0.397833f, 0.687225f)), new(1.5f, new(0.0138332f, 0.0317393f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.418879f, 0f, 0f)), new(0.55f, new(0.733038f, -0.244346f, 0f)), new(0.7f, new(0.733038f, -0.244346f, 0f)),
                new(0.78f, new(0.314159f, 0f, 0f)), new(0.95f, new(0.314159f, 0f, 0f)), new(1.5f, new(0.418879f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.5f, new(-0.112f, 0f)), new(RightLeg, 0f, 1.5f, new(0.112f, 0f))],
        Markers = [new("windup", 0.1f), new("strike", 0.78f), new("recover", 0.95f)],
    };

    public static SkeletonClip2D PersonHeavyWalk { get; } = new("person-heavy-walk", 1.5600001f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.0681317f)), new(0.097500004f, new(0f, -0.0613835f)), new(0.19500001f, new(0f, -0.0450918f)),
                    new(0.2925f, new(0f, -0.0288001f)), new(0.39000002f, new(0f, -0.0220517f)), new(0.48749998f, new(0f, -0.0288001f)),
                    new(0.585f, new(0f, -0.0450918f)), new(0.6825f, new(0f, -0.0613835f)), new(0.78000003f, new(0f, -0.0681317f)),
                    new(0.8775f, new(0f, -0.0613835f)), new(0.97499996f, new(0f, -0.0450917f)), new(1.0725f, new(0f, -0.0288001f)),
                    new(1.17f, new(0f, -0.0220517f)), new(1.2675f, new(0f, -0.0288001f)), new(1.365f, new(0f, -0.0450918f)),
                    new(1.4625f, new(0f, -0.0613835f)), new(1.5600001f, new(0f, -0.0681317f)))),
            new(Chest, Move: Track.Of<Vector2f>(new(0f, new(0.05f, 0f)), new(1.5600001f, new(0.05f, 0f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.0991765f, 0.04f)), new(0.097500004f, new(0.0895853f, 0.04f)), new(0.19500001f, new(0.0622719f, 0.04f)),
                    new(0.2925f, new(0.0213946f, 0.0400001f)), new(0.39000002f, new(-0.0268235f, 0.04f)),
                    new(0.48749998f, new(-0.0750417f, 0.0400001f)), new(0.585f, new(-0.115919f, 0.04f)), new(0.6825f, new(-0.143232f, 0.04f)),
                    new(0.78000003f, new(-0.152824f, 0.04f)), new(0.8775f, new(-0.143232f, 0.04f)), new(0.97499996f, new(-0.115919f, 0.04f)),
                    new(1.0725f, new(-0.0750416f, 0.0400001f)), new(1.17f, new(-0.0268235f, 0.04f)), new(1.2675f, new(0.0213946f, 0.04f)),
                    new(1.365f, new(0.062272f, 0.04f)), new(1.4625f, new(0.0895853f, 0.04f)), new(1.5600001f, new(0.0991765f, 0.04f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.097500004f, new(-0.107813f, 0.0140589f)), new(0.19500001f, new(-0.0875f, 0.048f)),
                    new(0.2925f, new(-0.0484375f, 0.0819411f)), new(0.39000002f, new(0f, 0.096f)), new(0.48749998f, new(0.0484375f, 0.0819411f)),
                    new(0.585f, new(0.0875f, 0.048f)), new(0.6825f, new(0.107813f, 0.0140589f)), new(0.78000003f, new(0.1f, 0f)),
                    new(0.8775f, new(0.075f, 0f)), new(0.97499996f, new(0.05f, 0f)), new(1.0725f, new(0.025f, 0f)), new(1.17f, new(0f, 0f)),
                    new(1.2675f, new(-0.025f, 0f)), new(1.365f, new(-0.05f, 0f)), new(1.4625f, new(-0.075f, 0f)), new(1.5600001f, new(-0.1f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.159176f, 0.04f)), new(0.097500004f, new(-0.149585f, 0.04f)), new(0.19500001f, new(-0.122272f, 0.04f)),
                    new(0.2925f, new(-0.0813946f, 0.0400001f)), new(0.39000002f, new(-0.0331765f, 0.04f)),
                    new(0.48749998f, new(0.0150417f, 0.0400001f)), new(0.585f, new(0.055919f, 0.04f)), new(0.6825f, new(0.0832324f, 0.04f)),
                    new(0.78000003f, new(0.0928235f, 0.04f)), new(0.8775f, new(0.0832324f, 0.04f)), new(0.97499996f, new(0.055919f, 0.04f)),
                    new(1.0725f, new(0.0150416f, 0.0400001f)), new(1.17f, new(-0.0331765f, 0.04f)), new(1.2675f, new(-0.0813946f, 0.04f)),
                    new(1.365f, new(-0.122272f, 0.04f)), new(1.4625f, new(-0.149585f, 0.04f)), new(1.5600001f, new(-0.159176f, 0.04f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0f)), new(0.097500004f, new(0.075f, 0f)), new(0.19500001f, new(0.05f, 0f)), new(0.2925f, new(0.025f, 0f)),
                    new(0.39000002f, new(0f, 0f)), new(0.48749998f, new(-0.025f, 0f)), new(0.585f, new(-0.05f, 0f)), new(0.6825f, new(-0.075f, 0f)),
                    new(0.78000003f, new(-0.1f, 0f)), new(0.8775f, new(-0.107813f, 0.0140589f)), new(0.97499996f, new(-0.0875f, 0.048f)),
                    new(1.0725f, new(-0.0484375f, 0.0819411f)), new(1.17f, new(0f, 0.096f)), new(1.2675f, new(0.0484375f, 0.0819411f)),
                    new(1.365f, new(0.0875f, 0.048f)), new(1.4625f, new(0.107813f, 0.0140589f)), new(1.5600001f, new(0.1f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.78000003f, new(0.1f, 0f)), new(LeftLeg, 0.78000003f, 1.5600001f, new(0.3f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(0.097500004f, new(0.025f, 0f)), new(0.19500001f, new(0.05f, 0f)), new(0.2925f, new(0.075f, 0f)),
            new(0.39000002f, new(0.1f, 0f)), new(0.48749998f, new(0.125f, 0f)), new(0.585f, new(0.15f, 0f)), new(0.6825f, new(0.175f, 0f)),
            new(0.78000003f, new(0.2f, 0f)), new(0.8775f, new(0.225f, 0f)), new(0.97499996f, new(0.25f, 0f)), new(1.0725f, new(0.275f, 0f)),
            new(1.17f, new(0.3f, 0f)), new(1.2675f, new(0.325f, 0f)), new(1.365f, new(0.35f, 0f)), new(1.4625f, new(0.375f, 0f)),
            new(1.5600001f, new(0.4f, 0f))),
    };

    public static SkeletonClip2D PersonHit { get; } = new("person-hit", 0.3f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.036f, -0.02f)), new(0.016666668f, new(-0.08f, -0.044f)), new(0.05f, new(-0.08f, -0.044f), Smooth),
                    new(0.17f, new(-0.024f, -0.012f), Smooth), new(0.3f, new(0f, 0f))),
                Turn: Track.Linear(
                    new(0f, 0.16f), new(0.016666668f, 0.32f), new(0.05f, 0.32f, SmoothTurn), new(0.17f, 0.08f, SmoothTurn), new(0.3f, 0f))),
            new(Chest,
                Turn: Track.Linear(
                    new(0f, 0.08f), new(0.016666668f, 0.16f), new(0.05f, 0.16f, SmoothTurn), new(0.17f, 0.04f, SmoothTurn), new(0.3f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0.1f)), new(0.016666668f, new(0.22f, 0.16f)), new(0.05f, new(0.22f, 0.16f), Smooth), new(0.3f, new(0f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.05f, 0.06f)), new(0.016666668f, new(-0.12f, 0.16f)), new(0.05f, new(-0.12f, 0.16f), Smooth),
                    new(0.3f, new(0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.3f, new(0f, 0f)), new(RightLeg, 0f, 0.3f, new(0f, 0f))],
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.35f, new(0f, 0f)), new(0.6f, new(0f, 0f), Straight),
                    new(0.63f, new(-0.0389766f, -0.00116962f)), new(0.85f, new(0f, 0f)), new(1.1f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.35f, 0f), new(0.6f, 0f, StraightTurn), new(0.63f, 0.06f), new(0.85f, 0f), new(1.1f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.35f, new(-0.00759949f, -7.59959E-05f)),
                    new(0.6f, new(-0.00759949f, -7.59959E-05f), Straight), new(0.63f, new(-0.0189921f, -0.0004749f)),
                    new(0.85f, new(-0.00759949f, -7.59959E-05f)), new(1.1f, new(-0.00759949f, -7.59959E-05f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn,
                    new(0f, 0.03f), new(0.35f, 0f), new(0.6f, 0f, StraightTurn), new(0.63f, 0.32f), new(0.85f, 0f), new(1.1f, 0.03f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.35f, new(-0.01f, 0.05f)), new(0.6f, new(-0.01f, 0.05f), Straight),
                    new(0.63f, new(-0.0559964f, 0.0504801f)), new(0.85f, new(-0.01f, 0.05f)), new(1.1f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.05f, 0.0650001f)), new(0.35f, new(0.615f, 0.63f)), new(0.6f, new(0.615f, 0.63f), Straight),
                    new(0.63f, new(0.471479f, 0.449703f)), new(0.85f, new(0.615f, 0.63f)), new(1.1f, new(0.05f, 0.0650001f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(
                new(0f, new(-0.20944f, 0.174533f, 0f)), new(0.58f, new(-0.20944f, 0.174533f, 0f)), new(0.65f, new(-0.453786f, 0.244346f, 0f)),
                new(1.1f, new(-0.20944f, 0.174533f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.1f, new(-0.052f, 0f)), new(RightLeg, 0f, 1.1f, new(0.052f, 0f))],
        Markers = [new("fire", 0.6f)],
    };

    public static SkeletonClip2D PersonRun { get; } = new("person-run", 0.72f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.044f)), new(0.020000001f, new(0f, -0.0580435f)), new(0.040000003f, new(0f, -0.0749353f)),
                    new(0.060000002f, new(0f, -0.0901505f)), new(0.080000006f, new(0f, -0.0991643f)), new(0.10000001f, new(0f, -0.0994864f)),
                    new(0.120000005f, new(0f, -0.0954934f)), new(0.14f, new(0f, -0.0885174f)), new(0.16000001f, new(0f, -0.0795338f)),
                    new(0.18f, new(0f, -0.0695176f)), new(0.20000002f, new(0f, -0.0594443f)), new(0.22000001f, new(0f, -0.0500977f)),
                    new(0.24000001f, new(0f, -0.0372699f)), new(0.26000002f, new(0f, -0.0231194f)), new(0.28f, new(0f, -0.013378f)),
                    new(0.3f, new(0f, -0.0129815f)), new(0.32000002f, new(0f, -0.0204061f)), new(0.34f, new(0f, -0.0319458f)),
                    new(0.36f, new(0f, -0.044f)), new(0.38000003f, new(0f, -0.0580435f)), new(0.40000004f, new(0f, -0.0749353f)),
                    new(0.42000002f, new(0f, -0.0901506f)), new(0.44000003f, new(0f, -0.0991643f)), new(0.46f, new(0f, -0.0994864f)),
                    new(0.48000002f, new(0f, -0.0954934f)), new(0.5f, new(0f, -0.0885174f)), new(0.52000004f, new(0f, -0.0795338f)),
                    new(0.54f, new(0f, -0.0695176f)), new(0.56f, new(0f, -0.0594443f)), new(0.58000004f, new(0f, -0.0500977f)),
                    new(0.6f, new(0f, -0.0372699f)), new(0.62f, new(0f, -0.0231194f)), new(0.64000005f, new(0f, -0.013378f)),
                    new(0.66f, new(0f, -0.0129815f)), new(0.68f, new(0f, -0.020406f)), new(0.70000005f, new(0f, -0.0319458f)),
                    new(0.72f, new(0f, -0.044f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.120031f, -0.00710267f)), new(0.020000001f, new(0.117112f, -0.00596797f)),
                    new(0.040000003f, new(0.116416f, -0.00482887f)), new(0.060000002f, new(0.118027f, -0.00562912f)),
                    new(0.080000006f, new(0.121749f, -0.00835937f)), new(0.10000001f, new(0.127127f, -0.0123315f)),
                    new(0.120000005f, new(0.133505f, -0.0151563f)), new(0.14f, new(0.140109f, -0.0175983f)),
                    new(0.16000001f, new(0.146144f, -0.0196513f)), new(0.18f, new(0.150887f, -0.0211259f)),
                    new(0.20000002f, new(0.153773f, -0.021854f)), new(0.22000001f, new(0.154461f, -0.0217853f)),
                    new(0.24000001f, new(0.152869f, -0.0224895f)), new(0.26000002f, new(0.149185f, -0.0221335f)),
                    new(0.28f, new(0.143847f, -0.0194877f)), new(0.3f, new(0.137492f, -0.0149825f)), new(0.32000002f, new(0.130882f, -0.0108967f)),
                    new(0.34f, new(0.124816f, -0.00824225f)), new(0.36f, new(0.120031f, -0.00710267f)),
                    new(0.38000003f, new(0.117112f, -0.00596792f)), new(0.40000004f, new(0.116416f, -0.00482887f)),
                    new(0.42000002f, new(0.118028f, -0.00562906f)), new(0.44000003f, new(0.121749f, -0.00835937f)),
                    new(0.46f, new(0.127127f, -0.0123315f)), new(0.48000002f, new(0.133505f, -0.0151563f)), new(0.5f, new(0.140109f, -0.0175983f)),
                    new(0.52000004f, new(0.146144f, -0.0196513f)), new(0.54f, new(0.150887f, -0.0211259f)), new(0.56f, new(0.153773f, -0.021854f)),
                    new(0.58000004f, new(0.154461f, -0.0217853f)), new(0.6f, new(0.152869f, -0.0224895f)), new(0.62f, new(0.149185f, -0.0221335f)),
                    new(0.64000005f, new(0.143847f, -0.0194877f)), new(0.66f, new(0.137492f, -0.0149825f)), new(0.68f, new(0.130882f, -0.0108967f)),
                    new(0.70000005f, new(0.124816f, -0.00824225f)), new(0.72f, new(0.120031f, -0.00710267f)))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0889394f, -0.00343859f)), new(0.020000001f, new(0.0867886f, -0.00242865f)),
                    new(0.040000003f, new(0.0862753f, -0.000505805f)), new(0.060000002f, new(0.087463f, -0.000884414f)),
                    new(0.080000006f, new(0.090205f, -0.00417697f)), new(0.10000001f, new(0.094164f, -0.00969946f)),
                    new(0.120000005f, new(0.0988549f, -0.0142411f)), new(0.14f, new(0.103707f, -0.0173991f)),
                    new(0.16000001f, new(0.108136f, -0.0199536f)), new(0.18f, new(0.111614f, -0.0217316f)),
                    new(0.20000002f, new(0.113728f, -0.0225793f)), new(0.22000001f, new(0.114232f, -0.0224558f)),
                    new(0.24000001f, new(0.113066f, -0.0229781f)), new(0.26000002f, new(0.110367f, -0.0237908f)),
                    new(0.28f, new(0.106452f, -0.0212396f)), new(0.3f, new(0.101785f, -0.0155537f)), new(0.32000002f, new(0.0969262f, -0.00932407f)),
                    new(0.34f, new(0.0924628f, -0.00523198f)), new(0.36f, new(0.0889394f, -0.00343859f)),
                    new(0.38000003f, new(0.0867885f, -0.00242865f)), new(0.40000004f, new(0.0862752f, -0.000505805f)),
                    new(0.42000002f, new(0.0874629f, -0.000884533f)), new(0.44000003f, new(0.0902051f, -0.00417697f)),
                    new(0.46f, new(0.0941639f, -0.00969946f)), new(0.48000002f, new(0.0988549f, -0.0142411f)), new(0.5f, new(0.103707f, -0.017399f)),
                    new(0.52000004f, new(0.108136f, -0.0199536f)), new(0.54f, new(0.111614f, -0.0217316f)), new(0.56f, new(0.113728f, -0.0225793f)),
                    new(0.58000004f, new(0.114232f, -0.0224557f)), new(0.6f, new(0.113066f, -0.0229781f)), new(0.62f, new(0.110366f, -0.0237908f)),
                    new(0.64000005f, new(0.106452f, -0.0212396f)), new(0.66f, new(0.101785f, -0.0155537f)),
                    new(0.68f, new(0.0969261f, -0.00932395f)), new(0.70000005f, new(0.0924628f, -0.00523198f)),
                    new(0.72f, new(0.0889394f, -0.00343859f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0166157f, 0.0219936f)), new(0.020000001f, new(0.0174104f, 0.0214256f)),
                    new(0.040000003f, new(0.0170913f, 0.0212903f)), new(0.060000002f, new(0.0156609f, 0.0216033f)),
                    new(0.080000006f, new(0.0131792f, 0.0223289f)), new(0.10000001f, new(0.00975817f, 0.0233815f)),
                    new(0.120000005f, new(0.00555169f, 0.0246379f)), new(0.14f, new(0.000741772f, 0.0259473f)),
                    new(0.16000001f, new(-0.00447506f, 0.0271516f)), new(0.18f, new(-0.00989936f, 0.0281031f)),
                    new(0.20000002f, new(-0.0153377f, 0.0286844f)), new(0.22000001f, new(-0.0206087f, 0.0288233f)),
                    new(0.24000001f, new(-0.0255489f, 0.0285022f)), new(0.26000002f, new(-0.0300165f, 0.0277612f)),
                    new(0.28f, new(-0.0338968f, 0.0266924f)), new(0.3f, new(-0.0371055f, 0.0254272f)),
                    new(0.32000002f, new(-0.0395904f, 0.0241201f)), new(0.34f, new(-0.0413289f, 0.0229285f)),
                    new(0.36f, new(-0.0423215f, 0.0219936f)), new(0.38000003f, new(-0.0425837f, 0.0214257f)),
                    new(0.40000004f, new(-0.0421369f, 0.0212903f)), new(0.42000002f, new(-0.0410017f, 0.0216035f)),
                    new(0.44000003f, new(-0.0391961f, 0.0223289f)), new(0.46f, new(-0.0367386f, 0.0233815f)),
                    new(0.48000002f, new(-0.0336536f, 0.0246377f)), new(0.5f, new(-0.0299808f, 0.0259473f)),
                    new(0.52000004f, new(-0.0257815f, 0.0271515f)), new(0.54f, new(-0.0211423f, 0.0281031f)),
                    new(0.56f, new(-0.0161753f, 0.0286844f)), new(0.58000004f, new(-0.0110158f, 0.0288233f)),
                    new(0.6f, new(-0.00581688f, 0.0285022f)), new(0.62f, new(-0.000745006f, 0.0277612f)),
                    new(0.64000005f, new(0.00402503f, 0.0266924f)), new(0.66f, new(0.00831418f, 0.0254272f)),
                    new(0.68f, new(0.0119472f, 0.0241201f)), new(0.70000005f, new(0.0147607f, 0.0229285f)), new(0.72f, new(0.0166158f, 0.0219936f)))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0210281f, 0f)), new(0.020000001f, new(-0.0184713f, 0f)), new(0.040000003f, new(-0.0155372f, 0f)),
                    new(0.060000002f, new(-0.0122297f, 0f)), new(0.080000006f, new(-0.00873926f, 0f)), new(0.10000001f, new(-0.00525645f, 0f)),
                    new(0.120000005f, new(-0.00174353f, 0f)), new(0.14f, new(0.00190618f, 0f)), new(0.16000001f, new(0.00555017f, 0f)),
                    new(0.18f, new(0.00904555f, 0f)), new(0.20000002f, new(0.0125012f, 0f)), new(0.22000001f, new(0.0163735f, 0f)),
                    new(0.24000001f, new(0.0199466f, 0f)), new(0.26000002f, new(0.0224108f, 0f)), new(0.28f, new(0.0229589f, 0f)),
                    new(0.3f, new(0.0213119f, 0f)), new(0.32000002f, new(0.018253f, 0f)), new(0.34f, new(0.0146874f, 0f)),
                    new(0.36f, new(0.01152f, 0f)), new(0.38000003f, new(0.00867557f, 0f)), new(0.40000004f, new(0.0058311f, 0f)),
                    new(0.42000002f, new(0.00298667f, 0f)), new(0.44000003f, new(0.000142238f, 0f)), new(0.46f, new(-0.00270219f, 0f)),
                    new(0.48000002f, new(-0.00554667f, 0f)), new(0.5f, new(-0.00839114f, 0f)), new(0.52000004f, new(-0.0112356f, 0f)),
                    new(0.54f, new(-0.01408f, 0f)), new(0.56f, new(-0.0169244f, 0f)), new(0.58000004f, new(-0.0197125f, 0f)),
                    new(0.6f, new(-0.0225185f, 0f)), new(0.62f, new(-0.025127f, 0f)), new(0.64000005f, new(-0.0267149f, 0f)),
                    new(0.66f, new(-0.0266574f, 0f)), new(0.68f, new(-0.0254396f, 0f)), new(0.70000005f, new(-0.023452f, 0f)),
                    new(0.72f, new(-0.0210282f, 0f)))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0454172f, -0.0112457f)), new(0.020000001f, new(-0.0455294f, -0.0110055f)),
                    new(0.040000003f, new(-0.0450474f, -0.0109479f)), new(0.060000002f, new(-0.043994f, -0.011081f)),
                    new(0.080000006f, new(-0.0423819f, -0.0113864f)), new(0.10000001f, new(-0.0402147f, -0.0118229f)),
                    new(0.120000005f, new(-0.0374912f, -0.0123327f)), new(0.14f, new(-0.0342122f, -0.0128522f)),
                    new(0.16000001f, new(-0.0303902f, -0.013319f)), new(0.18f, new(-0.0260592f, -0.0136809f)),
                    new(0.20000002f, new(-0.0212849f, -0.0138988f)), new(0.22000001f, new(-0.0161719f, -0.0139503f)),
                    new(0.24000001f, new(-0.0108657f, -0.0138307f)), new(0.26000002f, new(-0.00555009f, -0.0135517f)),
                    new(0.28f, new(-0.000438102f, -0.0131423f)), new(0.3f, new(0.00424122f, -0.0126474f)),
                    new(0.32000002f, new(0.00826042f, -0.0121241f)), new(0.34f, new(0.0114109f, -0.011636f)), new(0.36f, new(0.01352f, -0.0112457f)),
                    new(0.38000003f, new(0.0144647f, -0.0110054f)), new(0.40000004f, new(0.0141808f, -0.0109479f)),
                    new(0.42000002f, new(0.0126686f, -0.011081f)), new(0.44000003f, new(0.00999343f, -0.0113864f)),
                    new(0.46f, new(0.00628196f, -0.0118229f)), new(0.48000002f, new(0.00171398f, -0.0123327f)),
                    new(0.5f, new(-0.00348974f, -0.0128522f)), new(0.52000004f, new(-0.00908376f, -0.0133191f)),
                    new(0.54f, new(-0.0148163f, -0.0136809f)), new(0.56f, new(-0.0204471f, -0.0138988f)),
                    new(0.58000004f, new(-0.0257648f, -0.0139503f)), new(0.6f, new(-0.0305977f, -0.0138307f)),
                    new(0.62f, new(-0.0348216f, -0.0135517f)), new(0.64000005f, new(-0.0383598f, -0.0131423f)),
                    new(0.66f, new(-0.0411786f, -0.0126474f)), new(0.68f, new(-0.0432772f, -0.0121241f)),
                    new(0.70000005f, new(-0.0446787f, -0.011636f)), new(0.72f, new(-0.0454171f, -0.0112457f)))),
            new(RightHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.01152f, 0f)), new(0.020000001f, new(0.00867555f, 0f)), new(0.040000003f, new(0.00583112f, 0f)),
                    new(0.060000002f, new(0.00298666f, 0f)), new(0.080000006f, new(0.00014222f, 0f)), new(0.10000001f, new(-0.00270221f, 0f)),
                    new(0.120000005f, new(-0.00554666f, 0f)), new(0.14f, new(-0.00839111f, 0f)), new(0.16000001f, new(-0.0112355f, 0f)),
                    new(0.18f, new(-0.01408f, 0f)), new(0.20000002f, new(-0.0169245f, 0f)), new(0.22000001f, new(-0.0197125f, 0f)),
                    new(0.24000001f, new(-0.0225185f, 0f)), new(0.26000002f, new(-0.025127f, 0f)), new(0.28f, new(-0.0267149f, 0f)),
                    new(0.3f, new(-0.0266574f, 0f)), new(0.32000002f, new(-0.0254395f, 0f)), new(0.34f, new(-0.023452f, 0f)),
                    new(0.36f, new(-0.0210281f, 0f)), new(0.38000003f, new(-0.0184713f, 0f)), new(0.40000004f, new(-0.0155372f, 0f)),
                    new(0.42000002f, new(-0.0122297f, 0f)), new(0.44000003f, new(-0.00873928f, 0f)), new(0.46f, new(-0.00525641f, 0f)),
                    new(0.48000002f, new(-0.00174355f, 0f)), new(0.5f, new(0.00190616f, 0f)), new(0.52000004f, new(0.00555015f, 0f)),
                    new(0.54f, new(0.00904556f, 0f)), new(0.56f, new(0.0125013f, 0f)), new(0.58000004f, new(0.0163735f, 0f)),
                    new(0.6f, new(0.0199465f, 0f)), new(0.62f, new(0.0224108f, 0f)), new(0.64000005f, new(0.0229589f, 0f)),
                    new(0.66f, new(0.0213118f, 0f)), new(0.68f, new(0.018253f, 0f)), new(0.70000005f, new(0.0146874f, 0f)),
                    new(0.72f, new(0.01152f, 0f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.227799f, 0.387478f)), new(0.020000001f, new(0.232302f, 0.380644f)), new(0.040000003f, new(0.228117f, 0.37139f)),
                    new(0.060000002f, new(0.2153f, 0.359991f)), new(0.080000006f, new(0.194203f, 0.346943f)),
                    new(0.10000001f, new(0.165482f, 0.3329f)), new(0.120000005f, new(0.130092f, 0.318589f)), new(0.14f, new(0.0892594f, 0.304704f)),
                    new(0.16000001f, new(0.0444289f, 0.291812f)), new(0.18f, new(-0.00280845f, 0.280294f)),
                    new(0.20000002f, new(-0.0508033f, 0.270341f)), new(0.22000001f, new(-0.097936f, 0.261986f)),
                    new(0.24000001f, new(-0.142696f, 0.255183f)), new(0.26000002f, new(-0.183742f, 0.249884f)),
                    new(0.28f, new(-0.219943f, 0.246111f)), new(0.3f, new(-0.250398f, 0.243992f)), new(0.32000002f, new(-0.274433f, 0.24375f)),
                    new(0.34f, new(-0.291583f, 0.245655f)), new(0.36f, new(-0.301568f, 0.249944f)), new(0.38000003f, new(-0.304255f, 0.256742f)),
                    new(0.40000004f, new(-0.299637f, 0.265989f)), new(0.42000002f, new(-0.287822f, 0.277405f)),
                    new(0.44000003f, new(-0.269041f, 0.290502f)), new(0.46f, new(-0.243671f, 0.304628f)),
                    new(0.48000002f, new(-0.212262f, 0.31906f)), new(0.5f, new(-0.175559f, 0.333096f)), new(0.52000004f, new(-0.13451f, 0.34615f)),
                    new(0.54f, new(-0.0902491f, 0.357809f)), new(0.56f, new(-0.0440681f, 0.367856f)), new(0.58000004f, new(0.00263223f, 0.376233f)),
                    new(0.6f, new(0.048393f, 0.382984f)), new(0.62f, new(0.0917529f, 0.388167f)), new(0.64000005f, new(0.131302f, 0.391787f)),
                    new(0.66f, new(0.165736f, 0.393745f)), new(0.68f, new(0.193901f, 0.393846f)), new(0.70000005f, new(0.214835f, 0.391835f)),
                    new(0.72f, new(0.227799f, 0.387478f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.262852f, 0.335842f)), new(0.020000001f, new(-0.230891f, 0.367363f)),
                    new(0.040000003f, new(-0.194215f, 0.387297f)), new(0.060000002f, new(-0.152871f, 0.398419f)),
                    new(0.080000006f, new(-0.109241f, 0.402218f)), new(0.10000001f, new(-0.0657057f, 0.400182f)),
                    new(0.120000005f, new(-0.0217942f, 0.392132f)), new(0.14f, new(0.0238271f, 0.377226f)),
                    new(0.16000001f, new(0.0693771f, 0.356354f)), new(0.18f, new(0.113069f, 0.33041f)), new(0.20000002f, new(0.156266f, 0.298787f)),
                    new(0.22000001f, new(0.204668f, 0.258768f)), new(0.24000001f, new(0.249332f, 0.214557f)),
                    new(0.26000002f, new(0.280135f, 0.170918f)), new(0.28f, new(0.286986f, 0.132317f)), new(0.3f, new(0.266398f, 0.0976424f)),
                    new(0.32000002f, new(0.228163f, 0.064825f)), new(0.34f, new(0.183593f, 0.0326745f)), new(0.36f, new(0.144f, 0f)),
                    new(0.38000003f, new(0.108444f, 0f)), new(0.40000004f, new(0.0728889f, 0f)), new(0.42000002f, new(0.0373334f, 0f)),
                    new(0.44000003f, new(0.00177769f, 0f)), new(0.46f, new(-0.0337777f, 0f)), new(0.48000002f, new(-0.0693334f, 0f)),
                    new(0.5f, new(-0.104889f, 0f)), new(0.52000004f, new(-0.140444f, 0f)), new(0.54f, new(-0.176f, 0f)),
                    new(0.56f, new(-0.211556f, 0f)), new(0.58000004f, new(-0.246406f, 0.00491397f)), new(0.6f, new(-0.281481f, 0.041212f)),
                    new(0.62f, new(-0.314088f, 0.0898561f)), new(0.64000005f, new(-0.333937f, 0.141025f)), new(0.66f, new(-0.333218f, 0.188732f)),
                    new(0.68f, new(-0.317995f, 0.24045f)), new(0.70000005f, new(-0.29315f, 0.291561f)), new(0.72f, new(-0.262852f, 0.335842f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.301568f, 0.249944f)), new(0.020000001f, new(-0.304255f, 0.256742f)),
                    new(0.040000003f, new(-0.299637f, 0.265989f)), new(0.060000002f, new(-0.287822f, 0.277405f)),
                    new(0.080000006f, new(-0.269041f, 0.290502f)), new(0.10000001f, new(-0.243671f, 0.304628f)),
                    new(0.120000005f, new(-0.212262f, 0.31906f)), new(0.14f, new(-0.175559f, 0.333096f)), new(0.16000001f, new(-0.13451f, 0.34615f)),
                    new(0.18f, new(-0.0902493f, 0.357809f)), new(0.20000002f, new(-0.0440681f, 0.367856f)),
                    new(0.22000001f, new(0.00263211f, 0.376233f)), new(0.24000001f, new(0.048393f, 0.382984f)),
                    new(0.26000002f, new(0.0917527f, 0.388167f)), new(0.28f, new(0.131302f, 0.391787f)), new(0.3f, new(0.165736f, 0.393745f)),
                    new(0.32000002f, new(0.193901f, 0.393846f)), new(0.34f, new(0.214835f, 0.391835f)), new(0.36f, new(0.227799f, 0.387478f)),
                    new(0.38000003f, new(0.232302f, 0.380644f)), new(0.40000004f, new(0.228117f, 0.37139f)),
                    new(0.42000002f, new(0.2153f, 0.359991f)), new(0.44000003f, new(0.194203f, 0.346943f)), new(0.46f, new(0.165482f, 0.3329f)),
                    new(0.48000002f, new(0.130092f, 0.318589f)), new(0.5f, new(0.0892594f, 0.304704f)), new(0.52000004f, new(0.0444288f, 0.291812f)),
                    new(0.54f, new(-0.00280848f, 0.280294f)), new(0.56f, new(-0.0508033f, 0.270341f)), new(0.58000004f, new(-0.0979361f, 0.261986f)),
                    new(0.6f, new(-0.142696f, 0.255183f)), new(0.62f, new(-0.183742f, 0.249884f)), new(0.64000005f, new(-0.219943f, 0.246111f)),
                    new(0.66f, new(-0.250398f, 0.243992f)), new(0.68f, new(-0.274433f, 0.24375f)), new(0.70000005f, new(-0.291583f, 0.245655f)),
                    new(0.72f, new(-0.301568f, 0.249944f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.144f, 0f)), new(0.020000001f, new(0.108444f, 0f)), new(0.040000003f, new(0.0728889f, 0f)),
                    new(0.060000002f, new(0.0373333f, 0f)), new(0.080000006f, new(0.00177778f, 0f)), new(0.10000001f, new(-0.0337778f, 0f)),
                    new(0.120000005f, new(-0.0693333f, 0f)), new(0.14f, new(-0.104889f, 0f)), new(0.16000001f, new(-0.140444f, 0f)),
                    new(0.18f, new(-0.176f, 0f)), new(0.20000002f, new(-0.211556f, 0f)), new(0.22000001f, new(-0.246406f, 0.00491394f)),
                    new(0.24000001f, new(-0.281481f, 0.0412121f)), new(0.26000002f, new(-0.314088f, 0.089856f)),
                    new(0.28f, new(-0.333937f, 0.141025f)), new(0.3f, new(-0.333218f, 0.188732f)), new(0.32000002f, new(-0.317995f, 0.24045f)),
                    new(0.34f, new(-0.29315f, 0.291561f)), new(0.36f, new(-0.262852f, 0.335842f)), new(0.38000003f, new(-0.230891f, 0.367363f)),
                    new(0.40000004f, new(-0.194215f, 0.387297f)), new(0.42000002f, new(-0.152871f, 0.398419f)),
                    new(0.44000003f, new(-0.109241f, 0.402218f)), new(0.46f, new(-0.0657057f, 0.400182f)),
                    new(0.48000002f, new(-0.0217942f, 0.392132f)), new(0.5f, new(0.0238271f, 0.377226f)),
                    new(0.52000004f, new(0.0693771f, 0.356354f)), new(0.54f, new(0.113069f, 0.33041f)), new(0.56f, new(0.156266f, 0.298787f)),
                    new(0.58000004f, new(0.204668f, 0.258768f)), new(0.6f, new(0.249332f, 0.214557f)), new(0.62f, new(0.280135f, 0.170918f)),
                    new(0.64000005f, new(0.286986f, 0.132317f)), new(0.66f, new(0.266398f, 0.0976423f)), new(0.68f, new(0.228163f, 0.064825f)),
                    new(0.70000005f, new(0.183593f, 0.0326745f)), new(0.72f, new(0.144f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.21600002f, new(0.144f, 0f)), new(LeftLeg, 0.36f, 0.57600003f, new(0.784f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(0.020000001f, new(0.0355556f, 0f)), new(0.040000003f, new(0.0711111f, 0f)),
            new(0.060000002f, new(0.106667f, 0f)), new(0.080000006f, new(0.142222f, 0f)), new(0.10000001f, new(0.177778f, 0f)),
            new(0.120000005f, new(0.213333f, 0f)), new(0.14f, new(0.248889f, 0f)), new(0.16000001f, new(0.284444f, 0f)), new(0.18f, new(0.32f, 0f)),
            new(0.20000002f, new(0.355556f, 0f)), new(0.22000001f, new(0.391111f, 0f)), new(0.24000001f, new(0.426667f, 0f)),
            new(0.26000002f, new(0.462222f, 0f)), new(0.28f, new(0.497778f, 0f)), new(0.3f, new(0.533333f, 0f)),
            new(0.32000002f, new(0.568889f, 0f)), new(0.34f, new(0.604444f, 0f)), new(0.36f, new(0.64f, 0f)), new(0.38000003f, new(0.675556f, 0f)),
            new(0.40000004f, new(0.711111f, 0f)), new(0.42000002f, new(0.746667f, 0f)), new(0.44000003f, new(0.782222f, 0f)),
            new(0.46f, new(0.817778f, 0f)), new(0.48000002f, new(0.853333f, 0f)), new(0.5f, new(0.888889f, 0f)),
            new(0.52000004f, new(0.924444f, 0f)), new(0.54f, new(0.96f, 0f)), new(0.56f, new(0.995556f, 0f)), new(0.58000004f, new(1.03111f, 0f)),
            new(0.6f, new(1.06667f, 0f)), new(0.62f, new(1.10222f, 0f)), new(0.64000005f, new(1.13778f, 0f)), new(0.66f, new(1.17333f, 0f)),
            new(0.68f, new(1.20889f, 0f)), new(0.70000005f, new(1.24444f, 0f)), new(0.72f, new(1.28f, 0f))),
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
    };

    public static SkeletonClip2D PersonWalk { get; } = new("person-walk", 1.2f, loop: true)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.02f)), new(0.075f, new(0f, -0.0157824f)), new(0.15f, new(0f, -0.00560002f)),
                    new(0.22500001f, new(0f, 0.00458231f)), new(0.3f, new(0f, 0.00880003f)), new(0.375f, new(0f, 0.00458231f)),
                    new(0.45000002f, new(0f, -0.00560002f)), new(0.52500004f, new(0f, -0.0157824f)), new(0.6f, new(0f, -0.02f)),
                    new(0.675f, new(0f, -0.0157824f)), new(0.75f, new(0f, -0.00559998f)), new(0.82500005f, new(0f, 0.00458231f)),
                    new(0.90000004f, new(0f, 0.00880003f)), new(0.975f, new(0f, 0.00458231f)), new(1.0500001f, new(0f, -0.00560002f)),
                    new(1.125f, new(0f, -0.0157824f)), new(1.2f, new(0f, -0.02f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.15f, 0.04f)), new(0.075f, new(0.136298f, 0.04f)), new(0.15f, new(0.0972792f, 0.04f)),
                    new(0.22500001f, new(0.038883f, 0.0400001f)), new(0.3f, new(-0.03f, 0.04f)), new(0.375f, new(-0.098883f, 0.0400001f)),
                    new(0.45000002f, new(-0.157279f, 0.04f)), new(0.52500004f, new(-0.196298f, 0.04f)), new(0.6f, new(-0.21f, 0.04f)),
                    new(0.675f, new(-0.196298f, 0.04f)), new(0.75f, new(-0.157279f, 0.04f)), new(0.82500005f, new(-0.098883f, 0.0400001f)),
                    new(0.90000004f, new(-0.03f, 0.04f)), new(0.975f, new(0.0388831f, 0.04f)), new(1.0500001f, new(0.0972793f, 0.04f)),
                    new(1.125f, new(0.136298f, 0.04f)), new(1.2f, new(0.15f, 0.04f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.075f, new(-0.107813f, 0.0140589f)), new(0.15f, new(-0.0875f, 0.048f)),
                    new(0.22500001f, new(-0.0484375f, 0.0819411f)), new(0.3f, new(0f, 0.096f)), new(0.375f, new(0.0484375f, 0.0819411f)),
                    new(0.45000002f, new(0.0875f, 0.048f)), new(0.52500004f, new(0.107813f, 0.0140589f)), new(0.6f, new(0.1f, 0f)),
                    new(0.675f, new(0.075f, 0f)), new(0.75f, new(0.05f, 0f)), new(0.82500005f, new(0.025f, 0f)), new(0.90000004f, new(0f, 0f)),
                    new(0.975f, new(-0.025f, 0f)), new(1.0500001f, new(-0.05f, 0f)), new(1.125f, new(-0.075f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.21f, 0.04f)), new(0.075f, new(-0.196298f, 0.04f)), new(0.15f, new(-0.157279f, 0.04f)),
                    new(0.22500001f, new(-0.098883f, 0.0400001f)), new(0.3f, new(-0.03f, 0.04f)), new(0.375f, new(0.0388831f, 0.0400001f)),
                    new(0.45000002f, new(0.0972792f, 0.04f)), new(0.52500004f, new(0.136298f, 0.04f)), new(0.6f, new(0.15f, 0.04f)),
                    new(0.675f, new(0.136298f, 0.04f)), new(0.75f, new(0.0972792f, 0.04f)), new(0.82500005f, new(0.038883f, 0.0400001f)),
                    new(0.90000004f, new(-0.03f, 0.04f)), new(0.975f, new(-0.0988831f, 0.04f)), new(1.0500001f, new(-0.157279f, 0.04f)),
                    new(1.125f, new(-0.196298f, 0.04f)), new(1.2f, new(-0.21f, 0.04f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0f)), new(0.075f, new(0.075f, 0f)), new(0.15f, new(0.05f, 0f)), new(0.22500001f, new(0.025f, 0f)),
                    new(0.3f, new(0f, 0f)), new(0.375f, new(-0.025f, 0f)), new(0.45000002f, new(-0.05f, 0f)), new(0.52500004f, new(-0.075f, 0f)),
                    new(0.6f, new(-0.1f, 0f)), new(0.675f, new(-0.107813f, 0.0140589f)), new(0.75f, new(-0.0875f, 0.048f)),
                    new(0.82500005f, new(-0.0484375f, 0.0819411f)), new(0.90000004f, new(0f, 0.096f)), new(0.975f, new(0.0484375f, 0.0819411f)),
                    new(1.0500001f, new(0.0875f, 0.048f)), new(1.125f, new(0.107813f, 0.0140589f)), new(1.2f, new(0.1f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.6f, new(0.1f, 0f)), new(LeftLeg, 0.6f, 1.2f, new(0.3f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(0.075f, new(0.025f, 0f)), new(0.15f, new(0.05f, 0f)), new(0.22500001f, new(0.075f, 0f)),
            new(0.3f, new(0.1f, 0f)), new(0.375f, new(0.125f, 0f)), new(0.45000002f, new(0.15f, 0f)), new(0.52500004f, new(0.175f, 0f)),
            new(0.6f, new(0.2f, 0f)), new(0.675f, new(0.225f, 0f)), new(0.75f, new(0.25f, 0f)), new(0.82500005f, new(0.275f, 0f)),
            new(0.90000004f, new(0.3f, 0f)), new(0.975f, new(0.325f, 0f)), new(1.0500001f, new(0.35f, 0f)), new(1.125f, new(0.375f, 0f)),
            new(1.2f, new(0.4f, 0f))),
    };

    public static SkeletonClip2D PlayerBalanceBackward { get; } = new("player-balance-backward", 1f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0.04f, -0.032f)), new(0.5f, new(0.056f, -0.04f)), new(1f, new(0.04f, -0.032f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.154507f, -0.0186303f)), new(0.125f, new(0.118563f, -0.0109047f)), new(0.25f, new(0.103557f, -0.00830227f)),
                    new(0.375f, new(0.118563f, -0.0109047f)), new(0.5f, new(0.154507f, -0.0186303f)), new(0.625f, new(0.189956f, -0.0283758f)),
                    new(0.75f, new(0.204468f, -0.032997f)), new(0.875f, new(0.189956f, -0.0283757f)), new(1f, new(0.154507f, -0.0186303f))),
                Turn: Track.Linear(
                    new(0f, -0.24f), new(0.125f, -0.183431f), new(0.25f, -0.16f), new(0.375f, -0.183431f), new(0.5f, -0.24f),
                    new(0.625f, -0.296569f), new(0.75f, -0.32f), new(0.875f, -0.296569f), new(1f, -0.24f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0379367f, -0.00189841f)), new(0.125f, new(0.0486144f, -0.00312251f)), new(0.25f, new(0.0530264f, -0.0037179f)),
                    new(0.375f, new(0.0486144f, -0.00312251f)), new(0.5f, new(0.0379367f, -0.00189841f)),
                    new(0.625f, new(0.0272286f, -0.000976771f)), new(0.75f, new(0.0227863f, -0.000683784f)),
                    new(0.875f, new(0.0272286f, -0.000976771f)), new(1f, new(0.0379367f, -0.00189841f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.33065f, 0.803688f)), new(0.125f, new(0.399704f, 0.467427f)), new(0.25f, new(0.20903f, 0.19543f)),
                    new(0.375f, new(-0.0960101f, 0.140215f)), new(0.5f, new(-0.34311f, 0.302044f)), new(0.625f, new(-0.41912f, 0.591625f)),
                    new(0.75f, new(-0.275485f, 0.870835f)), new(0.875f, new(0.0364835f, 0.97271f)), new(1f, new(0.33065f, 0.803688f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.34311f, 0.302044f)), new(0.125f, new(-0.423223f, 0.635928f)), new(0.25f, new(-0.237166f, 0.907124f)),
                    new(0.375f, new(0.0724909f, 0.963141f)), new(0.5f, new(0.33065f, 0.803688f)), new(0.625f, new(0.417568f, 0.517106f)),
                    new(0.75f, new(0.278398f, 0.239318f)), new(0.875f, new(-0.0380354f, 0.136021f)), new(1f, new(-0.34311f, 0.302044f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1f, new(0.016f, 0f)), new(RightLeg, 0f, 1f, new(0.032f, 0f))],
    };

    public static SkeletonClip2D PlayerBalanceForward { get; } = new("player-balance-forward", 1f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.048f, -0.024f)), new(0.5f, new(-0.064f, -0.032f)), new(1f, new(-0.048f, -0.024f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0778129f, -0.00467438f)), new(0.125f, new(-0.114174f, -0.010106f)), new(0.25f, new(-0.129135f, -0.0129567f)),
                    new(0.375f, new(-0.114174f, -0.010106f)), new(0.5f, new(-0.0778129f, -0.00467438f)), new(0.625f, new(-0.0412028f, -0.00130725f)),
                    new(0.75f, new(-0.0259931f, -0.000519931f)), new(0.875f, new(-0.0412028f, -0.00130725f)),
                    new(1f, new(-0.0778129f, -0.00467438f))),
                Turn: Track.Linear(
                    new(0f, 0.12f), new(0.125f, 0.176569f), new(0.25f, 0.2f), new(0.375f, 0.176569f), new(0.5f, 0.12f), new(0.625f, 0.0634314f),
                    new(0.75f, 0.04f), new(0.875f, 0.0634315f), new(1f, 0.12f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.105015f, -0.0147989f)), new(0.125f, new(0.115301f, -0.0179149f)), new(0.25f, new(0.119535f, -0.0192905f)),
                    new(0.375f, new(0.115301f, -0.0179149f)), new(0.5f, new(0.105015f, -0.0147989f)), new(0.625f, new(0.0946451f, -0.0119751f)),
                    new(0.75f, new(0.090327f, -0.0108916f)), new(0.875f, new(0.0946451f, -0.0119751f)), new(1f, new(0.105015f, -0.0147989f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.361672f, 0.666789f)), new(0.125f, new(0.176232f, 0.906963f)), new(0.25f, new(-0.133308f, 0.963621f)),
                    new(0.375f, new(-0.402973f, 0.775352f)), new(0.5f, new(-0.445614f, 0.43465f)), new(0.625f, new(-0.218582f, 0.1718f)),
                    new(0.75f, new(0.113956f, 0.157003f)), new(0.875f, new(0.342063f, 0.367959f)), new(1f, new(0.361672f, 0.666789f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.445614f, 0.43465f)), new(0.125f, new(-0.271363f, 0.196147f)), new(0.25f, new(0.0335742f, 0.140365f)),
                    new(0.375f, new(0.307842f, 0.327757f)), new(0.5f, new(0.361672f, 0.666789f)), new(0.625f, new(0.145904f, 0.928603f)),
                    new(0.75f, new(-0.181954f, 0.943157f)), new(0.875f, new(-0.41474f, 0.732444f)), new(1f, new(-0.445614f, 0.434649f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1f, new(-0.016f, 0f)), new(RightLeg, 0f, 1f, new(0f, 0f))],
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.16f, new(0.116369f, -0.0105016f)),
                    new(0.4f, new(-0.0389766f, -0.00116962f), Straight), new(0.62f, new(0.0519445f, -0.00207889f)),
                    new(0.86f, new(-0.0194971f, -0.00029248f)), new(1.4f, new(-0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.16f, -0.18f), new(0.4f, 0.06f, StraightTurn), new(0.62f, -0.08f), new(0.86f, 0.03f), new(1.4f, 0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.16f, new(-0.0379367f, -0.00189841f)),
                    new(0.4f, new(-0.0303676f, -0.00121534f), Straight), new(0.62f, new(-0.0227863f, -0.000683784f)),
                    new(0.86f, new(-0.0303676f, -0.00121534f)), new(1.4f, new(-0.0303676f, -0.00121534f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.16f, new(-0.0585464f, 0.136595f)), new(0.4f, new(-0.319442f, 1.16829f), Straight),
                    new(0.62f, new(-0.362605f, 1.04468f)), new(0.86f, new(-0.334858f, 1.15937f)), new(1.02f, new(-0.300275f, 0.978255f)),
                    new(1.18f, new(-0.334858f, 1.15937f)), new(1.4f, new(-0.335158f, 1.14938f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.052f, 0f)), new(0.16f, new(-0.052f, 0f)), new(0.4f, new(-0.048f, 0.124f), Straight), new(0.62f, new(-0.052f, 0f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675114f, 0.0676629f)), new(0.16f, new(-0.0156121f, 0.124079f)), new(0.4f, new(0.299442f, 1.13111f), Straight),
                    new(0.62f, new(0.21554f, 1.09103f)), new(0.86f, new(0.284863f, 1.14078f)), new(1.02f, new(0.239482f, 0.962057f)),
                    new(1.18f, new(0.284863f, 1.14078f)), new(1.4f, new(0.284563f, 1.13078f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.052f, 0f)), new(0.16f, new(0.052f, 0f)), new(0.4f, new(0.032f, 0.172f), Straight), new(0.62f, new(0.052f, 0f)))),
        ],
        Contacts = [
            new(LeftLeg, 0f, 0.3f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.3f, new(0.052f, 0f)), new(LeftLeg, 0.62f, 1.4f, new(-0.052f, 0f)),
            new(RightLeg, 0.62f, 1.4f, new(0.052f, 0f))],
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
                Move: Track.Of<Vector2f>(new(0f, new(0.0129991f, -0.000129998f)), new(0.8f, new(0.0129991f, -0.000129998f))),
                Turn: Track.Linear(new(0f, -0.02f), new(0.8f, -0.02f)),
                Layer: Track.Linear(new(0f, -0.35f), new(0.8f, -0.35f))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(new(0f, new(0.291651f, 0f)), new(0.8f, new(0.291651f, 0f))),
                Layer: Track.Linear(new(0f, 0.118385f), new(0.8f, 0.118385f))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(new(0f, new(0.144f, 0f)), new(0.8f, new(0.144f, 0f))),
                Layer: Track.Linear(new(0f, 0.0539231f), new(0.8f, 0.0539231f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(new(0f, new(-0.248349f, 0f)), new(0.8f, new(-0.248349f, 0f))),
                Layer: Track.Linear(new(0f, -0.193385f), new(0.8f, -0.193385f))),
            new(RightHip,
                Move: Track.Of<Vector2f>(new(0f, new(-0.144f, 0f)), new(0.8f, new(-0.144f, 0f))),
                Layer: Track.Linear(new(0f, -0.153923f), new(0.8f, -0.153923f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.128929f, 1.13699f)), new(0.2f, new(0.134329f, 0.867044f)), new(0.4f, new(0.138929f, 0.63709f)),
                    new(0.6f, new(0.212313f, 0.968623f)), new(0.8f, new(0.128929f, 1.13699f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.128f, 0.02f)), new(0.2f, new(0.176f, 0.284f)), new(0.4f, new(0.128f, 0.46f)), new(0.6f, new(0.128f, 0.24f)),
                    new(0.8f, new(0.128f, 0.02f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.220927f, 0.62269f)), new(0.2f, new(-0.307511f, 0.951025f)), new(0.4f, new(-0.230927f, 1.12259f)),
                    new(0.6f, new(-0.225527f, 0.852645f)), new(0.8f, new(-0.220927f, 0.62269f))),
                Bend: Track.Constant(1)),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.128f, 0.46f)), new(0.2f, new(-0.128f, 0.24f)), new(0.4f, new(-0.128f, 0.02f)), new(0.6f, new(-0.176f, 0.284f)),
                    new(0.8f, new(-0.128f, 0.46f))),
                Bend: Track.Constant(-1)),
        ],
        Markers = [new("view-back", 0f), new("rung-right", 0f), new("rung-left", 0.4f)],
    };

    public static SkeletonClip2D PlayerClimbOff { get; } = new("player-climb-off", 0.3f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.08f)), new(0.08000001f, new(0f, -0.056f)), new(0.15f, new(0f, -0.024f)), new(0.21000001f, new(0f, 0.008f)),
                    new(0.3f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0129991f, -0.000129998f)), new(0.15000002f, new(0.0129991f, -0.000129998f)),
                    new(0.18f, new(0.0121326f, -0.000113249f)), new(0.21000001f, new(0.0069332f, -3.69549E-05f)),
                    new(0.24000001f, new(0.00173333f, -2.32458E-06f)), new(0.27f, new(0f, 0f)), new(0.3f, new(0f, 0f))),
                Turn: Track.Linear(
                    new(0f, -0.02f), new(0.15000002f, -0.02f), new(0.18f, -0.0186667f), new(0.21000001f, -0.0106667f),
                    new(0.24000001f, -0.00266667f), new(0.27f, 0f), new(0.3f, 0f)),
                Layer: Track.Linear(
                    new(0f, -0.35f), new(0.15000002f, -0.35f), new(0.18f, -0.326667f), new(0.21000001f, -0.186667f), new(0.24000001f, -0.0466667f),
                    new(0.27f, 0f), new(0.3f, 0f))),
            new(Head, Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.15f, new(0f, 0f)), new(0.3f, new(-0.00759949f, -7.59959E-05f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.291651f, 0f)), new(0.15000002f, new(0.291651f, 0f)), new(0.18f, new(0.26592f, 0f)),
                    new(0.21000001f, new(0.213516f, 0f)), new(0.24000001f, new(0.143497f, 0f)), new(0.27f, new(0.0679725f, 0f)),
                    new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0.118385f), new(0.15000002f, 0.118385f), new(0.18f, 0.0473334f), new(0.21000001f, -0.00710976f),
                    new(0.24000001f, -0.035531f), new(0.27f, -0.0330162f), new(0.3f, 0f))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.144f, 0f)), new(0.15000002f, new(0.144f, 0f)), new(0.18f, new(0.1357f, 0f)), new(0.21000001f, new(0.112237f, 0f)),
                    new(0.24000001f, new(0.0776656f, 0f)), new(0.27f, new(0.0379653f, 0f)), new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0.0539231f), new(0.15000002f, 0.0539231f), new(0.18f, 0.00844799f), new(0.21000001f, -0.011921f),
                    new(0.24000001f, -0.0168704f), new(0.27f, -0.0154196f), new(0.3f, 0f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.248349f, 0f)), new(0.15000002f, new(-0.248349f, 0f)), new(0.18f, new(-0.242956f, 0f)),
                    new(0.21000001f, new(-0.207372f, 0f)), new(0.24000001f, new(-0.147749f, 0f)), new(0.27f, new(-0.0743973f, 0f)),
                    new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.193385f), new(0.15000002f, -0.193385f), new(0.18f, -0.118011f), new(0.21000001f, -0.0513468f),
                    new(0.24000001f, -0.00491984f), new(0.27f, 0.0132426f), new(0.3f, 0f))),
            new(RightHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.144f, 0f)), new(0.15000002f, new(-0.144f, 0f)), new(0.18f, new(-0.1357f, 0f)),
                    new(0.21000001f, new(-0.112237f, 0f)), new(0.24000001f, new(-0.0776656f, 0f)), new(0.27f, new(-0.0379653f, 0f)),
                    new(0.3f, new(0f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.153923f), new(0.15000002f, -0.153923f), new(0.18f, -0.101781f), new(0.21000001f, -0.0414123f),
                    new(0.24000001f, 0.00353706f), new(0.27f, 0.0154196f), new(0.3f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.128929f, 1.13699f)), new(0.08000001f, new(0.421309f, 1.08911f)), new(0.09166667f, new(0.556267f, 0.906693f)),
                    new(0.10333334f, new(0.609872f, 0.662799f)), new(0.11500001f, new(0.566063f, 0.416956f)),
                    new(0.12666667f, new(0.431508f, 0.206593f)), new(0.13833334f, new(0.226693f, 0.0637335f)),
                    new(0.15f, new(0.0627832f, 0.0117279f)), new(0.21000001f, new(-0.0231201f, 0.00503671f)), new(0.3f, new(0.02f, 0.05f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.128f, 0.02f)), new(0.08000001f, new(0.128f, 0.004f)), new(0.15f, new(0.128f, 0f)),
                    new(0.21000001f, new(0.112f, 0f)), new(0.3f, new(-0.052f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.220927f, 0.62269f)), new(0.08000001f, new(-0.497508f, 1.02072f)), new(0.09166667f, new(-0.626063f, 0.883044f)),
                    new(0.10333334f, new(-0.669872f, 0.637201f)), new(0.11500001f, new(-0.616267f, 0.393307f)),
                    new(0.12666667f, new(-0.473407f, 0.188492f)), new(0.13833334f, new(-0.263044f, 0.0539374f)),
                    new(0.15f, new(-0.0971849f, 0.00852811f)), new(0.21000001f, new(-0.0231201f, 0.00503671f)), new(0.3f, new(0.05f, 0.0650001f))),
                Bend: Track.Held<int>(new(0f, 1), new(0.21000001f, -1))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.128f, 0.46f)), new(0.08000001f, new(-0.128f, 0.22f)), new(0.15f, new(-0.128f, 0f)),
                    new(0.21000001f, new(-0.112f, 0f)), new(0.3f, new(0.052f, 0f))),
                Bend: Track.Held<int>(new(0f, -1), new(0.21000001f, 1))),
        ],
        Markers = [new("view-back", 0f), new("view-profile", 0.21000001f)],
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
                    new(0f, new(0f, 0f)), new(0.03f, new(0f, 0f)), new(0.06f, new(0.00173333f, -2.32458E-06f)),
                    new(0.089999996f, new(0.0069332f, -3.69549E-05f)), new(0.12f, new(0.0121326f, -0.000113249f)),
                    new(0.14999999f, new(0.0129991f, -0.000129998f)), new(0.3f, new(0.0129991f, -0.000129998f))),
                Turn: Track.Linear(
                    new(0f, 0f), new(0.03f, 0f), new(0.06f, -0.00266667f), new(0.089999996f, -0.0106667f), new(0.12f, -0.0186667f),
                    new(0.14999999f, -0.02f), new(0.3f, -0.02f)),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, 0f), new(0.06f, -0.0466667f), new(0.089999996f, -0.186667f), new(0.12f, -0.326667f),
                    new(0.14999999f, -0.35f), new(0.3f, -0.35f))),
            new(Head, Move: Track.Of<Vector2f>(new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.15f, new(0f, 0f)), new(0.3f, new(0f, 0f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(0.0679725f, 0f)), new(0.06f, new(0.143497f, 0f)), new(0.089999996f, new(0.213516f, 0f)),
                    new(0.12f, new(0.26592f, 0f)), new(0.14999999f, new(0.291651f, 0f)), new(0.3f, new(0.291651f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, -0.0330162f), new(0.06f, -0.035531f), new(0.089999996f, -0.00710976f), new(0.12f, 0.0473334f),
                    new(0.14999999f, 0.118385f), new(0.3f, 0.118385f))),
            new(LeftHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(0.0379653f, 0f)), new(0.06f, new(0.0776656f, 0f)), new(0.089999996f, new(0.112237f, 0f)),
                    new(0.12f, new(0.1357f, 0f)), new(0.14999999f, new(0.144f, 0f)), new(0.3f, new(0.144f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, -0.0154196f), new(0.06f, -0.0168704f), new(0.089999996f, -0.011921f), new(0.12f, 0.00844799f),
                    new(0.14999999f, 0.0539231f), new(0.3f, 0.0539231f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(-0.0743973f, 0f)), new(0.06f, new(-0.147749f, 0f)), new(0.089999996f, new(-0.207372f, 0f)),
                    new(0.12f, new(-0.242956f, 0f)), new(0.14999999f, new(-0.248349f, 0f)), new(0.3f, new(-0.248349f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, 0.0132426f), new(0.06f, -0.00491984f), new(0.089999996f, -0.0513468f), new(0.12f, -0.118011f),
                    new(0.14999999f, -0.193385f), new(0.3f, -0.193385f))),
            new(RightHip,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.03f, new(-0.0379653f, 0f)), new(0.06f, new(-0.0776656f, 0f)), new(0.089999996f, new(-0.112237f, 0f)),
                    new(0.12f, new(-0.1357f, 0f)), new(0.14999999f, new(-0.144f, 0f)), new(0.3f, new(-0.144f, 0f))),
                Layer: Track.Linear(
                    new(0f, 0f), new(0.03f, 0.0154196f), new(0.06f, 0.00353706f), new(0.089999996f, -0.0414123f), new(0.12f, -0.101781f),
                    new(0.14999999f, -0.153923f), new(0.3f, -0.153923f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.02f, 0.05f)), new(0.09f, new(-0.0231201f, 0.00503671f)), new(0.15f, new(0.0627832f, 0.0117279f)),
                    new(0.16166668f, new(0.226693f, 0.0637335f)), new(0.17333335f, new(0.431508f, 0.206593f)),
                    new(0.185f, new(0.566063f, 0.416956f)), new(0.19666667f, new(0.609872f, 0.662799f)), new(0.20833334f, new(0.556267f, 0.906693f)),
                    new(0.22f, new(0.421309f, 1.08911f)), new(0.3f, new(0.128929f, 1.13699f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.052f, 0f)), new(0.09f, new(0.112f, 0f)), new(0.15f, new(0.128f, 0f)), new(0.22f, new(0.128f, 0.004f)),
                    new(0.3f, new(0.128f, 0.02f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.05f, 0.0650001f)), new(0.09f, new(-0.0231201f, 0.00503671f)), new(0.15f, new(-0.0971849f, 0.00852811f)),
                    new(0.16166668f, new(-0.263044f, 0.0539374f)), new(0.17333335f, new(-0.473407f, 0.188492f)),
                    new(0.185f, new(-0.616267f, 0.393307f)), new(0.19666667f, new(-0.669872f, 0.637201f)),
                    new(0.20833334f, new(-0.626063f, 0.883044f)), new(0.22f, new(-0.497508f, 1.02072f)), new(0.3f, new(-0.220927f, 0.62269f))),
                Bend: Track.Held<int>(new(0f, -1), new(0.09f, 1))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.052f, 0f)), new(0.09f, new(-0.112f, 0f)), new(0.15f, new(-0.128f, 0f)), new(0.22f, new(-0.128f, 0.22f)),
                    new(0.3f, new(-0.128f, 0.46f))),
                Bend: Track.Held<int>(new(0f, 1), new(0.09f, -1))),
        ],
        Markers = [new("view-profile", 0f), new("view-back", 0.09f)],
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
                    new(0f, new(0.222884f, -0.0394077f)), new(0.04f, new(0.4286f, -0.161326f)), new(0.16f, new(0.408715f, -0.144578f)),
                    new(0.28f, new(0.129135f, -0.0129567f))),
                Turn: new(SmoothTurn, new(0f, -0.35f), new(0.04f, -0.72f), new(0.16f, -0.68f), new(0.28f, -0.2f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0680312f, -0.0061394f)), new(0.04f, new(-0.14095f, -0.0271074f)), new(0.16f, new(-0.133864f, -0.0243592f)),
                    new(0.28f, new(-0.0379367f, -0.00189841f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.0933943f, 0.0945892f)), new(0.04f, new(-0.179864f, 0.0929176f)), new(0.16f, new(-0.242724f, 0.117883f)),
                    new(0.28f, new(-0.0167518f, 0.0812958f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.152f, 0.028f)), new(0.04f, new(-0.4f, 0.076f)), new(0.16f, new(-0.384f, 0.06f)), new(0.28f, new(-0.112f, 0.012f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(-0.0276382f, 0.118592f)), new(0.04f, new(-0.161131f, 0.162553f)), new(0.16f, new(-0.214934f, 0.178938f)),
                    new(0.28f, new(0.100856f, 0.105136f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.072f, 0.02f)), new(0.04f, new(0.192f, 0.028f)), new(0.16f, new(0.2f, 0.028f)), new(0.28f, new(0.072f, 0.004f)))),
        ],
        Markers = [new("dash-end", 0.16f)],
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.07f, new(-0.253122f, -0.0513104f), Straight),
                    new(0.34f, new(-0.192088f, -0.0290313f)), new(0.68f, new(-0.141849f, -0.0156667f), Straight),
                    new(0.92f, new(-0.0519445f, -0.00207889f), Straight), new(1.06f, new(-0.0778129f, -0.00467438f)),
                    new(1.3f, new(-0.0389766f, -0.00116962f)), new(1.6f, new(-0.0389766f, -0.00116962f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.07f, 0.4f, StraightTurn), new(0.34f, 0.3f), new(0.68f, 0.22f, StraightTurn),
                    new(0.92f, 0.08f, StraightTurn), new(1.06f, 0.12f), new(1.3f, 0.06f), new(1.6f, 0.06f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.07f, new(-0.119535f, -0.0192905f), Straight),
                    new(0.34f, new(-0.0754943f, -0.00757471f)), new(0.68f, new(-0.0530264f, -0.0037179f), Straight),
                    new(0.92f, new(-0.0454906f, -0.00273272f), Straight), new(1.06f, new(-0.0754943f, -0.00757471f)),
                    new(1.3f, new(-0.0379367f, -0.00189841f)), new(1.6f, new(-0.0379367f, -0.00189841f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.07f, new(0.186856f, 0.341174f), Straight), new(0.34f, new(0.173006f, 0.614102f)),
                    new(0.68f, new(0.281173f, 0.706317f), Straight), new(0.92f, new(0.0973509f, 0.430932f), Straight),
                    new(1.06f, new(-0.0250291f, 0.453389f)), new(1.3f, new(-0.0428767f, 0.49868f)), new(1.6f, new(-0.0428767f, 0.49868f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.052f, 0f)), new(0.07f, new(-0.052f, 0f), Straight), new(0.34f, new(-0.256f, 0f)),
                    new(0.68f, new(-0.032f, 0.012f), Straight), new(0.92f, new(0.256f, 0.044f), Straight), new(1.06f, new(0.272f, 0.06f)),
                    new(1.3f, new(0.288f, 0.012f)), new(1.6f, new(0.288f, 0.012f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675114f, 0.0676629f)), new(0.07f, new(0.265485f, 0.373073f), Straight), new(0.34f, new(0.266441f, 0.627136f)),
                    new(0.68f, new(0.335647f, 0.565135f), Straight), new(0.92f, new(0.0474473f, 0.186034f), Straight),
                    new(1.06f, new(-0.0469881f, 0.203944f)), new(1.3f, new(-0.22899f, 0.138593f)), new(1.6f, new(-0.22899f, 0.138593f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.052f, 0f)), new(0.07f, new(0.052f, 0f), Straight), new(0.34f, new(-0.032f, 0.028f)),
                    new(0.68f, new(0.128f, 0.14f), Straight), new(0.92f, new(0.216f, 0.092f), Straight), new(1.06f, new(0.224f, 0.116f)),
                    new(1.3f, new(0.24f, 0.036f)), new(1.6f, new(0.24f, 0.036f)))),
        ],
        Markers = [new("impact", 0.92f)],
    };

    public static SkeletonClip2D PlayerFall { get; } = new("player-fall", 0.6f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0f, 0.016f)), new(0.3f, new(0f, 0.024f)), new(0.6f, new(0f, 0.016f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(-0.0389766f, -0.00116962f)), new(0.3f, new(-0.0519445f, -0.00207889f)), new(0.6f, new(-0.0389766f, -0.00116962f))),
                Turn: new(SmoothTurn, new(0f, 0.06f), new(0.3f, 0.08f), new(0.6f, 0.06f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(0.0379367f, -0.00189841f)), new(0.3f, new(0.049261f, -0.00320649f)), new(0.6f, new(0.0379367f, -0.00189841f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth, new(0f, new(-0.339406f, 1.16949f)), new(0.3f, new(-0.285887f, 1.21225f)), new(0.6f, new(-0.339406f, 1.16949f)))),
            new(LeftLeg, Target: new(Smooth, new(0f, new(-0.048f, 0.076f)), new(0.3f, new(-0.032f, 0.116f)), new(0.6f, new(-0.048f, 0.076f)))),
            new(RightArm,
                Target: new(Smooth, new(0f, new(0.396864f, 1.08519f)), new(0.3f, new(0.368806f, 1.11963f)), new(0.6f, new(0.396864f, 1.08519f)))),
            new(RightLeg, Target: new(Smooth, new(0f, new(0.072f, 0.188f)), new(0.3f, new(0.056f, 0.148f)), new(0.6f, new(0.072f, 0.188f)))),
        ],
    };

    public static SkeletonClip2D PlayerGunAim { get; } = new("player-gun-aim", 2f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.016f, 0.0096f)), new(1f, new(-0.016f, 0.0032f)), new(2f, new(-0.016f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth, new(0f, new(0f, 0f)), new(1f, new(-0.00974963f, -7.31349E-05f)), new(2f, new(0f, 0f))),
                Turn: new(SmoothTurn, new(0f, 0f), new(1f, 0.015f), new(2f, 0f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(1f, new(-0.00379994f, -1.90139E-05f)), new(2f, new(-0.00759949f, -7.59959E-05f)))),
            new(RightShoulder, Turn: new(SmoothTurn, new(0f, 0f), new(1f, -0.005f), new(2f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(-0.01f, 0.05f)), new(1f, new(-0.0291508f, 0.0399187f)), new(2f, new(-0.01f, 0.05f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0.615f, 0.63f)), new(1f, new(0.614668f, 0.613552f)), new(2f, new(0.615f, 0.63f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(new(0f, new(-0.20944f, 0.174533f, 0f)), new(2f, new(-0.20944f, 0.174533f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 2f, new(-0.052f, 0f)), new(RightLeg, 0f, 2f, new(0.052f, 0f))],
    };

    public static SkeletonClip2D PlayerGunCharge { get; } = new("player-gun-charge", 1f)
    {
        Joints =
        [
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.45f, new(0.0113983f, -0.000170976f)), new(1f, new(0.0227863f, -0.000683784f)))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.01f, 0.15f)), new(0.45f, new(0.32f, 0.53f)), new(1f, new(0.41f, 0.59f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0.615f, 0.63f)), new(0.45f, new(0.615f, 0.63f)), new(1f, new(0.615f, 0.63f)))),
        ],
    };

    public static SkeletonClip2D PlayerGunShot { get; } = new("player-gun-shot", 0.2f)
    {
        Joints =
        [
            new(Chest,
                Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.03f, new(-0.0389766f, -0.00116962f), Smooth), new(0.2f, new(0f, 0f))),
                Turn: Track.Linear(new(0f, 0f), new(0.03f, 0.06f, SmoothTurn), new(0.2f, 0f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.03f, new(-0.0189921f, -0.0004749f), Smooth),
                    new(0.2f, new(-0.00759949f, -7.59959E-05f)))),
            new(RightShoulder, Turn: Track.Linear(new(0f, 0f), new(0.03f, 0.32f, SmoothTurn), new(0.2f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.01f, 0.05f)), new(0.03f, new(-0.0559964f, 0.0504801f), Smooth), new(0.2f, new(-0.01f, 0.05f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.615f, 0.63f)), new(0.03f, new(0.471479f, 0.449703f), Smooth), new(0.2f, new(0.615f, 0.63f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(
                new(0f, new(-0.20944f, 0.174533f, 0f)), new(0.03f, new(-0.453786f, 0.244346f, 0f)), new(0.2f, new(-0.20944f, 0.174533f, 0f)))),
        ],
        Markers = [new("fire", 0f)],
    };

    public static SkeletonClip2D PlayerGunWallShot { get; } = new("player-gun-wall-shot", 0.3f)
    {
        Joints =
        [
            new(Hips, Move: Track.Of<Vector2f>(new(0f, new(-0.08f, -0.16f)), new(0.3f, new(-0.08f, -0.16f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0519445f, -0.00207889f)), new(0.03f, new(-0.0842622f, -0.00548476f), Smooth),
                    new(0.3f, new(-0.0519445f, -0.00207889f))),
                Turn: Track.Linear(new(0f, 0.08f), new(0.03f, 0.13f, SmoothTurn), new(0.3f, 0.08f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.00759949f, -7.59959E-05f)), new(0.03f, new(-0.00759949f, -7.59959E-05f), Smooth),
                    new(0.3f, new(0.00759949f, -7.59959E-05f)))),
            new(RightShoulder, Turn: Track.Linear(new(0f, -0.08f), new(0.03f, 0.27f, SmoothTurn), new(0.3f, -0.08f))),
        ],
        Limbs =
        [
            new(LeftArm, Target: Track.Of<Vector2f>(new(0f, new(-0.0877204f, 0.595183f)), new(0.3f, new(-0.0877204f, 0.595183f)))),
            new(LeftLeg, Target: Track.Of<Vector2f>(new(0f, new(-0.192f, 0.284f)), new(0.3f, new(-0.192f, 0.284f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.615f, 0.63f)), new(0.03f, new(0.452846f, 0.456713f), Smooth), new(0.3f, new(0.615f, 0.63f)))),
            new(RightLeg, Target: Track.Of<Vector2f>(new(0f, new(-0.192f, 0.028f)), new(0.3f, new(-0.192f, 0.028f)))),
        ],
        Mounts =
        [
            new(GunHand, Track.Of<Vector3f>(
                new(0f, new(-0.20944f, 0.174533f, 0f)), new(0.03f, new(-0.453786f, 0.244346f, 0f)), new(0.3f, new(-0.20944f, 0.174533f, 0f)))),
        ],
        Markers = [new("fire", 0f)],
    };

    public static SkeletonClip2D PlayerHealGather { get; } = new("player-heal-gather", 1f)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(0f, 0.0096f)), new(0.25f, new(0f, -0.0224f)), new(1f, new(0f, -0.0464f)))),
            new(Chest,
                Move: new(Smooth, new(0f, new(0f, 0f)), new(0.25f, new(0.0648917f, -0.00324726f)), new(1f, new(0.090703f, -0.00635958f))),
                Turn: new(SmoothTurn, new(0f, 0f), new(0.25f, -0.1f), new(1f, -0.14f))),
            new(Head, Move: new(Smooth, new(0f, new(0f, 0f)), new(0.25f, new(0.0605409f, -0.00485364f)), new(1f, new(0.0754943f, -0.00757471f)))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.15f, 0.3f)), new(0.25f, new(0.224774f, 0.514759f)), new(1f, new(0.217286f, 0.543465f)))),
            new(RightArm, Target: new(Smooth, new(0f, new(0.09f, 0.3f)), new(0.25f, new(0.188968f, 0.470966f)), new(1f, new(0.183259f, 0.498274f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1f, new(-0.052f, 0f)), new(RightLeg, 0f, 1f, new(0.052f, 0f))],
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.05f, new(-0.228978f, -0.041667f), Straight),
                    new(0.18f, new(-0.090703f, -0.00635958f)), new(0.35f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn, new(0f, -0.03f), new(0.05f, 0.36f, StraightTurn), new(0.18f, 0.14f), new(0.35f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.05f, new(-0.112298f, -0.0169721f), Straight),
                    new(0.18f, new(-0.0379367f, -0.00189841f)), new(0.35f, new(-0.00759949f, -7.59959E-05f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.05f, new(0.173269f, 0.33842f), Straight), new(0.18f, new(0.0896308f, 0.208992f)),
                    new(0.35f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675114f, 0.0676629f)), new(0.05f, new(0.250559f, 0.373438f), Straight), new(0.18f, new(0.134821f, 0.243018f)),
                    new(0.35f, new(0.0675114f, 0.0676629f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.35f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.35f, new(0.052f, 0f))],
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(1.2f, new(0.00324999f, -8.10623E-06f)), new(2.4f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn, new(0f, -0.03f), new(1.2f, -0.005f), new(2.4f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(1.2f, new(0.00379994f, -1.90139E-05f)),
                    new(2.4f, new(-0.00759949f, -7.59959E-05f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.35f, new(0.0348992f, 0.0514283f)), new(1.55f, new(0.0112339f, 0.0353865f)),
                    new(2.4f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0675114f, 0.0676629f)), new(0.35f, new(0.0645171f, 0.0671695f)), new(1.55f, new(0.0410803f, 0.0506899f)),
                    new(2.4f, new(0.0675114f, 0.0676629f)))),
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
                    new(0f, new(0.141849f, -0.0156667f)), new(0.08f, new(0.0259931f, -0.000519931f), Straight),
                    new(0.3f, new(0.0648917f, -0.00324726f)), new(0.5f, new(0.0519445f, -0.00207889f))),
                Turn: new(SmoothTurn, new(0f, -0.22f), new(0.08f, -0.04f, StraightTurn), new(0.3f, -0.1f), new(0.5f, -0.08f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0454906f, -0.00273272f)), new(0.08f, new(-0.00759949f, -7.59959E-05f), Straight),
                    new(0.3f, new(-0.0227863f, -0.000683784f)), new(0.5f, new(-0.0189921f, -0.0004749f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.198501f, 0.120465f)), new(0.08f, new(0.0819061f, 0.854639f), Straight), new(0.3f, new(0.169067f, 0.468969f)),
                    new(0.5f, new(0.13033f, 0.40202f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.112f, 0.028f)), new(0.08f, new(-0.064f, -0.004f), Straight), new(0.3f, new(-0.096f, 0.348f)),
                    new(0.5f, new(-0.072f, 0.284f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(-0.135583f, 0.114041f)), new(0.08f, new(0.161042f, 0.877822f), Straight), new(0.3f, new(0.224774f, 0.514759f)),
                    new(0.5f, new(0.186942f, 0.446687f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0f, 0.004f)), new(0.08f, new(-0.032f, 0.06f), Straight), new(0.3f, new(0.048f, 0.396f)),
                    new(0.5f, new(0.032f, 0.332f)))),
        ],
        Markers = [new("launch", 0f), new("apex", 0.3f)],
    };

    public static SkeletonClip2D PlayerLand { get; } = new("player-land", 0.36f)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.016f, -0.144f)), new(0.09f, new(-0.016f, -0.192f)), new(0.36f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.179631f, -0.025314f)), new(0.09f, new(0.216767f, -0.0372095f)), new(0.36f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn, new(0f, -0.28f), new(0.09f, -0.34f), new(0.36f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0605409f, -0.00485364f)), new(0.09f, new(-0.0754943f, -0.00757471f)),
                    new(0.36f, new(-0.00759949f, -7.59959E-05f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.32225f, 0.293461f)), new(0.09f, new(0.348511f, 0.359605f)), new(0.36f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.368859f, 0.348485f)), new(0.09f, new(0.391737f, 0.417324f)), new(0.36f, new(0.0675114f, 0.0676629f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.36f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.36f, new(0.052f, 0f))],
        Markers = [new("impact", 0f)],
    };

    public static SkeletonClip2D PlayerSwordBackhand { get; } = new("player-sword-backhand", 0.25f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.048f, -0.128f)), new(0.0333333f, new(0.016f, -0.064f)), new(0.099999964f, new(0.016f, -0.064f), Straight),
                    new(0.116666615f, new(0f, -0.064f), Straight), new(0.13333333f, new(0.048f, -0.104f), Straight),
                    new(0.16666669f, new(0.04f, -0.128f)), new(0.23333335f, new(0.032f, -0.112f)), new(0.25f, new(0.032f, -0.112f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.23505f, -0.0439872f)), new(0.0333333f, new(0.0324865f, -0.000812352f)),
                    new(0.099999964f, new(0.0324865f, -0.000812352f), Straight), new(0.116666615f, new(-0.0129991f, -0.000129998f), Straight),
                    new(0.13333333f, new(0.192088f, -0.0290313f), Straight), new(0.16666669f, new(0.222884f, -0.0394077f)),
                    new(0.23333335f, new(0.210628f, -0.0350724f)), new(0.25f, new(0.210628f, -0.0350724f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.37f), new(0.0333333f, -0.05f), new(0.099999964f, -0.05f, StraightTurn), new(0.116666615f, 0.02f, StraightTurn),
                    new(0.13333333f, -0.3f, StraightTurn), new(0.16666669f, -0.35f), new(0.23333335f, -0.33f), new(0.25f, -0.33f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0680312f, -0.0061394f)), new(0.0333333f, new(-0.0379367f, -0.00189841f)),
                    new(0.099999964f, new(-0.0379367f, -0.00189841f), Straight), new(0.116666615f, new(-0.0303676f, -0.00121534f), Straight),
                    new(0.13333333f, new(-0.0680312f, -0.0061394f), Straight), new(0.16666669f, new(-0.0680312f, -0.0061394f)),
                    new(0.23333335f, new(-0.0605409f, -0.00485364f)), new(0.25f, new(-0.0605409f, -0.00485364f)))),
            new(LeftShoulder,
                Move: new(Smooth,
                    new(0f, new(-0.069956f, 0f)), new(0.0333333f, new(-0.0423071f, 0f)), new(0.099999964f, new(-0.0423071f, 0f), Straight),
                    new(0.116666615f, new(-0.0620502f, 0f), Straight), new(0.13333333f, new(0f, 0f), Straight),
                    new(0.16666669f, new(0.0866506f, 0f)), new(0.23333335f, new(0.102434f, 0f)), new(0.25f, new(0.102434f, 0f))),
                Layer: new(SmoothTurn,
                    new(0f, 0.136748f), new(0.0333333f, 0.0468339f), new(0.099999964f, 0.0468339f, StraightTurn),
                    new(0.116666615f, 0.089971f, StraightTurn), new(0.13333333f, 0f, StraightTurn), new(0.16666669f, -0.0366154f),
                    new(0.23333335f, -0.0381094f), new(0.25f, -0.0381094f))),
            new(RightShoulder,
                Move: new(Smooth,
                    new(0f, new(0.104575f, 0f)), new(0.0333333f, new(0.0534689f, 0f)), new(0.099999964f, new(0.0534689f, 0f), Straight),
                    new(0.116666615f, new(0.0842206f, 0f), Straight), new(0.13333333f, new(0f, 0f), Straight),
                    new(0.16666669f, new(-0.0933494f, 0f)), new(0.23333335f, new(-0.108942f, 0f)), new(0.25f, new(-0.108942f, 0f))),
                Turn: new(SmoothTurn,
                    new(0f, -5.75231f), new(0.0333333f, -3.8612f), new(0.099999964f, -3.8612f, StraightTurn),
                    new(0.116666615f, -3.9312f, StraightTurn), new(0.13333333f, -5.56383f, StraightTurn), new(0.16666669f, -5.7477f),
                    new(0.23333335f, -5.70653f), new(0.25f, -5.70653f)),
                Layer: new(SmoothTurn,
                    new(0f, -0.112508f), new(0.0333333f, -0.0335317f), new(0.099999964f, -0.0335317f, StraightTurn),
                    new(0.116666615f, -0.0696556f, StraightTurn), new(0.13333333f, 0f, StraightTurn), new(0.16666669f, 0.0116154f),
                    new(0.23333335f, 0.00875157f), new(0.25f, 0.00875157f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.0910316f, 0.143665f)), new(0.0333333f, new(-0.186785f, 0.181579f)),
                    new(0.099999964f, new(-0.186785f, 0.181579f), Straight), new(0.116666615f, new(-0.239159f, 0.194092f), Straight),
                    new(0.13333333f, new(0.153706f, 0.225321f), Straight), new(0.16666669f, new(0.240458f, 0.259036f)),
                    new(0.23333335f, new(0.213664f, 0.247225f)), new(0.25f, new(0.213664f, 0.247225f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.220106f, 0.0471136f)), new(0.0333333f, new(0.10958f, 0.0387381f)),
                    new(0.099999964f, new(0.165415f, 0.0665041f), Straight), new(0.116666615f, new(0.362913f, 0.507991f), Straight),
                    new(0.13333333f, new(0.171954f, 0.0274494f), Straight), new(0.16666669f, new(0.18007f, 0.0338861f)),
                    new(0.23333335f, new(0.188976f, 0.0353374f)), new(0.25f, new(0.188976f, 0.0353374f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.349066f, 0.517989f, 0f)), new(0.0333333f, new(0.174533f, 0.393066f, 0f)),
                new(0.099999964f, new(0.174533f, 0.393066f, 0f)), new(0.116666615f, new(0.698132f, 0.393066f, 0f)),
                new(0.13333333f, new(1.309f, 0.402636f, 0f)), new(0.16666669f, new(0.959931f, -0.343542f, 0f)),
                new(0.23333335f, new(0.349066f, -0.135453f, 0f)), new(0.25f, new(0.349066f, -0.135453f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.25f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.25f, new(0.08f, 0f))],
        Markers = [
            new("sword-draw", 0f), new("swoosh", 0.116666615f), new("strike", 0.13333333f), new("swoosh-end", 0.14999998f),
            new("recover", 0.21666664f)],
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
                    new(0f, new(0f, 0f), Straight), new(0.04f, new(0.129135f, -0.0129567f)), new(0.18f, new(0.141849f, -0.0156667f)),
                    new(0.25f, new(0.0778129f, -0.00467438f))),
                Turn: new(SmoothTurn, new(0f, 0f, StraightTurn), new(0.04f, -0.2f), new(0.18f, -0.22f), new(0.25f, -0.12f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(0f, 0f), Straight), new(0.04f, new(0.0605409f, -0.00485364f)), new(0.18f, new(0.0680312f, -0.0061394f)),
                    new(0.25f, new(0.0303676f, -0.00121534f)))),
            new(RightShoulder,
                Turn: new(SmoothTurn, new(0f, -1.6908f, StraightTurn), new(0.04f, -1.3708f), new(0.18f, -1.3508f), new(0.25f, -1.13f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.07f, 0.25f), Straight), new(0.04f, new(0.198993f, 0.20582f)), new(0.18f, new(0.200818f, 0.188624f)),
                    new(0.25f, new(0.0894246f, 0.160778f)))),
            new(LeftLeg,
                Target: new(Smooth,
                    new(0f, new(-0.064f, 0.268f), Straight), new(0.04f, new(-0.096f, 0.396f)), new(0.18f, new(-0.096f, 0.412f)),
                    new(0.25f, new(-0.072f, 0.316f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(-0.0098464f, 0.482861f), Straight), new(0.04f, new(0.495059f, 0.629772f)), new(0.18f, new(0.511346f, 0.618907f)),
                    new(0.25f, new(0.434009f, 0.563164f)))),
            new(RightLeg,
                Target: new(Smooth,
                    new(0f, new(0.032f, 0.316f), Straight), new(0.04f, new(0.016f, 0.428f)), new(0.18f, new(0.016f, 0.444f)),
                    new(0.25f, new(0.032f, 0.348f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(1.13446f, 0.261799f, 0f)), new(0.04f, new(0.261799f, 0f, 0f)), new(0.18f, new(0.261799f, 0f, 0f)),
                new(0.25f, new(0.523599f, 0f, 0f)))),
        ],
        Markers = [new("sword-draw", 0f), new("strike", 0.02f), new("recover", 0.083f)],
    };

    public static SkeletonClip2D PlayerSwordForehand { get; } = new("player-sword-forehand", 0.25f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.032f, -0.112f), Smooth), new(0.0333333f, new(-0.016f, -0.048f), Smooth), new(0.08333331f, new(-0.016f, -0.048f)),
                    new(0.099999964f, new(-0.016f, -0.048f)), new(0.116666675f, new(0.008f, -0.064f)), new(0.13333333f, new(0.048f, -0.088f)),
                    new(0.16666669f, new(0.048f, -0.136f), Smooth), new(0.23333335f, new(0.048f, -0.128f), Smooth), new(0.25f, new(0.048f, -0.128f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.210628f, -0.0350724f), Smooth), new(0.0333333f, new(-0.0324865f, -0.000812352f), Smooth),
                    new(0.08333331f, new(-0.0324865f, -0.000812352f)), new(0.099999964f, new(-0.0324865f, -0.000812352f)),
                    new(0.116666675f, new(0.0648917f, -0.00324726f)), new(0.13333333f, new(0.204468f, -0.032997f)),
                    new(0.16666669f, new(0.253122f, -0.0513104f), Smooth), new(0.23333335f, new(0.23505f, -0.0439872f), Smooth),
                    new(0.25f, new(0.23505f, -0.0439872f))),
                Turn: Track.Linear(
                    new(0f, -0.33f, SmoothTurn), new(0.0333333f, 0.05f, SmoothTurn), new(0.08333331f, 0.05f), new(0.099999964f, 0.05f),
                    new(0.116666675f, -0.1f), new(0.13333333f, -0.32f), new(0.16666669f, -0.4f, SmoothTurn), new(0.23333335f, -0.37f, SmoothTurn),
                    new(0.25f, -0.37f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0605409f, -0.00485364f), Smooth), new(0.0333333f, new(-0.0227863f, -0.000683784f), Smooth),
                    new(0.08333331f, new(-0.0227863f, -0.000683784f)), new(0.099999964f, new(-0.0227863f, -0.000683784f)),
                    new(0.116666675f, new(-0.0379367f, -0.00189841f)), new(0.13333333f, new(-0.0680312f, -0.0061394f)),
                    new(0.16666669f, new(-0.0754943f, -0.00757471f), Smooth), new(0.23333335f, new(-0.0680312f, -0.0061394f), Smooth),
                    new(0.25f, new(-0.0680312f, -0.0061394f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.102434f, 0f), Smooth), new(0.0333333f, new(0.118287f, 0f), Smooth), new(0.08333331f, new(0.118287f, 0f)),
                    new(0.099999964f, new(0.118287f, 0f)), new(0.116666675f, new(0.0557738f, 0f)), new(0.13333333f, new(-0.0660445f, 0f)),
                    new(0.16666669f, new(-0.0698433f, 0f), Smooth), new(0.23333335f, new(-0.069956f, 0f), Smooth), new(0.25f, new(-0.069956f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.0381094f, SmoothTurn), new(0.0333333f, -0.038222f, SmoothTurn), new(0.08333331f, -0.038222f),
                    new(0.099999964f, -0.038222f), new(0.116666675f, -0.0295396f), new(0.13333333f, 0.105313f),
                    new(0.16666669f, 0.152601f, SmoothTurn), new(0.23333335f, 0.136748f, SmoothTurn), new(0.25f, 0.136748f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.108942f, 0f), Smooth), new(0.0333333f, new(-0.124226f, 0f), Smooth), new(0.08333331f, new(-0.124226f, 0f)),
                    new(0.099999964f, new(-0.124226f, 0f)), new(0.116666675f, new(-0.0617129f, 0f)), new(0.13333333f, new(0.0922448f, 0f)),
                    new(0.16666669f, new(0.108787f, 0f), Smooth), new(0.23333335f, new(0.104575f, 0f), Smooth), new(0.25f, new(0.104575f, 0f))),
                Turn: Track.Linear(
                    new(0f, -5.70653f, SmoothTurn), new(0.0333333f, -3.99432f, SmoothTurn), new(0.08333331f, -3.99432f),
                    new(0.099999964f, -3.99432f), new(0.116666675f, -3.79214f), new(0.13333333f, -5.74073f), new(0.16666669f, -5.81488f, SmoothTurn),
                    new(0.23333335f, -5.75231f, SmoothTurn), new(0.25f, -5.75231f)),
                Layer: Track.Linear(
                    new(0f, 0.00875157f, SmoothTurn), new(0.0333333f, 0.00453962f, SmoothTurn), new(0.08333331f, 0.00453962f),
                    new(0.099999964f, 0.00453962f), new(0.116666675f, 0.013222f), new(0.13333333f, -0.0833286f),
                    new(0.16666669f, -0.127792f, SmoothTurn), new(0.23333335f, -0.112508f, SmoothTurn), new(0.25f, -0.112508f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.213664f, 0.247225f), Smooth), new(0.0333333f, new(0.127784f, 0.201554f), Smooth),
                    new(0.08333331f, new(0.127784f, 0.201554f)), new(0.099999964f, new(0.127784f, 0.201554f)),
                    new(0.116666675f, new(0.0656736f, 0.19729f)), new(0.13333333f, new(-0.0941312f, 0.144147f)),
                    new(0.16666669f, new(-0.0889212f, 0.147379f), Smooth), new(0.23333335f, new(-0.0910316f, 0.143665f), Smooth),
                    new(0.25f, new(-0.0910316f, 0.143665f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.188976f, 0.0353374f), Smooth), new(0.0333333f, new(0.546399f, 0.775706f), Smooth),
                    new(0.08333331f, new(0.546399f, 0.775706f)), new(0.099999964f, new(0.546399f, 0.775706f)),
                    new(0.116666675f, new(-0.0738933f, 0.531065f)), new(0.13333333f, new(0.269717f, 0.0681713f)),
                    new(0.16666669f, new(0.209834f, 0.0439671f), Smooth), new(0.23333335f, new(0.220106f, 0.0471136f), Smooth),
                    new(0.25f, new(0.220106f, 0.0471136f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.349066f, -0.135453f, 0f)), new(0.0333333f, new(0.174533f, -0.18405f, 0f)),
                new(0.08333331f, new(0.523599f, -0.18405f, 0f)), new(0.099999964f, new(0.523599f, -0.18405f, 0f)),
                new(0.116666675f, new(1.22173f, -0.902511f, 0f)), new(0.13333333f, new(1.309f, -0.341124f, 0f)),
                new(0.16666669f, new(0.959931f, 0.696982f, 0f)), new(0.23333335f, new(0.349066f, 0.517989f, 0f)),
                new(0.25f, new(0.349066f, 0.517989f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.25f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.25f, new(0.08f, 0f))],
        Markers = [
            new("sword-draw", 0f), new("swoosh", 0.099999964f), new("strike", 0.13333333f), new("swoosh-end", 0.14999998f),
            new("recover", 0.21666664f)],
    };

    public static SkeletonClip2D PlayerSwordPutAway { get; } = new("player-sword-put-away", 0.4833333f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.048f, -0.128f)), new(0.18333334f, new(0.048f, -0.128f)), new(0.26333332f, new(0.008f, -0.04f), Straight),
                    new(0.33333337f, new(-0.008f, 0.0096f), Straight), new(0.4033333f, new(-0.008f, 0.0096f)),
                    new(0.4833333f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.23505f, -0.0439872f)), new(0.18333334f, new(0.23505f, -0.0439872f)),
                    new(0.26333332f, new(0.0129991f, -0.000129998f), Straight), new(0.33333337f, new(-0.0259931f, -0.000519931f), Straight),
                    new(0.4033333f, new(-0.0129991f, -0.000129998f)), new(0.4833333f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.37f), new(0.18333334f, -0.37f), new(0.26333332f, -0.02f, StraightTurn), new(0.33333337f, 0.04f, StraightTurn),
                    new(0.4033333f, 0.02f), new(0.4833333f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0680312f, -0.0061394f)), new(0.18333334f, new(-0.0680312f, -0.0061394f)),
                    new(0.26333332f, new(-0.0227863f, -0.000683784f), Straight), new(0.33333337f, new(-0.0151959f, -0.000303954f), Straight),
                    new(0.4033333f, new(-0.00759949f, -7.59959E-05f)), new(0.4833333f, new(-0.00759949f, -7.59959E-05f)))),
            new(LeftShoulder,
                Move: new(Smooth,
                    new(0f, new(-0.069956f, 0f)), new(0.18333334f, new(-0.069956f, 0f)), new(0.26333332f, new(0.0265947f, 0f), Straight),
                    new(0.33333337f, new(0.0866506f, 0f), Straight), new(0.4033333f, new(0.0557738f, 0f)), new(0.4833333f, new(0f, 0f))),
                Layer: new(SmoothTurn,
                    new(0f, 0.136748f), new(0.18333334f, 0.136748f), new(0.26333332f, -0.0172096f, StraightTurn),
                    new(0.33333337f, -0.0366154f, StraightTurn), new(0.4033333f, -0.0295396f), new(0.4833333f, 0f))),
            new(RightShoulder,
                Move: new(Smooth,
                    new(0f, new(0.104575f, 0f)), new(0.18333334f, new(0.104575f, 0f)), new(0.26333332f, new(-0.0302781f, 0f), Straight),
                    new(0.33333337f, new(-0.0933494f, 0f), Straight), new(0.4033333f, new(-0.0617129f, 0f)), new(0.4833333f, new(0f, 0f))),
                Turn: new(SmoothTurn,
                    new(0f, -5.75231f), new(0.18333334f, -5.75231f), new(0.26333332f, -4.66319f, StraightTurn),
                    new(0.33333337f, -2.82319f, StraightTurn), new(0.4033333f, -1.6908f), new(0.4833333f, -1.6408f)),
                Layer: new(SmoothTurn,
                    new(0f, -0.112508f), new(0.18333334f, -0.112508f), new(0.26333332f, 0.0093106f, StraightTurn),
                    new(0.33333337f, 0.0116154f, StraightTurn), new(0.4033333f, 0.013222f), new(0.4833333f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(-0.0910316f, 0.143665f)), new(0.18333334f, new(-0.0910316f, 0.143665f)),
                    new(0.26333332f, new(-0.0184008f, 0.070116f), Straight), new(0.33333337f, new(-0.0140256f, 0.0488804f), Straight),
                    new(0.4033333f, new(0.0079908f, 0.0491201f)), new(0.4833333f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.220106f, 0.0471136f)), new(0.18333334f, new(0.220106f, 0.0471136f)),
                    new(0.26333332f, new(0.448319f, 0.555456f), Straight), new(0.33333337f, new(-0.153944f, 0.141724f), Straight),
                    new(0.4033333f, new(-0.0172341f, 0.54413f)), new(0.4833333f, new(0.544091f, 0.788003f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.349066f, 0.517989f, 0f)), new(0.18333334f, new(0.349066f, 0.517989f, 0f)), new(0.26333332f, new(0f, 0f, 0f)),
                new(0.33333337f, new(0f, 0f, 0f)), new(0.4033333f, new(0f, 0f, 0f)), new(0.4833333f, new(0f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.4833333f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.4833333f, new(0.08f, 0f))],
        Markers = [new("sword-sheathe", 0.4033333f)],
    };

    public static SkeletonClip2D PlayerSwordPutAwayBackhand { get; } = new("player-sword-put-away-backhand", 0.48333335f)
    {
        Joints =
        [
            new(Hips,
                Move: new(Smooth,
                    new(0f, new(0.032f, -0.112f)), new(0.1833334f, new(0.032f, -0.112f)), new(0.26333344f, new(0.008f, -0.04f), Straight),
                    new(0.33333337f, new(-0.008f, 0.0096f), Straight), new(0.40333343f, new(-0.008f, 0.0096f)),
                    new(0.48333335f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.210628f, -0.0350724f)), new(0.1833334f, new(0.210628f, -0.0350724f)),
                    new(0.26333344f, new(0.0129991f, -0.000129998f), Straight), new(0.33333337f, new(-0.0259931f, -0.000519931f), Straight),
                    new(0.40333343f, new(-0.0129991f, -0.000129998f)), new(0.48333335f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.33f), new(0.1833334f, -0.33f), new(0.26333344f, -0.02f, StraightTurn), new(0.33333337f, 0.04f, StraightTurn),
                    new(0.40333343f, 0.02f), new(0.48333335f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.0605409f, -0.00485364f)), new(0.1833334f, new(-0.0605409f, -0.00485364f)),
                    new(0.26333344f, new(-0.0227863f, -0.000683784f), Straight), new(0.33333337f, new(-0.0151959f, -0.000303954f), Straight),
                    new(0.40333343f, new(-0.00759949f, -7.59959E-05f)), new(0.48333335f, new(-0.00759949f, -7.59959E-05f)))),
            new(LeftShoulder,
                Move: new(Smooth,
                    new(0f, new(0.102434f, 0f)), new(0.1833334f, new(0.102434f, 0f)), new(0.26333344f, new(0.0265947f, 0f), Straight),
                    new(0.33333337f, new(0.0866506f, 0f), Straight), new(0.40333343f, new(0.0557738f, 0f)), new(0.48333335f, new(0f, 0f))),
                Layer: new(SmoothTurn,
                    new(0f, -0.0381094f), new(0.1833334f, -0.0381094f), new(0.26333344f, -0.0172096f, StraightTurn),
                    new(0.33333337f, -0.0366154f, StraightTurn), new(0.40333343f, -0.0295396f), new(0.48333335f, 0f))),
            new(RightShoulder,
                Move: new(Smooth,
                    new(0f, new(-0.108942f, 0f)), new(0.1833334f, new(-0.108942f, 0f)), new(0.26333344f, new(-0.0302781f, 0f), Straight),
                    new(0.33333337f, new(-0.0933494f, 0f), Straight), new(0.40333343f, new(-0.0617129f, 0f)), new(0.48333335f, new(0f, 0f))),
                Turn: new(SmoothTurn,
                    new(0f, -5.70653f), new(0.1833334f, -5.70653f), new(0.26333344f, -4.66319f, StraightTurn),
                    new(0.33333337f, -2.82319f, StraightTurn), new(0.40333343f, -1.6908f), new(0.48333335f, -1.6408f)),
                Layer: new(SmoothTurn,
                    new(0f, 0.00875157f), new(0.1833334f, 0.00875157f), new(0.26333344f, 0.0093106f, StraightTurn),
                    new(0.33333337f, 0.0116154f, StraightTurn), new(0.40333343f, 0.013222f), new(0.48333335f, 0f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.213664f, 0.247225f)), new(0.1833334f, new(0.213664f, 0.247225f)),
                    new(0.26333344f, new(-0.0184008f, 0.070116f), Straight), new(0.33333337f, new(-0.0140256f, 0.0488804f), Straight),
                    new(0.40333343f, new(0.0079908f, 0.0491201f)), new(0.48333335f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.188976f, 0.0353374f)), new(0.1833334f, new(0.188976f, 0.0353374f)),
                    new(0.26333344f, new(0.448319f, 0.555456f), Straight), new(0.33333337f, new(-0.153944f, 0.141724f), Straight),
                    new(0.40333343f, new(-0.0172341f, 0.54413f)), new(0.48333335f, new(0.544091f, 0.788003f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.349066f, -0.135453f, 0f)), new(0.1833334f, new(0.349066f, -0.135453f, 0f)), new(0.26333344f, new(0f, 0f, 0f)),
                new(0.33333337f, new(0f, 0f, 0f)), new(0.40333343f, new(0f, 0f, 0f)), new(0.48333335f, new(0f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.48333335f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.48333335f, new(0.08f, 0f))],
        Markers = [new("sword-sheathe", 0.40333343f)],
    };

    public static SkeletonClip2D PlayerSwordSheathe { get; } = new("player-sword-sheathe", 0.42f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.024f, -0.056f), Smooth), new(0.12f, new(0.008f, -0.04f)), new(0.22f, new(-0.008f, 0.0096f)),
                    new(0.3f, new(-0.008f, 0.0096f), Smooth), new(0.42f, new(-0.008f, 0.0096f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0778129f, -0.00467438f), Smooth), new(0.12f, new(0.0129991f, -0.000129998f)),
                    new(0.22f, new(-0.0259931f, -0.000519931f)), new(0.3f, new(-0.0129991f, -0.000129998f), Smooth),
                    new(0.42f, new(0.0194971f, -0.00029248f))),
                Turn: Track.Linear(
                    new(0f, -0.12f, SmoothTurn), new(0.12f, -0.02f), new(0.22f, 0.04f), new(0.3f, 0.02f, SmoothTurn), new(0.42f, -0.03f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0303676f, -0.00121534f), Smooth), new(0.12f, new(-0.0227863f, -0.000683784f)),
                    new(0.22f, new(-0.0151959f, -0.000303954f)), new(0.3f, new(-0.00759949f, -7.59959E-05f), Smooth),
                    new(0.42f, new(-0.00759949f, -7.59959E-05f)))),
            new(RightShoulder,
                Turn: Track.Linear(
                    new(0f, -0.1f, SmoothTurn), new(0.12f, 1.62f), new(0.22f, 3.46f), new(0.3f, 4.59239f, SmoothTurn), new(0.42f, 4.64239f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.0447801f, 0.104306f), Smooth), new(0.12f, new(-0.0184008f, 0.070116f)), new(0.22f, new(-0.0140256f, 0.0488804f)),
                    new(0.3f, new(0.0079908f, 0.0491201f), Smooth), new(0.42f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.202503f, 0.407168f), Smooth), new(0.12f, new(0.449808f, 0.585697f)), new(0.22f, new(-0.0652871f, 0.112501f)),
                    new(0.3f, new(-0.00984636f, 0.482861f), Smooth), new(0.42f, new(0.544091f, 0.788003f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0.349066f, 0f, 0f)), new(0.12f, new(1.13446f, -0.314159f, 0f)), new(0.22f, new(0.436332f, -0.174533f, 0f)),
                new(0.3f, new(0f, 0f, 0f)), new(0.42f, new(0f, 0f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.42f, new(-0.052f, 0f)), new(RightLeg, 0f, 0.42f, new(0.052f, 0f))],
        Markers = [new("sword-sheathe", 0.3f)],
    };

    public static SkeletonClip2D PlayerSwordSideCut { get; } = new("player-sword-side-cut", 0.16666669f)
    {
        Joints =
        [
            new(Hips,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.024f, -0.032f)), new(0.01666668f, new(-0.024f, -0.04f)), new(0.03333333f, new(-0.016f, -0.048f)),
                    new(0.050000012f, new(0.048f, -0.088f)), new(0.08333334f, new(0.048f, -0.136f), Smooth),
                    new(0.15000004f, new(0.048f, -0.128f), Smooth), new(0.16666669f, new(0.048f, -0.128f)))),
            new(Chest,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0648917f, -0.00324726f)), new(0.01666668f, new(-0.0194971f, -0.00029248f)),
                    new(0.03333333f, new(-0.0324865f, -0.000812352f)), new(0.050000012f, new(0.204468f, -0.032997f)),
                    new(0.08333334f, new(0.253122f, -0.0513104f), Smooth), new(0.15000004f, new(0.23505f, -0.0439872f), Smooth),
                    new(0.16666669f, new(0.23505f, -0.0439872f))),
                Turn: Track.Linear(
                    new(0f, 0.1f), new(0.01666668f, 0.03f), new(0.03333333f, 0.05f), new(0.050000012f, -0.32f), new(0.08333334f, -0.4f, SmoothTurn),
                    new(0.15000004f, -0.37f, SmoothTurn), new(0.16666669f, -0.37f))),
            new(Head,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.01666668f, new(-0.00759949f, -7.59959E-05f)),
                    new(0.03333333f, new(-0.0227863f, -0.000683784f)), new(0.050000012f, new(-0.0680312f, -0.0061394f)),
                    new(0.08333334f, new(-0.0754943f, -0.00757471f), Smooth), new(0.15000004f, new(-0.0680312f, -0.0061394f), Smooth),
                    new(0.16666669f, new(-0.0680312f, -0.0061394f)))),
            new(LeftShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0.0866506f, 0f)), new(0.01666668f, new(0.118287f, 0f)), new(0.03333333f, new(0.118287f, 0f)),
                    new(0.050000012f, new(-0.0660445f, 0f)), new(0.08333334f, new(-0.0698433f, 0f), Smooth),
                    new(0.15000004f, new(-0.069956f, 0f), Smooth), new(0.16666669f, new(-0.069956f, 0f))),
                Layer: Track.Linear(
                    new(0f, -0.0366154f), new(0.01666668f, -0.038222f), new(0.03333333f, -0.038222f), new(0.050000012f, 0.105313f),
                    new(0.08333334f, 0.152601f, SmoothTurn), new(0.15000004f, 0.136748f, SmoothTurn), new(0.16666669f, 0.136748f))),
            new(RightShoulder,
                Move: Track.Of<Vector2f>(
                    new(0f, new(-0.0933494f, 0f)), new(0.01666668f, new(-0.124226f, 0f)), new(0.03333333f, new(-0.124226f, 0f)),
                    new(0.050000012f, new(0.0922448f, 0f)), new(0.08333334f, new(0.108787f, 0f), Smooth),
                    new(0.15000004f, new(0.104575f, 0f), Smooth), new(0.16666669f, new(0.104575f, 0f))),
                Turn: Track.Linear(
                    new(0f, -1.6908f), new(0.01666668f, -1.7708f), new(0.03333333f, -3.99432f), new(0.050000012f, -5.74073f),
                    new(0.08333334f, -5.81488f, SmoothTurn), new(0.15000004f, -5.75231f, SmoothTurn), new(0.16666669f, -5.75231f)),
                Layer: Track.Linear(
                    new(0f, 0.0116154f), new(0.01666668f, 0.00453962f), new(0.03333333f, 0.00453962f), new(0.050000012f, -0.0833286f),
                    new(0.08333334f, -0.127792f, SmoothTurn), new(0.15000004f, -0.112508f, SmoothTurn), new(0.16666669f, -0.112508f))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.0374871f, 0.120618f)), new(0.01666668f, new(0.11613f, 0.185408f)), new(0.03333333f, new(0.127784f, 0.201554f)),
                    new(0.050000012f, new(-0.0941312f, 0.144147f)), new(0.08333334f, new(-0.0889212f, 0.147379f), Smooth),
                    new(0.15000004f, new(-0.0910316f, 0.143665f), Smooth), new(0.16666669f, new(-0.0910316f, 0.143665f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.0210216f, 0.575539f)), new(0.01666668f, new(-0.510481f, 0.245834f)), new(0.03333333f, new(0.546399f, 0.775706f)),
                    new(0.050000012f, new(0.269717f, 0.0681713f)), new(0.08333334f, new(0.209834f, 0.0439671f), Smooth),
                    new(0.15000004f, new(0.220106f, 0.0471136f), Smooth), new(0.16666669f, new(0.220106f, 0.0471136f)))),
        ],
        Mounts =
        [
            new(SwordHand, Track.Of<Vector3f>(
                new(0f, new(0f, 0f, 0f)), new(0.01666668f, new(0f, 0f, 0f)), new(0.03333333f, new(0.523599f, -0.18405f, 0f)),
                new(0.050000012f, new(1.309f, -0.341124f, 0f)), new(0.08333334f, new(0.959931f, 0.696982f, 0f)),
                new(0.15000004f, new(0.349066f, 0.517989f, 0f)), new(0.16666669f, new(0.349066f, 0.517989f, 0f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 0.16666669f, new(-0.08f, 0f)), new(RightLeg, 0f, 0.16666669f, new(0.08f, 0f))],
        Markers = [
            new("sword-draw", 0.01666668f), new("swoosh", 0.03333333f), new("strike", 0.050000012f), new("swoosh-end", 0.06666669f),
            new("recover", 0.13333336f)],
    };

    public static SkeletonClip2D PlayerWallGrip { get; } = new("player-wall-grip", 1.2f, loop: true)
    {
        Joints =
        [
            new(Hips, Move: new(Smooth, new(0f, new(-0.064f, -0.08f)), new(0.6f, new(-0.064f, -0.092f)), new(1.2f, new(-0.064f, -0.08f)))),
            new(Chest,
                Move: new(Smooth,
                    new(0f, new(0.311627f, -0.0795714f)), new(0.6f, new(0.300156f, -0.0734533f)), new(1.2f, new(0.311627f, -0.0795714f))),
                Turn: new(SmoothTurn, new(0f, -0.5f), new(0.6f, -0.48f), new(1.2f, -0.5f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.112298f, -0.0169721f)), new(0.6f, new(-0.105015f, -0.0147989f)), new(1.2f, new(-0.112298f, -0.0169721f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth, new(0f, new(-1.27341f, 1.16292f)), new(0.6f, new(-1.24917f, 1.18536f)), new(1.2f, new(-1.27341f, 1.16292f)))),
            new(LeftLeg, Target: new(Smooth, new(0f, new(-0.272f, 0.028f)), new(0.6f, new(-0.272f, 0.02f)), new(1.2f, new(-0.272f, 0.028f)))),
            new(RightArm,
                Target: new(Smooth, new(0f, new(0.385229f, 0.307094f)), new(0.6f, new(0.395297f, 0.352809f)), new(1.2f, new(0.385229f, 0.307094f)))),
            new(RightLeg, Target: new(Smooth, new(0f, new(-0.048f, 0.14f)), new(0.6f, new(-0.032f, 0.124f)), new(1.2f, new(-0.048f, 0.14f)))),
        ],
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
                    new(0f, new(0.0194971f, -0.00029248f)), new(0.8f, new(0f, 0f)), new(1.05f, new(0f, 0f), Straight),
                    new(1.14f, new(0.253122f, -0.0513104f)), new(1.3f, new(0.265044f, -0.0564922f)), new(1.85f, new(0.0194971f, -0.00029248f))),
                Turn: new(SmoothTurn,
                    new(0f, -0.03f), new(0.8f, 0f), new(1.05f, 0f, StraightTurn), new(1.14f, -0.28f), new(1.3f, -0.28f), new(1.85f, -0.03f))),
            new(Head,
                Move: new(Smooth,
                    new(0f, new(-0.00759949f, -7.59959E-05f)), new(0.8f, new(0f, 0f)), new(1.05f, new(0f, 0f), Straight),
                    new(1.14f, new(-0.0454906f, -0.00273272f)), new(1.3f, new(-0.0530264f, -0.0037179f)),
                    new(1.85f, new(-0.00759949f, -7.59959E-05f)))),
        ],
        Limbs =
        [
            new(LeftArm,
                Target: new(Smooth,
                    new(0f, new(0.0379748f, 0.0517697f)), new(0.8f, new(0.03f, 1.5f)), new(1.05f, new(0.03f, 1.5f), Straight),
                    new(1.14f, new(-0.0584449f, 0.203692f)), new(1.3f, new(-0.0677754f, 0.195057f)), new(1.85f, new(0.0379748f, 0.0517697f)))),
            new(RightArm,
                Target: new(Smooth,
                    new(0f, new(0.0138332f, 0.0317393f)), new(0.35f, new(-0.32f, -0.18f)), new(0.8f, new(-0.03f, 1.5f)),
                    new(1.05f, new(-0.03f, 1.5f), Straight), new(1.14f, new(0.385794f, 0.64568f)), new(1.3f, new(0.397833f, 0.687225f)),
                    new(1.85f, new(0.0138332f, 0.0317393f)))),
        ],
        Contacts = [new(LeftLeg, 0f, 1.85f, new(-0.112f, 0f)), new(RightLeg, 0f, 1.85f, new(0.112f, 0f))],
        Markers = [new("search", 0.15f), new("lift", 0.55f), new("aim", 0.8f), new("release", 1.14f), new("recover", 1.3f)],
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
                    new(0f, new(0f, -0.02f)), new(0.075f, new(0f, -0.0157824f)), new(0.15f, new(0f, -0.00560002f)),
                    new(0.22500001f, new(0f, 0.00458231f)), new(0.3f, new(0f, 0.00880003f)), new(0.375f, new(0f, 0.00458231f)),
                    new(0.45000002f, new(0f, -0.00560002f)), new(0.52500004f, new(0f, -0.0157824f)), new(0.6f, new(0f, -0.02f)),
                    new(0.675f, new(0f, -0.0157824f)), new(0.75f, new(0f, -0.00559998f)), new(0.82500005f, new(0f, 0.00458231f)),
                    new(0.90000004f, new(0f, 0.00880003f)), new(0.975f, new(0f, 0.00458231f)), new(1.0500001f, new(0f, -0.00560002f)),
                    new(1.125f, new(0f, -0.0157824f)), new(1.2f, new(0f, -0.02f)))),
        ],
        Limbs =
        [
            new(LeftArm, Target: new(Smooth, new(0f, new(0.5f, 0.12f)), new(1.2f, new(0.5f, 0.12f)))),
            new(LeftLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.075f, new(-0.107813f, 0.0140589f)), new(0.15f, new(-0.0875f, 0.048f)),
                    new(0.22500001f, new(-0.0484375f, 0.0819411f)), new(0.3f, new(0f, 0.096f)), new(0.375f, new(0.0484375f, 0.0819411f)),
                    new(0.45000002f, new(0.0875f, 0.048f)), new(0.52500004f, new(0.107813f, 0.0140589f)), new(0.6f, new(0.1f, 0f)),
                    new(0.675f, new(0.075f, 0f)), new(0.75f, new(0.05f, 0f)), new(0.82500005f, new(0.025f, 0f)), new(0.90000004f, new(0f, 0f)),
                    new(0.975f, new(-0.025f, 0f)), new(1.0500001f, new(-0.05f, 0f)), new(1.125f, new(-0.075f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(RightArm,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.21f, 0.04f)), new(0.075f, new(-0.196298f, 0.04f)), new(0.15f, new(-0.157279f, 0.04f)),
                    new(0.22500001f, new(-0.098883f, 0.0400001f)), new(0.3f, new(-0.03f, 0.04f)), new(0.375f, new(0.0388831f, 0.0400001f)),
                    new(0.45000002f, new(0.0972792f, 0.04f)), new(0.52500004f, new(0.136298f, 0.04f)), new(0.6f, new(0.15f, 0.04f)),
                    new(0.675f, new(0.136298f, 0.04f)), new(0.75f, new(0.0972792f, 0.04f)), new(0.82500005f, new(0.038883f, 0.0400001f)),
                    new(0.90000004f, new(-0.03f, 0.04f)), new(0.975f, new(-0.0988831f, 0.04f)), new(1.0500001f, new(-0.157279f, 0.04f)),
                    new(1.125f, new(-0.196298f, 0.04f)), new(1.2f, new(-0.21f, 0.04f)))),
            new(RightLeg,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.1f, 0f)), new(0.075f, new(0.075f, 0f)), new(0.15f, new(0.05f, 0f)), new(0.22500001f, new(0.025f, 0f)),
                    new(0.3f, new(0f, 0f)), new(0.375f, new(-0.025f, 0f)), new(0.45000002f, new(-0.05f, 0f)), new(0.52500004f, new(-0.075f, 0f)),
                    new(0.6f, new(-0.1f, 0f)), new(0.675f, new(-0.107813f, 0.0140589f)), new(0.75f, new(-0.0875f, 0.048f)),
                    new(0.82500005f, new(-0.0484375f, 0.0819411f)), new(0.90000004f, new(0f, 0.096f)), new(0.975f, new(0.0484375f, 0.0819411f)),
                    new(1.0500001f, new(0.0875f, 0.048f)), new(1.125f, new(0.107813f, 0.0140589f)), new(1.2f, new(0.1f, 0f)))),
        ],
        Contacts = [new(RightLeg, 0f, 0.6f, new(0.1f, 0f)), new(LeftLeg, 0.6f, 1.2f, new(0.3f, 0f))],
        Travel = Track.Of<Vector2f>(
            new(0f, new(0f, 0f)), new(0.075f, new(0.025f, 0f)), new(0.15f, new(0.05f, 0f)), new(0.22500001f, new(0.075f, 0f)),
            new(0.3f, new(0.1f, 0f)), new(0.375f, new(0.125f, 0f)), new(0.45000002f, new(0.15f, 0f)), new(0.52500004f, new(0.175f, 0f)),
            new(0.6f, new(0.2f, 0f)), new(0.675f, new(0.225f, 0f)), new(0.75f, new(0.25f, 0f)), new(0.82500005f, new(0.275f, 0f)),
            new(0.90000004f, new(0.3f, 0f)), new(0.975f, new(0.325f, 0f)), new(1.0500001f, new(0.35f, 0f)), new(1.125f, new(0.375f, 0f)),
            new(1.2f, new(0.4f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        ClubCavemanSlam, PersonDeath, PersonHammerSlam, PersonHeavyWalk, PersonHit, PersonIdle, PersonJump, PersonPistolShot, PersonRun,
        PersonThrust, PersonWalk, PlayerBalanceBackward, PlayerBalanceForward, PlayerCelebrate, PlayerClimb, PlayerClimbOff, PlayerClimbOn,
        PlayerDash, PlayerDeath, PlayerFall, PlayerGunAim, PlayerGunCharge, PlayerGunShot, PlayerGunWallShot, PlayerHealGather, PlayerHit,
        PlayerIdle, PlayerJump, PlayerLand, PlayerSwordBackhand, PlayerSwordDownAttack, PlayerSwordForehand, PlayerSwordPutAway,
        PlayerSwordPutAwayBackhand, PlayerSwordSheathe, PlayerSwordSideCut, PlayerWallGrip, RockThrowerThrow, ShieldDefenderBash, ShieldDefenderIdle,
        ShieldDefenderWalk];
}
