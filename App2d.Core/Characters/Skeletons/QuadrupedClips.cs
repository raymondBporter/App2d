// Generated from the authored JSON clips by LegacyClipEmitter in App2d.Tests. While those clips exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named GeneratedRigTests) instead of editing this file.
using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.QuadrupedSkeleton;

namespace App2d.Core.Characters;

/// <summary>The quadruped animations, for <see cref="QuadrupedSkeleton.Skeleton"/>. Values are changes from rest, in the skeleton's own units.</summary>
public static class QuadrupedClips
{

    public static SkeletonClip2D QuadrupedBrake { get; } = new("quadruped-brake", 0.65f)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(new(0f, new(0f, -0.055f)), new(0.25f, new(0.08f, -0.1f)), new(0.65f, new(0f, 0f))),
                Turn: Track.Linear(new(0f, 0f), new(0.325f, 0.08f), new(0.65f, 0f))),
            new(Head, Turn: Track.Linear(new(0f, -0.32f), new(0.325f, 0.1f), new(0.65f, 0f))),
        ],
        Limbs =
        [
            new(FarHind, Target: Track.Of<Vector2f>(new(0f, new(0.15f, 0f)), new(0.3f, new(0.22f, 0f)), new(0.65f, new(0f, 0f)))),
            new(FarFront, Target: Track.Of<Vector2f>(new(0f, new(0.15f, 0f)), new(0.3f, new(0.22f, 0f)), new(0.65f, new(0f, 0f)))),
            new(NearHind, Target: Track.Of<Vector2f>(new(0f, new(0.15f, 0f)), new(0.3f, new(0.22f, 0f)), new(0.65f, new(0f, 0f)))),
            new(NearFront, Target: Track.Of<Vector2f>(new(0f, new(0.15f, 0f)), new(0.3f, new(0.22f, 0f)), new(0.65f, new(0f, 0f)))),
        ],
        Markers = [new("stop", 0.3f)],
    };

    public static SkeletonClip2D QuadrupedHeadDown { get; } = new("quadruped-head-down", 0.55f)
    {
        Joints =
        [
            new(Head,
                Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.55f, new(0f, -0.18f))),
                Turn: Track.Linear(new(0f, 0f), new(0.275f, -0.4f), new(0.55f, -0.95f))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.55f, new(0f, 0f)), new(FarFront, 0f, 0.55f, new(0f, 0f)), new(NearHind, 0f, 0.55f, new(0f, 0f)),
            new(NearFront, 0f, 0.55f, new(0f, 0f))],
    };

    public static SkeletonClip2D QuadrupedIdle { get; } = new("quadruped-idle", 2f, loop: true)
    {
        Joints =
        [
            new(Body, Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1f, new(0f, 0.025f)), new(2f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(1f, -0.025f), new(2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0f), new(1f, 0.12f), new(2f, 0f))),
        ],
        Contacts = [
            new(FarHind, 0f, 2f, new(0f, 0f)), new(FarFront, 0f, 2f, new(0f, 0f)), new(NearHind, 0f, 2f, new(0f, 0f)),
            new(NearFront, 0f, 2f, new(0f, 0f))],
    };

    public static SkeletonClip2D QuadrupedRecover { get; } = new("quadruped-recover", 1.2f)
    {
        Joints =
        [
            new(Head,
                Move: Track.Of<Vector2f>(new(0f, new(0f, -0.18f)), new(1.2f, new(0f, 0f))),
                Turn: Track.Linear(new(0f, -0.95f), new(0.6f, -0.35f), new(1.2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, -0.15f), new(0.6f, 0.16f), new(1.2f, 0f))),
        ],
        Contacts = [
            new(FarHind, 0f, 1.2f, new(0f, 0f)), new(FarFront, 0f, 1.2f, new(0f, 0f)), new(NearHind, 0f, 1.2f, new(0f, 0f)),
            new(NearFront, 0f, 1.2f, new(0f, 0f))],
    };

    public static SkeletonClip2D QuadrupedRun { get; } = new("quadruped-run", 0.65f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.08125f, new(0f, 0.025f)), new(0.1625f, new(0f, 0f)), new(0.24374999f, new(0f, -0.025f)),
                    new(0.325f, new(0f, 0f)), new(0.40625f, new(0f, 0.025f)), new(0.48749998f, new(0f, 0f)), new(0.56874996f, new(0f, -0.025f)),
                    new(0.65f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.325f, -0.04f), new(0.65f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.325f, -0.12f), new(0.65f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.21f, 0f)), new(0.027083334f, new(0.180833f, 0f)), new(0.054166667f, new(0.151667f, 0f)),
                    new(0.08125f, new(0.1225f, 0f)), new(0.108333334f, new(0.0933333f, 0f)), new(0.13541666f, new(0.0641667f, 0f)),
                    new(0.1625f, new(0.035f, 0f)), new(0.18958332f, new(0.00583336f, 0f)), new(0.21666667f, new(-0.0233333f, 0f)),
                    new(0.24374999f, new(-0.0525f, 0f)), new(0.2708333f, new(-0.0816666f, 0f)), new(0.29791665f, new(-0.110833f, 0f)),
                    new(0.325f, new(-0.14f, 0f)), new(0.35208333f, new(-0.169167f, 0f)), new(0.37916663f, new(-0.198333f, 0f)),
                    new(0.39000002f, new(-0.21f, 0f)), new(0.40625f, new(-0.18375f, 0.0370671f)), new(0.43333334f, new(-0.14f, 0.095f)),
                    new(0.46041664f, new(-0.09625f, 0.14285f)), new(0.48749998f, new(-0.0525f, 0.175537f)),
                    new(0.51458335f, new(-0.00874999f, 0.189593f)), new(0.5416666f, new(0.035f, 0.183526f)),
                    new(0.56874996f, new(0.07875f, 0.157979f)), new(0.5958333f, new(0.1225f, 0.115665f)),
                    new(0.62291664f, new(0.16625f, 0.0610735f)), new(0.65f, new(0.21f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.14f, 0f)), new(0.027083334f, new(-0.169167f, 0f)), new(0.054166667f, new(-0.198333f, 0f)),
                    new(0.06500001f, new(-0.21f, 0f)), new(0.08125f, new(-0.18375f, 0.0370671f)), new(0.108333334f, new(-0.14f, 0.095f)),
                    new(0.13541666f, new(-0.09625f, 0.14285f)), new(0.1625f, new(-0.0525f, 0.175537f)),
                    new(0.18958332f, new(-0.00875005f, 0.189593f)), new(0.21666667f, new(0.035f, 0.183526f)),
                    new(0.24374999f, new(0.07875f, 0.157979f)), new(0.2708333f, new(0.1225f, 0.115665f)),
                    new(0.29791665f, new(0.16625f, 0.0610734f)), new(0.325f, new(0.21f, 0f)), new(0.35208333f, new(0.180833f, 0f)),
                    new(0.37916663f, new(0.151667f, 0f)), new(0.40625f, new(0.1225f, 0f)), new(0.43333334f, new(0.0933333f, 0f)),
                    new(0.46041664f, new(0.0641667f, 0f)), new(0.48749998f, new(0.035f, 0f)), new(0.51458335f, new(0.0058333f, 0f)),
                    new(0.5416666f, new(-0.0233333f, 0f)), new(0.56874996f, new(-0.0525f, 0f)), new(0.5958333f, new(-0.0816667f, 0f)),
                    new(0.62291664f, new(-0.110833f, 0f)), new(0.65f, new(-0.14f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.14f, 0f)), new(0.027083334f, new(-0.169167f, 0f)), new(0.054166667f, new(-0.198333f, 0f)),
                    new(0.06500001f, new(-0.21f, 0f)), new(0.08125f, new(-0.18375f, 0.0370671f)), new(0.108333334f, new(-0.14f, 0.095f)),
                    new(0.13541666f, new(-0.09625f, 0.14285f)), new(0.1625f, new(-0.0525f, 0.175537f)),
                    new(0.18958332f, new(-0.00875005f, 0.189593f)), new(0.21666667f, new(0.035f, 0.183526f)),
                    new(0.24374999f, new(0.07875f, 0.157979f)), new(0.2708333f, new(0.1225f, 0.115665f)),
                    new(0.29791665f, new(0.16625f, 0.0610734f)), new(0.325f, new(0.21f, 0f)), new(0.35208333f, new(0.180833f, 0f)),
                    new(0.37916663f, new(0.151667f, 0f)), new(0.40625f, new(0.1225f, 0f)), new(0.43333334f, new(0.0933333f, 0f)),
                    new(0.46041664f, new(0.0641667f, 0f)), new(0.48749998f, new(0.035f, 0f)), new(0.51458335f, new(0.0058333f, 0f)),
                    new(0.5416666f, new(-0.0233333f, 0f)), new(0.56874996f, new(-0.0525f, 0f)), new(0.5958333f, new(-0.0816667f, 0f)),
                    new(0.62291664f, new(-0.110833f, 0f)), new(0.65f, new(-0.14f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.21f, 0f)), new(0.027083334f, new(0.180833f, 0f)), new(0.054166667f, new(0.151667f, 0f)),
                    new(0.08125f, new(0.1225f, 0f)), new(0.108333334f, new(0.0933333f, 0f)), new(0.13541666f, new(0.0641667f, 0f)),
                    new(0.1625f, new(0.035f, 0f)), new(0.18958332f, new(0.00583336f, 0f)), new(0.21666667f, new(-0.0233333f, 0f)),
                    new(0.24374999f, new(-0.0525f, 0f)), new(0.2708333f, new(-0.0816666f, 0f)), new(0.29791665f, new(-0.110833f, 0f)),
                    new(0.325f, new(-0.14f, 0f)), new(0.35208333f, new(-0.169167f, 0f)), new(0.37916663f, new(-0.198333f, 0f)),
                    new(0.39000002f, new(-0.21f, 0f)), new(0.40625f, new(-0.18375f, 0.0370671f)), new(0.43333334f, new(-0.14f, 0.095f)),
                    new(0.46041664f, new(-0.09625f, 0.14285f)), new(0.48749998f, new(-0.0525f, 0.175537f)),
                    new(0.51458335f, new(-0.00874999f, 0.189593f)), new(0.5416666f, new(0.035f, 0.183526f)),
                    new(0.56874996f, new(0.07875f, 0.157979f)), new(0.5958333f, new(0.1225f, 0.115665f)),
                    new(0.62291664f, new(0.16625f, 0.0610735f)), new(0.65f, new(0.21f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.39000002f, new(0.21f, 0f)), new(FarFront, 0f, 0.06500001f, new(-0.14f, 0f)),
            new(FarFront, 0.325f, 0.65f, new(0.56f, 0f)), new(NearHind, 0f, 0.06500001f, new(-0.14f, 0f)),
            new(NearHind, 0.325f, 0.65f, new(0.56f, 0f)), new(NearFront, 0f, 0.39000002f, new(0.21f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.65f, new(0.7f, 0f))),
    };

    public static SkeletonClip2D QuadrupedRush { get; } = new("quadruped-rush", 0.48f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.055f)), new(0.06f, new(0f, -0.03f)), new(0.12f, new(0f, -0.055f)), new(0.17999999f, new(0f, -0.08f)),
                    new(0.24f, new(0f, -0.055f)), new(0.29999998f, new(0f, -0.03f)), new(0.35999998f, new(0f, -0.055f)), new(0.42f, new(0f, -0.08f)),
                    new(0.48f, new(0f, -0.055f)))),
            new(Head, Turn: Track.Linear(new(0f, -0.32f), new(0.24f, -0.36f), new(0.48f, -0.32f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.24f, -0.12f), new(0.48f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.23f, 0f)), new(0.02f, new(0.198056f, 0f)), new(0.04f, new(0.166111f, 0f)), new(0.06f, new(0.134167f, 0f)),
                    new(0.08f, new(0.102222f, 0f)), new(0.099999994f, new(0.0702778f, 0f)), new(0.12f, new(0.0383333f, 0f)),
                    new(0.13999999f, new(0.0063889f, 0f)), new(0.16f, new(-0.0255556f, 0f)), new(0.17999999f, new(-0.0575f, 0f)),
                    new(0.19999999f, new(-0.0894444f, 0f)), new(0.22f, new(-0.121389f, 0f)), new(0.24f, new(-0.153333f, 0f)),
                    new(0.26f, new(-0.185278f, 0f)), new(0.27999997f, new(-0.217222f, 0f)), new(0.28800002f, new(-0.23f, 0f)),
                    new(0.29999998f, new(-0.20125f, 0.0292635f)), new(0.32f, new(-0.153333f, 0.075f)), new(0.33999997f, new(-0.105417f, 0.112776f)),
                    new(0.35999998f, new(-0.0575f, 0.138582f)), new(0.38f, new(-0.00958332f, 0.149679f)),
                    new(0.39999998f, new(0.0383333f, 0.144889f)), new(0.42f, new(0.08625f, 0.12472f)), new(0.44f, new(0.134167f, 0.0913142f)),
                    new(0.45999998f, new(0.182083f, 0.048216f)), new(0.48f, new(0.23f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.153333f, 0f)), new(0.02f, new(-0.185278f, 0f)), new(0.04f, new(-0.217222f, 0f)),
                    new(0.04800001f, new(-0.23f, 0f)), new(0.06f, new(-0.20125f, 0.0292635f)), new(0.08f, new(-0.153333f, 0.075f)),
                    new(0.099999994f, new(-0.105417f, 0.112776f)), new(0.12f, new(-0.0575f, 0.138582f)),
                    new(0.13999999f, new(-0.0095834f, 0.149679f)), new(0.16f, new(0.0383334f, 0.144889f)), new(0.17999999f, new(0.08625f, 0.12472f)),
                    new(0.19999999f, new(0.134167f, 0.0913142f)), new(0.22f, new(0.182083f, 0.0482159f)), new(0.24f, new(0.23f, 0f)),
                    new(0.26f, new(0.198056f, 0f)), new(0.27999997f, new(0.166111f, 0f)), new(0.29999998f, new(0.134167f, 0f)),
                    new(0.32f, new(0.102222f, 0f)), new(0.33999997f, new(0.0702778f, 0f)), new(0.35999998f, new(0.0383333f, 0f)),
                    new(0.38f, new(0.00638884f, 0f)), new(0.39999998f, new(-0.0255555f, 0f)), new(0.42f, new(-0.0575f, 0f)),
                    new(0.44f, new(-0.0894445f, 0f)), new(0.45999998f, new(-0.121389f, 0f)), new(0.48f, new(-0.153333f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.153333f, 0f)), new(0.02f, new(-0.185278f, 0f)), new(0.04f, new(-0.217222f, 0f)),
                    new(0.04800001f, new(-0.23f, 0f)), new(0.06f, new(-0.20125f, 0.0292635f)), new(0.08f, new(-0.153333f, 0.075f)),
                    new(0.099999994f, new(-0.105417f, 0.112776f)), new(0.12f, new(-0.0575f, 0.138582f)),
                    new(0.13999999f, new(-0.0095834f, 0.149679f)), new(0.16f, new(0.0383334f, 0.144889f)), new(0.17999999f, new(0.08625f, 0.12472f)),
                    new(0.19999999f, new(0.134167f, 0.0913142f)), new(0.22f, new(0.182083f, 0.0482159f)), new(0.24f, new(0.23f, 0f)),
                    new(0.26f, new(0.198056f, 0f)), new(0.27999997f, new(0.166111f, 0f)), new(0.29999998f, new(0.134167f, 0f)),
                    new(0.32f, new(0.102222f, 0f)), new(0.33999997f, new(0.0702778f, 0f)), new(0.35999998f, new(0.0383333f, 0f)),
                    new(0.38f, new(0.00638884f, 0f)), new(0.39999998f, new(-0.0255555f, 0f)), new(0.42f, new(-0.0575f, 0f)),
                    new(0.44f, new(-0.0894445f, 0f)), new(0.45999998f, new(-0.121389f, 0f)), new(0.48f, new(-0.153333f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.23f, 0f)), new(0.02f, new(0.198056f, 0f)), new(0.04f, new(0.166111f, 0f)), new(0.06f, new(0.134167f, 0f)),
                    new(0.08f, new(0.102222f, 0f)), new(0.099999994f, new(0.0702778f, 0f)), new(0.12f, new(0.0383333f, 0f)),
                    new(0.13999999f, new(0.0063889f, 0f)), new(0.16f, new(-0.0255556f, 0f)), new(0.17999999f, new(-0.0575f, 0f)),
                    new(0.19999999f, new(-0.0894444f, 0f)), new(0.22f, new(-0.121389f, 0f)), new(0.24f, new(-0.153333f, 0f)),
                    new(0.26f, new(-0.185278f, 0f)), new(0.27999997f, new(-0.217222f, 0f)), new(0.28800002f, new(-0.23f, 0f)),
                    new(0.29999998f, new(-0.20125f, 0.0292635f)), new(0.32f, new(-0.153333f, 0.075f)), new(0.33999997f, new(-0.105417f, 0.112776f)),
                    new(0.35999998f, new(-0.0575f, 0.138582f)), new(0.38f, new(-0.00958332f, 0.149679f)),
                    new(0.39999998f, new(0.0383333f, 0.144889f)), new(0.42f, new(0.08625f, 0.12472f)), new(0.44f, new(0.134167f, 0.0913142f)),
                    new(0.45999998f, new(0.182083f, 0.048216f)), new(0.48f, new(0.23f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.28800002f, new(0.23f, 0f)), new(FarFront, 0f, 0.04800001f, new(-0.153333f, 0f)),
            new(FarFront, 0.24f, 0.48f, new(0.613333f, 0f)), new(NearHind, 0f, 0.04800001f, new(-0.153333f, 0f)),
            new(NearHind, 0.24f, 0.48f, new(0.613333f, 0f)), new(NearFront, 0f, 0.28800002f, new(0.23f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.48f, new(0.766667f, 0f))),
    };

    public static SkeletonClip2D QuadrupedScrape { get; } = new("quadruped-scrape", 1.1f, loop: true)
    {
        Joints =
        [
            new(Head, Turn: Track.Linear(new(0f, -0.12f), new(0.55f, -0.18f), new(1.1f, -0.12f))),
        ],
        Limbs =
        [
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.25f, new(0.16f, 0.12f)), new(0.45f, new(0.22f, 0f)), new(0.78f, new(-0.18f, 0f)),
                    new(1.1f, new(0f, 0f)))),
        ],
        Contacts = [new(FarHind, 0f, 1.1f, new(0f, 0f)), new(FarFront, 0f, 1.1f, new(0f, 0f)), new(NearHind, 0f, 1.1f, new(0f, 0f))],
        Markers = [new("scrape", 0.45f)],
    };

    public static SkeletonClip2D QuadrupedWalk { get; } = new("quadruped-walk", 1.2f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, 0f)), new(0.15f, new(0f, 0.025f)), new(0.3f, new(0f, 0f)), new(0.45000002f, new(0f, -0.025f)),
                    new(0.6f, new(0f, 0f)), new(0.75f, new(0f, 0.025f)), new(0.90000004f, new(0f, 0f)), new(1.0500001f, new(0f, -0.025f)),
                    new(1.2f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.6f, -0.04f), new(1.2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.6f, -0.12f), new(1.2f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.15f, 0f)), new(0.050000004f, new(0.129167f, 0f)), new(0.10000001f, new(0.108333f, 0f)),
                    new(0.15f, new(0.0875f, 0f)), new(0.20000002f, new(0.0666667f, 0f)), new(0.25f, new(0.0458333f, 0f)), new(0.3f, new(0.025f, 0f)),
                    new(0.35f, new(0.00416668f, 0f)), new(0.40000004f, new(-0.0166667f, 0f)), new(0.45000002f, new(-0.0375f, 0f)),
                    new(0.5f, new(-0.0583333f, 0f)), new(0.55f, new(-0.0791667f, 0f)), new(0.6f, new(-0.1f, 0f)),
                    new(0.65000004f, new(-0.120833f, 0f)), new(0.7f, new(-0.141667f, 0f)), new(0.72f, new(-0.15f, 0f)),
                    new(0.75f, new(-0.13125f, 0.0253617f)), new(0.8000001f, new(-0.1f, 0.065f)), new(0.85f, new(-0.06875f, 0.0977392f)),
                    new(0.90000004f, new(-0.0375f, 0.120104f)), new(0.95000005f, new(-0.00624999f, 0.129722f)), new(1f, new(0.025f, 0.12557f)),
                    new(1.0500001f, new(0.05625f, 0.108091f)), new(1.1f, new(0.0875f, 0.079139f)), new(1.15f, new(0.11875f, 0.0417872f)),
                    new(1.2f, new(0.15f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.050000004f, new(-0.120833f, 0f)), new(0.10000001f, new(-0.141667f, 0f)),
                    new(0.120000035f, new(-0.15f, 0f)), new(0.15f, new(-0.13125f, 0.0253617f)), new(0.20000002f, new(-0.1f, 0.065f)),
                    new(0.25f, new(-0.06875f, 0.0977392f)), new(0.3f, new(-0.0375f, 0.120104f)), new(0.35f, new(-0.00625004f, 0.129722f)),
                    new(0.40000004f, new(0.025f, 0.12557f)), new(0.45000002f, new(0.05625f, 0.108091f)), new(0.5f, new(0.0875f, 0.079139f)),
                    new(0.55f, new(0.11875f, 0.0417871f)), new(0.6f, new(0.15f, 0f)), new(0.65000004f, new(0.129167f, 0f)),
                    new(0.7f, new(0.108333f, 0f)), new(0.75f, new(0.0875f, 0f)), new(0.8000001f, new(0.0666666f, 0f)),
                    new(0.85f, new(0.0458334f, 0f)), new(0.90000004f, new(0.025f, 0f)), new(0.95000005f, new(0.00416663f, 0f)),
                    new(1f, new(-0.0166666f, 0f)), new(1.0500001f, new(-0.0375f, 0f)), new(1.1f, new(-0.0583334f, 0f)),
                    new(1.15f, new(-0.0791666f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.050000004f, new(-0.120833f, 0f)), new(0.10000001f, new(-0.141667f, 0f)),
                    new(0.120000035f, new(-0.15f, 0f)), new(0.15f, new(-0.13125f, 0.0253617f)), new(0.20000002f, new(-0.1f, 0.065f)),
                    new(0.25f, new(-0.06875f, 0.0977392f)), new(0.3f, new(-0.0375f, 0.120104f)), new(0.35f, new(-0.00625004f, 0.129722f)),
                    new(0.40000004f, new(0.025f, 0.12557f)), new(0.45000002f, new(0.05625f, 0.108091f)), new(0.5f, new(0.0875f, 0.079139f)),
                    new(0.55f, new(0.11875f, 0.0417871f)), new(0.6f, new(0.15f, 0f)), new(0.65000004f, new(0.129167f, 0f)),
                    new(0.7f, new(0.108333f, 0f)), new(0.75f, new(0.0875f, 0f)), new(0.8000001f, new(0.0666666f, 0f)),
                    new(0.85f, new(0.0458334f, 0f)), new(0.90000004f, new(0.025f, 0f)), new(0.95000005f, new(0.00416663f, 0f)),
                    new(1f, new(-0.0166666f, 0f)), new(1.0500001f, new(-0.0375f, 0f)), new(1.1f, new(-0.0583334f, 0f)),
                    new(1.15f, new(-0.0791666f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.15f, 0f)), new(0.050000004f, new(0.129167f, 0f)), new(0.10000001f, new(0.108333f, 0f)),
                    new(0.15f, new(0.0875f, 0f)), new(0.20000002f, new(0.0666667f, 0f)), new(0.25f, new(0.0458333f, 0f)), new(0.3f, new(0.025f, 0f)),
                    new(0.35f, new(0.00416668f, 0f)), new(0.40000004f, new(-0.0166667f, 0f)), new(0.45000002f, new(-0.0375f, 0f)),
                    new(0.5f, new(-0.0583333f, 0f)), new(0.55f, new(-0.0791667f, 0f)), new(0.6f, new(-0.1f, 0f)),
                    new(0.65000004f, new(-0.120833f, 0f)), new(0.7f, new(-0.141667f, 0f)), new(0.72f, new(-0.15f, 0f)),
                    new(0.75f, new(-0.13125f, 0.0253617f)), new(0.8000001f, new(-0.1f, 0.065f)), new(0.85f, new(-0.06875f, 0.0977392f)),
                    new(0.90000004f, new(-0.0375f, 0.120104f)), new(0.95000005f, new(-0.00624999f, 0.129722f)), new(1f, new(0.025f, 0.12557f)),
                    new(1.0500001f, new(0.05625f, 0.108091f)), new(1.1f, new(0.0875f, 0.079139f)), new(1.15f, new(0.11875f, 0.0417872f)),
                    new(1.2f, new(0.15f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.72f, new(0.15f, 0f)), new(FarFront, 0f, 0.120000035f, new(-0.1f, 0f)), new(FarFront, 0.6f, 1.2f, new(0.4f, 0f)),
            new(NearHind, 0f, 0.120000035f, new(-0.1f, 0f)), new(NearHind, 0.6f, 1.2f, new(0.4f, 0f)), new(NearFront, 0f, 0.72f, new(0.15f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1.2f, new(0.5f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        QuadrupedBrake, QuadrupedHeadDown, QuadrupedIdle, QuadrupedRecover, QuadrupedRun, QuadrupedRush, QuadrupedScrape, QuadrupedWalk];
}
