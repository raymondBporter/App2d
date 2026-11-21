// Generated from the authored JSON clips by LegacyClipEmitter in App2d.Tests. While those clips exist, change them and
// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named GeneratedRigTests) instead of editing this file.
using App2d.Core.Animation;
using System.Collections.Immutable;
using static App2d.Core.Characters.StalkerSkeleton;

namespace App2d.Core.Characters;

/// <summary>The stalker animations, for <see cref="StalkerSkeleton"/>. Values are changes from rest, in the skeleton's own units.</summary>
public static class StalkerClips
{
    private static readonly Interpolator<Vector2f, float> Straight = Interpolation.Default<Vector2f, float>();
    private static readonly Interpolator<Vector2f, float> Smooth = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth);

    public static SkeletonClip2D StalkerDeath { get; } = new("stalker-death", 0.9f)
    {
        Joints =
        [
            new(Body,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.12f, new(-0.06f, 0.04f)), new(0.6f, new(-0.08f, -0.5f)), new(0.9f, new(-0.08f, -0.46f)))),
        ],
        Contacts = [new(Foot0Chain, 0f, 0.9f, new(0f, 0f)), new(Foot1Chain, 0f, 0.9f, new(0f, 0f)), new(Foot2Chain, 0f, 0.9f, new(0f, 0f))],
        Markers = [new("impact", 0.6f)],
    };

    public static SkeletonClip2D StalkerHit { get; } = new("stalker-hit", 0.3f)
    {
        Joints =
        [
            new(Body, Move: Track.Of<Vector2f>(new(0f, new(0f, 0f), Smooth), new(0.07f, new(-0.1f, -0.08f)), new(0.3f, new(0f, 0f)))),
        ],
        Contacts = [new(Foot0Chain, 0f, 0.3f, new(0f, 0f)), new(Foot1Chain, 0f, 0.3f, new(0f, 0f)), new(Foot2Chain, 0f, 0.3f, new(0f, 0f))],
    };

    public static SkeletonClip2D StalkerIdle { get; } = new("stalker-idle", 1.5f, loop: true)
    {
        Joints =
        [
            new(Body, Move: new(Smooth, new(0f, new(0f, 0f)), new(0.75f, new(0f, -0.03f)), new(1.5f, new(0f, 0f)))),
        ],
        Contacts = [new(Foot0Chain, 0f, 1.5f, new(0f, 0f)), new(Foot1Chain, 0f, 1.5f, new(0f, 0f)), new(Foot2Chain, 0f, 1.5f, new(0f, 0f))],
    };

    public static SkeletonClip2D StalkerLunge { get; } = new("stalker-lunge", 0.8f)
    {
        Joints =
        [
            new(Body,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.22f, new(-0.1f, 0.06f)), new(0.32f, new(0.24f, -0.06f), Straight), new(0.5f, new(0.24f, -0.06f)),
                    new(0.8f, new(0f, 0f)))),
        ],
        Contacts = [new(Foot0Chain, 0f, 0.8f, new(0f, 0f)), new(Foot1Chain, 0f, 0.8f, new(0f, 0f)), new(Foot2Chain, 0f, 0.8f, new(0f, 0f))],
        Markers = [new("windup", 0.05f), new("strike", 0.3f), new("recover", 0.5f)],
    };

    public static SkeletonClip2D StalkerWalk { get; } = new("stalker-walk", 1f, loop: true)
    {
        Joints =
        [
            new(Body,
                Move: new(Smooth,
                    new(0f, new(0f, 0f)), new(0.25f, new(0f, 0.025f)), new(0.5f, new(0f, 0f)), new(0.75f, new(0f, 0.025f)), new(1f, new(0f, 0f)))),
        ],
        Limbs =
        [
            new(Foot0Chain,
                Target: Track.Of<Vector2f>(new(0f, new(0.15f, 0f)), new(0.5f, new(-0.15f, 0f)), new(0.75f, new(0f, 0.12f)), new(1f, new(0.15f, 0f)))),
            new(Foot1Chain,
                Target: Track.Of<Vector2f>(
                    new(0f, new(-0.15f, 0f)), new(0.25f, new(0f, 0.12f)), new(0.5f, new(0.15f, 0f)), new(1f, new(-0.15f, 0f)))),
            new(Foot2Chain,
                Target: Track.Of<Vector2f>(new(0f, new(0.15f, 0f)), new(0.5f, new(-0.15f, 0f)), new(0.75f, new(0f, 0.12f)), new(1f, new(0.15f, 0f)))),
        ],
        Contacts = [new(Foot0Chain, 0f, 0.5f, new(0.15f, 0f)), new(Foot2Chain, 0f, 0.5f, new(0.15f, 0f)), new(Foot1Chain, 0.5f, 1f, new(0.45f, 0f))],
        Markers = [new("step", 0f), new("step-middle", 0.5f)],
        Travel = Track.Of<Vector2f>(new(0f, new(0f, 0f)), new(1f, new(0.6f, 0f))),
    };

    public static ImmutableArray<SkeletonClip2D> All { get; } = [StalkerDeath, StalkerHit, StalkerIdle, StalkerLunge, StalkerWalk];
}
