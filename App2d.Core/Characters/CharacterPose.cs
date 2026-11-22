using App2d.Core.Animation;
using App2d.Core.Characters.Attachments;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace App2d.Core.Characters;

/// <summary>A second clip laid over the first on the joints, limbs and mounts of a mask: a sword swing over walking legs.</summary>
public readonly record struct PoseOverlay(SkeletonClip2D Clip, double Seconds, SkeletonMask2D Mask, float Weight = 1);

/// <summary>
/// A character in one pose, placed in the world. The joints are in the character's own space: facing +X, feet at the origin,
/// in the skeleton's units. <see cref="Position"/> and <see cref="Facing"/> say where that space is. Drawing, mounts and
/// colliders all read this one pose, so they cannot disagree. A pose is never changed once made.
/// </summary>
public sealed class CharacterPose
{
    private CharacterPose(Skeleton2D skeleton, ImmutableArray<RigidTransform2D> local, ImmutableArray<RigidTransform2D> world, ImmutableArray<float> layers, ImmutableArray<Vector3f> turns)
    {
        Skeleton = skeleton;
        Local = local;
        World = world;
        Layers = layers;
        Turns = turns;
    }

    public Skeleton2D Skeleton { get; }
    /// <summary>Each joint relative to its parent.</summary>
    public ImmutableArray<RigidTransform2D> Local { get; }
    /// <summary>Each joint in the character's own space.</summary>
    public ImmutableArray<RigidTransform2D> World { get; }
    /// <summary>Each joint's place in the drawing order; smaller is nearer the viewer.</summary>
    public ImmutableArray<float> Layers { get; }
    /// <summary>How far each mount is turned out of the plane.</summary>
    public ImmutableArray<Vector3f> Turns { get; }
    /// <summary>Where the feet are, in the skeleton's units.</summary>
    public Vector2f Position { get; init; }
    /// <summary>1 facing +X, -1 facing -X.</summary>
    public int Facing { get; init; } = 1;
    /// <summary>The expression the clip asked for at this moment; empty to hide the face, null when it asked for nothing.</summary>
    public string? Expression { get; init; }
    /// <summary>Whether the clip shows the character from behind at this moment.</summary>
    public bool SeenFromBehind { get; init; }

    public PosedSkeleton2D Posed => new(Skeleton, World.AsSpan(), Layers.AsSpan(), Turns.AsSpan());

    /// <summary>A point of the character's own space, in the world.</summary>
    public Vector2f Place(Vector2f point) => Position + new Vector2f(point.X * Facing, point.Y);

    /// <summary>A point of the world, in the character's own space.</summary>
    public Vector2f ToLocal(Vector2f point) => new((point.X - Position.X) * Facing, point.Y - Position.Y);

    /// <summary>A mount's frame in the character's own space, turned out of the plane as the clip turns it.</summary>
    public Frame3D MountFrame(int mount) => Posed.MountFrame(mount);

    /// <summary>A point given in a mount's own axes, such as a blade's tip, in the character's own space. The depth is dropped.</summary>
    public Vector2f MountPoint(int mount, Vector2f point) => MountFrame(mount).TransformPoint(new(point, 0)).XY;

    /// <summary>The character's own clock when the pose was made, for a face that blinks.</summary>
    public double Clock { get; init; }

    /// <summary>The same pose showing an expression at a moment of the character's clock.</summary>
    public CharacterPose Showing(string? expression, double clock) => new(Skeleton, Local, World, Layers, Turns) { Position = Position, Facing = Facing, Expression = expression, SeenFromBehind = SeenFromBehind, Clock = clock };

    /// <summary>The same pose somewhere else.</summary>
    public CharacterPose At(Vector2f position, int facing) => new(Skeleton, Local, World, Layers, Turns) { Position = position, Facing = facing, Expression = Expression, SeenFromBehind = SeenFromBehind, Clock = Clock };

    /// <summary>The skeleton at rest.</summary>
    public static CharacterPose Rest(Skeleton2D skeleton) => From(skeleton, skeleton.RestPose(), new float[skeleton.Count], new Vector3f[skeleton.Mounts.Length]);

