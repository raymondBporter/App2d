using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.PterosaurSkeleton;

namespace App2d.Core.Characters;

/// <summary>The pterosaur animations, for <see cref="PterosaurSkeleton"/>. Values are changes from rest, in the skeleton's own units.</summary>
public static class PterosaurClips
{

    public static SkeletonClip2D PterosaurBrake { get; } = new("pterosaur-brake", 0.32f)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, -0.65f), new(0.16f, -0.12f), new(0.32f, 0f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, -0.15f), new(0.16f, 0.1f), new(0.32f, 0.28f))),
            new(NearBend, Turn: Track.Linear(new(0f, -1.1f), new(0.16f, -0.3f), new(0.32f, 0f))),
            new(NearLeg, Turn: Track.Linear(new(0f, 0.6f), new(0.16f, 0.3f), new(0.32f, 0.1f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, 1.3f), new(0.16f, 0.4f), new(0.32f, -0.28f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0.1f), new(0.32f, 0f))),
            new(FarLeg, Turn: Track.Linear(new(0f, 0.6f), new(0.16f, 0.3f), new(0.32f, 0.1f))),
        ],
        Markers = [new("open", 0.16f)],
    };

    public static SkeletonClip2D PterosaurDeath { get; } = new("pterosaur-death", 0.8f)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, 0f), new(0.24f, 1.8f), new(0.8f, 1.8f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, 0f), new(0.24f, -0.8f), new(0.8f, -0.8f))),
            new(NearBend, Turn: Track.Linear(new(0f, 0f), new(0.24f, -0.5f), new(0.8f, -0.5f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, 0f), new(0.24f, 0.8f), new(0.8f, 0.8f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0f), new(0.24f, 0.5f), new(0.8f, 0.5f))),
        ],
    };

    public static SkeletonClip2D PterosaurDive { get; } = new("pterosaur-dive", 0.65f)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, -0.4f), new(0.65f, -0.65f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, -0.15f), new(0.65f, -0.15f))),
            new(NearBend, Turn: Track.Linear(new(0f, -1.1f), new(0.65f, -1.1f))),
            new(NearLeg, Turn: Track.Linear(new(0f, 0.6f), new(0.65f, 0.6f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, 1.3f), new(0.65f, 1.3f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0.1f), new(0.65f, 0.1f))),
            new(FarLeg, Turn: Track.Linear(new(0f, 0.6f), new(0.65f, 0.6f))),
        ],
    };

    public static SkeletonClip2D PterosaurFold { get; } = new("pterosaur-fold", 0.48f)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, 0f), new(0.24f, -0.22f), new(0.48f, -0.4f))),
            new(Jaw, Turn: Track.Linear(new(0f, 0f), new(0.24f, -0.5f), new(0.48f, 0f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, -0.22f), new(0.24f, -0.15f), new(0.48f, -0.15f))),
            new(NearBend, Turn: Track.Linear(new(0f, 0f), new(0.24f, -1.1f), new(0.48f, -1.1f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, 0.22f), new(0.24f, 1.3f), new(0.48f, 1.3f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0f), new(0.24f, 0.1f), new(0.48f, 0.1f))),
        ],
        Markers = [new("screech", 0.2f), new("commit", 0.4f)],
    };

    public static SkeletonClip2D PterosaurHit { get; } = new("pterosaur-hit", 0.45f)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, 0f), new(0.135f, 0.3f), new(0.45f, 0f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, 0f), new(0.135f, -0.8f), new(0.45f, 0f))),
            new(NearBend, Turn: Track.Linear(new(0f, 0f), new(0.135f, -0.5f), new(0.45f, 0f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, 0f), new(0.135f, 0.8f), new(0.45f, 0f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0f), new(0.135f, 0.5f), new(0.45f, 0f))),
        ],
    };

    public static SkeletonClip2D PterosaurHover { get; } = new("pterosaur-hover", 1f, loop: true)
    {
        Joints =
        [
            new(Body, Move: Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(0.5f, new(0f, 0.05f)), new(1f, new(0f, 0f)))),
            new(Head, Turn: Track.Linear(new(0f, -0.08f), new(0.5f, -0.12f), new(1f, -0.08f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, -0.22f), new(0.5f, 0.28f), new(1f, -0.22f))),
            new(NearBend, Turn: Track.Linear(new(0f, 0f), new(0.5f, -0.1f), new(1f, 0f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, 0.22f), new(0.5f, -0.28f), new(1f, 0.22f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0f), new(0.5f, 0.1f), new(1f, 0f))),
        ],
    };

    public static SkeletonClip2D PterosaurLowRecovery { get; } = new("pterosaur-low-recovery", 1f, loop: true)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, 0f), new(0.5f, -0.02f), new(1f, 0f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, 0.28f), new(0.5f, 0.12f), new(1f, 0.28f))),
            new(NearBend, Turn: Track.Linear(new(0f, 0f), new(0.5f, -0.08f), new(1f, 0f))),
            new(NearLeg, Turn: Track.Linear(new(0f, 0.1f), new(0.5f, 0.15f), new(1f, 0.1f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, -0.28f), new(0.5f, -0.12f), new(1f, -0.28f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0f), new(0.5f, 0.08f), new(1f, 0f))),
            new(FarLeg, Turn: Track.Linear(new(0f, 0.1f), new(0.5f, 0.15f), new(1f, 0.1f))),
        ],
    };

    public static SkeletonClip2D PterosaurReset { get; } = new("pterosaur-reset", 1.2f)
    {
        Joints =
        [
            new(Body, Turn: Track.Linear(new(0f, 0f), new(0.4f, 0.12f), new(1.2f, 0f))),
            new(NearShoulder, Turn: Track.Linear(new(0f, 0.28f), new(0.4f, -0.22f), new(0.8f, 0.28f), new(1.2f, -0.22f))),
            new(NearBend, Turn: Track.Linear(new(0f, 0f), new(0.6f, -0.08f), new(1.2f, 0f))),
            new(FarShoulder, Turn: Track.Linear(new(0f, -0.28f), new(0.4f, 0.22f), new(0.8f, -0.28f), new(1.2f, 0.22f))),
            new(FarBend, Turn: Track.Linear(new(0f, 0f), new(0.6f, 0.08f), new(1.2f, 0f))),
        ],
    };

    public static SkeletonClip2D PterosaurSwoop { get; } = new("pterosaur-swoop", 3.65f)
    {
        Joints =
        [
            new(Body,
                Turn: Track.Linear(
                    new(0f, 0f), new(0.24f, -0.22f), new(0.48f, -0.4f), new(1.13f, -0.65f), new(1.29f, -0.12f), new(1.45f, 0f), new(1.95f, -0.02f),
                    new(2.45f, 0f), new(2.85f, 0.12f), new(3.65f, 0f))),
            new(Jaw,
                Turn: Track.Linear(new(0f, 0f), new(0.24f, -0.5f), new(0.48f, 0f), new(3.65f, 0f))),
            new(NearShoulder,
                Turn: Track.Linear(
                    new(0f, -0.22f), new(0.24f, -0.15f), new(1.13f, -0.15f), new(1.29f, 0.1f), new(1.45f, 0.28f), new(1.95f, 0.12f),
                    new(2.45f, 0.28f), new(2.85f, -0.22f), new(3.25f, 0.28f), new(3.65f, -0.22f))),
            new(NearBend,
                Turn: Track.Linear(
                    new(0f, 0f), new(0.24f, -1.1f), new(1.13f, -1.1f), new(1.29f, -0.3f), new(1.45f, 0f), new(1.95f, -0.08f), new(2.45f, 0f),
                    new(3.05f, -0.08f), new(3.65f, 0f))),
            new(NearLeg,
                Turn: Track.Linear(
                    new(0f, 0f), new(0.48f, 0.6f), new(1.13f, 0.6f), new(1.29f, 0.3f), new(1.45f, 0.1f), new(1.95f, 0.15f), new(2.45f, 0f),
                    new(3.65f, 0f))),
            new(FarShoulder,
                Turn: Track.Linear(
                    new(0f, 0.22f), new(0.24f, 1.3f), new(1.13f, 1.3f), new(1.29f, 0.4f), new(1.45f, -0.28f), new(1.95f, -0.12f), new(2.45f, -0.28f),
                    new(2.85f, 0.22f), new(3.25f, -0.28f), new(3.65f, 0.22f))),
            new(FarBend,
                Turn: Track.Linear(
                    new(0f, 0f), new(0.24f, 0.1f), new(1.13f, 0.1f), new(1.45f, 0f), new(1.95f, 0.08f), new(2.45f, 0f), new(3.05f, 0.08f),
                    new(3.65f, 0f))),
            new(FarLeg,
                Turn: Track.Linear(
                    new(0f, 0f), new(0.48f, 0.6f), new(1.13f, 0.6f), new(1.29f, 0.3f), new(1.45f, 0.1f), new(1.95f, 0.15f), new(2.45f, 0f),
                    new(3.65f, 0f))),
        ],
        Markers = [new("screech", 0.2f), new("commit", 0.4f), new("rush", 0.48f), new("brake", 1.13f), new("low", 1.45f), new("reset", 2.45f)],
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [
        PterosaurBrake, PterosaurDeath, PterosaurDive, PterosaurFold, PterosaurHit, PterosaurHover, PterosaurLowRecovery, PterosaurReset,
        PterosaurSwoop];
}
