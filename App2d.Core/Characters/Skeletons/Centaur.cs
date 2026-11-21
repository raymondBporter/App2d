using App2d.Core.Animation;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>
/// A centaur: the quadruped without its head, and a person from the waist up, at nine tenths of the size, where its neck was.
/// It has no joints, art or clips of its own. Each half plays a clip made for the skeleton it came from: the quadruped's clip
/// owns the body, legs and travel, and the person's clip is laid over the rider's joints.
/// </summary>
public static class Centaur
{
    /// <summary>How the two skeletons are joined, and where each one's joints, limbs and mounts went.</summary>
    public static SkeletonGraft2D Graft { get; } = new(QuadrupedSkeleton.Skeleton, [QuadrupedSkeleton.Head], PersonSkeleton.Skeleton,
        [PersonSkeleton.LeftHip, PersonSkeleton.RightHip], QuadrupedSkeleton.Body, new(new(0.42f, 0.395f)), 0.9f, -0.05f);

    public static Skeleton2D Skeleton => Graft.Skeleton;

    /// <summary>The quadruped's body, then the person's from the waist up.</summary>
    public static ImmutableArray<Attachment> Body { get; } =
    [
        .. Attachment.Renumbered(QuadrupedBodies.Quadruped, Graft.HostJoints, Graft.HostMounts),
        .. Attachment.Renumbered(PersonBody.Standard, Graft.GuestJoints, Graft.GuestMounts, 0.9f),
    ];

    /// <summary>What the rider's clip owns: the person's upper body.</summary>
    public static SkeletonMask2D Rider { get; } = PersonSkeleton.UpperBody.Renumbered(Graft.GuestJoints, Graft.GuestLimbs, Graft.GuestMounts);

    /// <summary>A quadruped clip, for the centaur's body and legs.</summary>
    public static SkeletonClip2D Horse(SkeletonClip2D quadruped) => quadruped.Renumbered(Graft.HostJoints, Graft.HostLimbs, Graft.HostMounts);

    /// <summary>A person clip, for the rider. It keeps what it keys from the chest up; the horse owns the ground.</summary>
    public static SkeletonClip2D Person(SkeletonClip2D person) => person.Renumbered(Graft.GuestJoints, Graft.GuestLimbs, Graft.GuestMounts, rides: true);

    /// <summary>Poses the centaur: the horse's clip, with the rider's laid over the rider's joints, each at its own time.</summary>
    public static void Pose(SkeletonClip2D horse, float horseTime, SkeletonClip2D rider, float riderTime, Span<RigidTransform2D> local)
    {
        Span<LimbSample2D> limbs = stackalloc LimbSample2D[Skeleton.Limbs.Length];
        Span<LimbSample2D> riderLimbs = stackalloc LimbSample2D[Skeleton.Limbs.Length];
        Span<RigidTransform2D> riderLocal = stackalloc RigidTransform2D[Skeleton.Count];
        horse.Sample(Skeleton, horseTime, local, limbs);
        rider.Sample(Skeleton, riderTime, riderLocal, riderLimbs);
        SkeletonClip2D.Overlay(local, limbs, riderLocal, riderLimbs, Rider);
        SkeletonClip2D.Place(Skeleton, local, limbs);
    }
}