    private static CharacterPose From(Skeleton2D skeleton, RigidTransform2D[] local, float[] keyed, Vector3f[] turns)
    {
        var world = new RigidTransform2D[skeleton.Count];
        var layers = new float[skeleton.Count];
        skeleton.ToWorld(local, world);
        skeleton.ToLayers(keyed, layers);
        // The arrays were made here and are never written again, so they are handed over as they are.
        return new(skeleton, ImmutableCollectionsMarshal.AsImmutableArray(local), ImmutableCollectionsMarshal.AsImmutableArray(world),
            ImmutableCollectionsMarshal.AsImmutableArray(layers), ImmutableCollectionsMarshal.AsImmutableArray(turns));
    }

    /// <summary>
    /// Samples a clip in place: its travel is left out, because whoever moves the character owns where it is. A looping clip
    /// repeats when <paramref name="repeat"/> is set; otherwise the clip holds its last moment.
    /// </summary>
    /// <param name="overlay">A second clip laid over the first before the limbs are placed, so a limb is never a mix of two solved poses.</param>
    /// <param name="plant">Given a limb and where the clip plants it, returns where it stays planted: a foot held where it landed.</param>
    /// <param name="reversed">Plays every sideways move and every turn the other way, for a hit taken from behind.</param>
    public static CharacterPose Sample(Skeleton2D skeleton, SkeletonClip2D clip, double seconds, bool repeat = false, PoseOverlay? overlay = null,
        Func<int, Vector2f, Vector2f>? plant = null, bool reversed = false)
    {
        var time = Time(clip, seconds, repeat && clip.Loop);
        var local = new RigidTransform2D[skeleton.Count];
        var keyed = new float[skeleton.Count];
        var turns = new Vector3f[skeleton.Mounts.Length];
        Span<LimbSample2D> limbs = stackalloc LimbSample2D[skeleton.Limbs.Length];
        clip.Sample(skeleton, time, local, limbs, reversed);
        clip.SampleLayers(time, keyed);
        clip.SampleMounts(time, turns, reversed);
        if (overlay is { Weight: > 0 } over)
        {
            var overTime = Time(over.Clip, over.Seconds, over.Clip.Loop);
            Span<RigidTransform2D> overLocal = stackalloc RigidTransform2D[skeleton.Count];
            Span<LimbSample2D> overLimbs = stackalloc LimbSample2D[skeleton.Limbs.Length];
            Span<float> overKeyed = stackalloc float[skeleton.Count];
            Span<Vector3f> overTurns = stackalloc Vector3f[skeleton.Mounts.Length];
            over.Clip.Sample(skeleton, overTime, overLocal, overLimbs, reversed);
            over.Clip.SampleLayers(overTime, overKeyed);
            over.Clip.SampleMounts(overTime, overTurns, reversed);
            SkeletonClip2D.Overlay(local, limbs, overLocal, overLimbs, over.Mask, over.Weight);
            SkeletonClip2D.OverlayLayers(keyed, overKeyed, over.Mask, over.Weight);
            SkeletonClip2D.OverlayMounts(turns, overTurns, over.Mask, over.Weight);
        }
        if (plant is not null)
        {
            // A limb an overlay has fully taken is not planted any more.
            for (var i = 0; i < limbs.Length; i++)
            {
                if (limbs[i] is { Planted: { } planted, Release: 0 }) limbs[i] = limbs[i] with { Planted = plant(i, planted) };
            }
        }
        SkeletonClip2D.Place(skeleton, local, limbs);
        var pose = From(skeleton, local, keyed, turns);
        return new(skeleton, pose.Local, pose.World, pose.Layers, pose.Turns) { Expression = clip.Expression?.Sample(time), SeenFromBehind = clip.SeenFromBehind(time) };
    }

    /// <summary>The time inside the clip: wrapped round a looping clip, held at the end of any other.</summary>
    public static float Time(SkeletonClip2D clip, double seconds, bool loop) =>
        (float)(loop && clip.Duration > 0 ? seconds % clip.Duration : Math.Clamp(seconds, 0, clip.Duration));
}
