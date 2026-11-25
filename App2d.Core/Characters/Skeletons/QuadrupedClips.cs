using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.Skeletons.QuadrupedSkeleton;

namespace App2d.Core.Characters.Skeletons;

/// <summary>The quadruped animations, for <see cref="Skeleton"/>. Values are changes from rest, in the skeleton's own units.</summary>
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
                    new(0f, new(0f, 0f)), new(0.0813f, new(0f, 0.025f)), new(0.2437f, new(0f, -0.025f)), new(0.4062f, new(0f, 0.025f)),
                    new(0.5687f, new(0f, -0.025f)), new(0.65f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.325f, -0.04f), new(0.65f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.325f, -0.12f), new(0.65f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.21f, 0f)), new(0.39f, new(-0.21f, 0f)), new(0.4062f, new(-0.1837f, 0.0371f)), new(0.4333f, new(-0.14f, 0.095f)),
                    new(0.4604f, new(-0.0963f, 0.1429f)), new(0.4875f, new(-0.0525f, 0.1755f)), new(0.5146f, new(-0.0087f, 0.1896f)),
                    new(0.5417f, new(0.035f, 0.1835f)), new(0.5687f, new(0.0788f, 0.158f)), new(0.5958f, new(0.1225f, 0.1157f)),
                    new(0.6229f, new(0.1663f, 0.0611f)), new(0.65f, new(0.21f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.14f, 0f)), new(0.065f, new(-0.21f, 0f)), new(0.0813f, new(-0.1837f, 0.0371f)), new(0.1083f, new(-0.14f, 0.095f)),
                    new(0.1354f, new(-0.0963f, 0.1429f)), new(0.1625f, new(-0.0525f, 0.1755f)), new(0.1896f, new(-0.0088f, 0.1896f)),
                    new(0.2167f, new(0.035f, 0.1835f)), new(0.2437f, new(0.0788f, 0.158f)), new(0.2708f, new(0.1225f, 0.1157f)),
                    new(0.2979f, new(0.1663f, 0.0611f)), new(0.325f, new(0.21f, 0f)), new(0.65f, new(-0.14f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.14f, 0f)), new(0.065f, new(-0.21f, 0f)), new(0.0813f, new(-0.1837f, 0.0371f)), new(0.1083f, new(-0.14f, 0.095f)),
                    new(0.1354f, new(-0.0963f, 0.1429f)), new(0.1625f, new(-0.0525f, 0.1755f)), new(0.1896f, new(-0.0088f, 0.1896f)),
                    new(0.2167f, new(0.035f, 0.1835f)), new(0.2437f, new(0.0788f, 0.158f)), new(0.2708f, new(0.1225f, 0.1157f)),
                    new(0.2979f, new(0.1663f, 0.0611f)), new(0.325f, new(0.21f, 0f)), new(0.65f, new(-0.14f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.21f, 0f)), new(0.39f, new(-0.21f, 0f)), new(0.4062f, new(-0.1837f, 0.0371f)), new(0.4333f, new(-0.14f, 0.095f)),
                    new(0.4604f, new(-0.0963f, 0.1429f)), new(0.4875f, new(-0.0525f, 0.1755f)), new(0.5146f, new(-0.0087f, 0.1896f)),
                    new(0.5417f, new(0.035f, 0.1835f)), new(0.5687f, new(0.0788f, 0.158f)), new(0.5958f, new(0.1225f, 0.1157f)),
                    new(0.6229f, new(0.1663f, 0.0611f)), new(0.65f, new(0.21f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.39f, new(0.21f, 0f)), new(FarFront, 0f, 0.065f, new(-0.14f, 0f)),
            new(FarFront, 0.325f, 0.65f, new(0.56f, 0f)), new(NearHind, 0f, 0.065f, new(-0.14f, 0f)),
            new(NearHind, 0.325f, 0.65f, new(0.56f, 0f)), new(NearFront, 0f, 0.39f, new(0.21f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.65f, new(0.7f, 0f))),
    };

    public static SkeletonClip2D QuadrupedRush { get; } = new("quadruped-rush", 0.48f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: Track.Of<Vector2f>(
                    new(0f, new(0f, -0.055f)), new(0.06f, new(0f, -0.03f)), new(0.18f, new(0f, -0.08f)), new(0.3f, new(0f, -0.03f)),
                    new(0.42f, new(0f, -0.08f)), new(0.48f, new(0f, -0.055f)))),
            new(Head, Turn: Track.Linear(new(0f, -0.32f), new(0.24f, -0.36f), new(0.48f, -0.32f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.24f, -0.12f), new(0.48f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.23f, 0f)), new(0.288f, new(-0.23f, 0f)), new(0.3f, new(-0.2013f, 0.0293f)), new(0.32f, new(-0.1533f, 0.075f)),
                    new(0.34f, new(-0.1054f, 0.1128f)), new(0.36f, new(-0.0575f, 0.1386f)), new(0.38f, new(-0.0096f, 0.1497f)),
                    new(0.4f, new(0.0383f, 0.1449f)), new(0.42f, new(0.0862f, 0.1247f)), new(0.44f, new(0.1342f, 0.0913f)),
                    new(0.46f, new(0.1821f, 0.0482f)), new(0.48f, new(0.23f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1533f, 0f)), new(0.048f, new(-0.23f, 0f)), new(0.06f, new(-0.2013f, 0.0293f)), new(0.08f, new(-0.1533f, 0.075f)),
                    new(0.1f, new(-0.1054f, 0.1128f)), new(0.12f, new(-0.0575f, 0.1386f)), new(0.14f, new(-0.0096f, 0.1497f)),
                    new(0.16f, new(0.0383f, 0.1449f)), new(0.18f, new(0.0862f, 0.1247f)), new(0.2f, new(0.1342f, 0.0913f)),
                    new(0.22f, new(0.1821f, 0.0482f)), new(0.24f, new(0.23f, 0f)), new(0.48f, new(-0.1533f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1533f, 0f)), new(0.048f, new(-0.23f, 0f)), new(0.06f, new(-0.2013f, 0.0293f)), new(0.08f, new(-0.1533f, 0.075f)),
                    new(0.1f, new(-0.1054f, 0.1128f)), new(0.12f, new(-0.0575f, 0.1386f)), new(0.14f, new(-0.0096f, 0.1497f)),
                    new(0.16f, new(0.0383f, 0.1449f)), new(0.18f, new(0.0862f, 0.1247f)), new(0.2f, new(0.1342f, 0.0913f)),
                    new(0.22f, new(0.1821f, 0.0482f)), new(0.24f, new(0.23f, 0f)), new(0.48f, new(-0.1533f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.23f, 0f)), new(0.288f, new(-0.23f, 0f)), new(0.3f, new(-0.2013f, 0.0293f)), new(0.32f, new(-0.1533f, 0.075f)),
                    new(0.34f, new(-0.1054f, 0.1128f)), new(0.36f, new(-0.0575f, 0.1386f)), new(0.38f, new(-0.0096f, 0.1497f)),
                    new(0.4f, new(0.0383f, 0.1449f)), new(0.42f, new(0.0862f, 0.1247f)), new(0.44f, new(0.1342f, 0.0913f)),
                    new(0.46f, new(0.1821f, 0.0482f)), new(0.48f, new(0.23f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.288f, new(0.23f, 0f)), new(FarFront, 0f, 0.048f, new(-0.1533f, 0f)),
            new(FarFront, 0.24f, 0.48f, new(0.6133f, 0f)), new(NearHind, 0f, 0.048f, new(-0.1533f, 0f)),
            new(NearHind, 0.24f, 0.48f, new(0.6133f, 0f)), new(NearFront, 0f, 0.288f, new(0.23f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.48f, new(0.7667f, 0f))),
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
                    new(0f, new(0f, 0f)), new(0.15f, new(0f, 0.025f)), new(0.45f, new(0f, -0.025f)), new(0.75f, new(0f, 0.025f)),
                    new(1.05f, new(0f, -0.025f)), new(1.2f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, 0f), new(0.6f, -0.04f), new(1.2f, 0f))),
            new(Tail, Turn: Track.Linear(new(0f, 0.05f), new(0.6f, -0.12f), new(1.2f, 0.05f))),
        ],
        Limbs =
        [
            new(FarHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.15f, 0f)), new(0.72f, new(-0.15f, 0f)), new(0.75f, new(-0.1313f, 0.0254f)), new(0.8f, new(-0.1f, 0.065f)),
                    new(0.85f, new(-0.0688f, 0.0977f)), new(0.9f, new(-0.0375f, 0.1201f)), new(0.95f, new(-0.0062f, 0.1297f)),
                    new(1f, new(0.025f, 0.1256f)), new(1.05f, new(0.0563f, 0.1081f)), new(1.1f, new(0.0875f, 0.0791f)),
                    new(1.15f, new(0.1187f, 0.0418f)), new(1.2f, new(0.15f, 0f)))),
            new(FarFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.12f, new(-0.15f, 0f)), new(0.15f, new(-0.1313f, 0.0254f)), new(0.2f, new(-0.1f, 0.065f)),
                    new(0.25f, new(-0.0688f, 0.0977f)), new(0.3f, new(-0.0375f, 0.1201f)), new(0.35f, new(-0.0063f, 0.1297f)),
                    new(0.4f, new(0.025f, 0.1256f)), new(0.45f, new(0.0563f, 0.1081f)), new(0.5f, new(0.0875f, 0.0791f)),
                    new(0.55f, new(0.1187f, 0.0418f)), new(0.6f, new(0.15f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(NearHind,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.1f, 0f)), new(0.12f, new(-0.15f, 0f)), new(0.15f, new(-0.1313f, 0.0254f)), new(0.2f, new(-0.1f, 0.065f)),
                    new(0.25f, new(-0.0688f, 0.0977f)), new(0.3f, new(-0.0375f, 0.1201f)), new(0.35f, new(-0.0063f, 0.1297f)),
                    new(0.4f, new(0.025f, 0.1256f)), new(0.45f, new(0.0563f, 0.1081f)), new(0.5f, new(0.0875f, 0.0791f)),
                    new(0.55f, new(0.1187f, 0.0418f)), new(0.6f, new(0.15f, 0f)), new(1.2f, new(-0.1f, 0f)))),
            new(NearFront,
                Target: Track.Of<Vector2f>(
                    new(0f, new(0.15f, 0f)), new(0.72f, new(-0.15f, 0f)), new(0.75f, new(-0.1313f, 0.0254f)), new(0.8f, new(-0.1f, 0.065f)),
                    new(0.85f, new(-0.0688f, 0.0977f)), new(0.9f, new(-0.0375f, 0.1201f)), new(0.95f, new(-0.0062f, 0.1297f)),
                    new(1f, new(0.025f, 0.1256f)), new(1.05f, new(0.0563f, 0.1081f)), new(1.1f, new(0.0875f, 0.0791f)),
                    new(1.15f, new(0.1187f, 0.0418f)), new(1.2f, new(0.15f, 0f)))),
        ],
        Contacts = [
            new(FarHind, 0f, 0.72f, new(0.15f, 0f)), new(FarFront, 0f, 0.12f, new(-0.1f, 0f)), new(FarFront, 0.6f, 1.2f, new(0.4f, 0f)),
            new(NearHind, 0f, 0.12f, new(-0.1f, 0f)), new(NearHind, 0.6f, 1.2f, new(0.4f, 0f)), new(NearFront, 0f, 0.72f, new(0.15f, 0f))],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1.2f, new(0.5f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        QuadrupedBrake, QuadrupedHeadDown, QuadrupedIdle, QuadrupedRecover, QuadrupedRun, QuadrupedRush, QuadrupedScrape, QuadrupedWalk];
}
