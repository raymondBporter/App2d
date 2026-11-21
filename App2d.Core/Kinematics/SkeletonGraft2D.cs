using App2d.Core.Mathematics.Transforms;
using System.Collections.Immutable;

namespace App2d.Core.Kinematics;

/// <summary>
/// One skeleton with part of another attached to it: a horse's body with a person from the waist up where its neck was.
/// Branches are cut off either skeleton by naming the joint each starts at. The guest's root is then fixed to a joint of the
/// host, and the guest is scaled about that root. The maps give each original joint, limb and mount its number in the new
/// skeleton, or <see cref="Gone"/>, so clips and drawn pieces made for either skeleton can be renumbered for this one.
/// </summary>
public sealed class SkeletonGraft2D
{
    /// <summary>The number of a joint, limb or mount that was cut off.</summary>
    public const int Gone = -1;

    /// <param name="host">The skeleton that carries the other. Its travel scale is kept.</param>
    /// <param name="hostCut">The joints whose branches are cut off the host.</param>
    /// <param name="guest">The skeleton attached. Its first joint must be its only root.</param>
    /// <param name="guestCut">The joints whose branches are cut off the guest.</param>
    /// <param name="onto">The host joint the guest's root is attached to.</param>
    /// <param name="at">Where the guest's root rests, in the axes of <paramref name="onto"/>.</param>
    /// <param name="scale">The guest's size; 1 keeps it.</param>
    /// <param name="layer">How far the guest sits in front of (negative) or behind the joint it is attached to.</param>
    public SkeletonGraft2D(Skeleton2D host, ReadOnlySpan<int> hostCut, Skeleton2D guest, ReadOnlySpan<int> guestCut, int onto, RigidTransform2D at, float scale = 1, float layer = 0)
    {
        var joints = new List<Joint2D>();
        var hostJoints = Keep(host, hostCut, 0);
        var guestJoints = Keep(guest, guestCut, hostJoints.Count(j => j != Gone));
        if (hostJoints[onto] == Gone) throw new ArgumentException("The guest cannot be attached to a joint that was cut off.", nameof(onto));
        for (var i = 0; i < host.Count; i++)
        {
            if (hostJoints[i] == Gone) continue;
            var joint = host.Joints[i];
            joints.Add(joint with { Parent = joint.Parent == Skeleton2D.NoParent ? Skeleton2D.NoParent : hostJoints[joint.Parent] });
        }
        for (var i = 0; i < guest.Count; i++)
        {
            if (guestJoints[i] == Gone) continue;
            var joint = guest.Joints[i];
            if (joint.Parent == Skeleton2D.NoParent && i != 0) throw new ArgumentException("The guest must have one root, and it must be its first joint.", nameof(guest));
            joints.Add(i == 0
                ? joint with { Parent = hostJoints[onto], Rest = at, MoveScale = joint.MoveScale * scale, Layer = joint.Layer + layer }
                : joint with { Parent = guestJoints[joint.Parent], Rest = new(joint.Rest.Translation * scale, joint.Rest.Rotation), MoveScale = joint.MoveScale * scale });
        }

        var limbs = new List<Limb2D>();
        var mounts = new List<Mount2D>();
        HostJoints = [.. hostJoints];
        GuestJoints = [.. guestJoints];
        HostLimbs = Limbs(host, hostJoints, 1, limbs);
        GuestLimbs = Limbs(guest, guestJoints, scale, limbs);
        HostMounts = Mounts(host, hostJoints, 1, mounts);
        GuestMounts = Mounts(guest, guestJoints, scale, mounts);
        Skeleton = new(joints.ToArray()) { Limbs = [.. limbs], Mounts = [.. mounts], TravelScale = host.TravelScale };
    }

    public Skeleton2D Skeleton { get; }
    public ImmutableArray<int> HostJoints { get; }
    public ImmutableArray<int> HostLimbs { get; }
    public ImmutableArray<int> HostMounts { get; }
    public ImmutableArray<int> GuestJoints { get; }
    public ImmutableArray<int> GuestLimbs { get; }
    public ImmutableArray<int> GuestMounts { get; }

    /// <summary>Each joint's new number, counting on from <paramref name="first"/>; a joint under a cut is gone.</summary>
    private static int[] Keep(Skeleton2D skeleton, ReadOnlySpan<int> cut, int first)
    {
        var map = new int[skeleton.Count];
        for (var i = 0; i < map.Length; i++)
        {
            var parent = skeleton.Joints[i].Parent;
            map[i] = cut.Contains(i) || parent != Skeleton2D.NoParent && map[parent] == Gone ? Gone : first++;
        }
        return map;
    }

    private static int Renumber(int joint, int[] joints) => joint == Skeleton2D.NoParent ? Skeleton2D.NoParent : joints[joint];

    /// <summary>A limb or mount stays when every joint it names stays.</summary>
    private static bool Stays(int[] joints, params ReadOnlySpan<int> named)
    {
        foreach (var joint in named)
        {
            if (joint != Skeleton2D.NoParent && joints[joint] == Gone) return false;
        }
        return true;
    }

    private static ImmutableArray<int> Limbs(Skeleton2D skeleton, int[] joints, float scale, List<Limb2D> limbs)
    {
        var map = new int[skeleton.Limbs.Length];
        for (var i = 0; i < map.Length; i++)
        {
            var limb = skeleton.Limbs[i];
            map[i] = Stays(joints, limb.End, limb.Frame) ? limbs.Count : Gone;
            if (map[i] != Gone) limbs.Add(new(joints[limb.End], limb.Bend, Renumber(limb.Frame, joints), limb.TargetScale * scale));
        }
        return [.. map];
    }

    private static ImmutableArray<int> Mounts(Skeleton2D skeleton, int[] joints, float scale, List<Mount2D> mounts)
    {
        var map = new int[skeleton.Mounts.Length];
        for (var i = 0; i < map.Length; i++)
        {
            var mount = skeleton.Mounts[i];
            map[i] = Stays(joints, mount.At, mount.Turn, mount.Toward) ? mounts.Count : Gone;
            if (map[i] != Gone)
            {
                mounts.Add(new(mount.Name, joints[mount.At], Renumber(mount.Turn, joints), Renumber(mount.Toward, joints),
                    new(mount.Local.Translation * scale, mount.Local.Rotation), mount.Layer));
            }
        }
        return [.. map];
    }
}
