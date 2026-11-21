using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Collections.Immutable;

namespace App2d.Core.Kinematics;

/// <summary>
/// A tree of joints. A pose is one <see cref="RigidTransform2D"/> per joint, in its parent's axes, indexed like
/// <see cref="Joints"/>. Anything may write a pose (keys, IK, physics); the skeleton only says how the joints hang together.
/// </summary>
public sealed class Skeleton2D
{
    /// <summary>The <see cref="Joint2D.Parent"/> of a joint that hangs from nothing.</summary>
    public const int NoParent = -1;

    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);

    /// <summary>Joints in order, every parent before its children.</summary>
    public Skeleton2D(params ReadOnlySpan<Joint2D> joints)
    {
        for (var i = 0; i < joints.Length; i++)
        {
            Ensure.ThrowIf(joints[i].Parent < NoParent || joints[i].Parent >= i, $"Joint '{joints[i].Name}' must come after its parent.", nameof(joints));
            Ensure.ThrowIf(!_indices.TryAdd(joints[i].Name, i), $"Joint '{joints[i].Name}' appears twice.", nameof(joints));
        }
        Joints = [.. joints];
        var rest = new RigidTransform2D[Count];
        ToWorld(RestPose(), rest);
        RestWorld = [.. rest];
        var layers = new float[Count];
        for (var i = 0; i < Count; i++) layers[i] = (joints[i].Parent == NoParent ? 0 : layers[joints[i].Parent]) + joints[i].Layer;
        RestLayers = [.. layers];
    }

    public ImmutableArray<Joint2D> Joints { get; }
    /// <summary>Each joint's rest transform in the skeleton's own space.</summary>
    public ImmutableArray<RigidTransform2D> RestWorld { get; }
    /// <summary>Each joint's place in the drawing order at rest: its own layer added to its parent's.</summary>
    public ImmutableArray<float> RestLayers { get; }
    /// <summary>The limbs IK places. Limb goals are given in this order.</summary>
    public ImmutableArray<Limb2D> Limbs { get; init; } = [];
    /// <summary>The places things can be attached.</summary>
    public ImmutableArray<Mount2D> Mounts { get; init; } = [];
    /// <summary>Multiplies a clip's travel: a build with a longer stride than the one the clips were made for covers more ground.</summary>
    public float TravelScale { get; init; } = 1;
    public int Count => Joints.Length;
    public int IndexOf(string name) => _indices[name];

    /// <summary>A new pose with every joint at rest.</summary>
    public RigidTransform2D[] RestPose()
    {
        var local = new RigidTransform2D[Count];
        for (var i = 0; i < local.Length; i++) local[i] = Joints[i].Rest;
        return local;
    }

    /// <summary>Forward kinematics: each joint's transform in the skeleton's own space.</summary>
    public void ToWorld(ReadOnlySpan<RigidTransform2D> local, Span<RigidTransform2D> world)
    {
        for (var i = 0; i < Count; i++)
        {
            var parent = Joints[i].Parent;
            world[i] = parent == NoParent ? local[i] : local[i].Then(world[parent]);
        }
    }

    /// <summary>
    /// Each joint's place in the drawing order: its own layer and any change a clip keyed, added to its parent's.
    /// <paramref name="keyed"/> is one change per joint, or empty for none. A limb's end takes its layer from the limb's frame,
    /// as its position does: a hand follows its shoulder, and a foot on the ground keeps its resting layer whatever the hips do.
    /// </summary>
    public void ToLayers(ReadOnlySpan<float> keyed, Span<float> layers)
    {
        for (var i = 0; i < Count; i++)
        {
            var parent = Joints[i].Parent;
            layers[i] = (parent == NoParent ? 0 : layers[parent]) + Joints[i].Layer + (keyed.IsEmpty ? 0 : keyed[i]);
        }
        foreach (var limb in Limbs)
        {
            layers[limb.End] = limb.Frame == NoParent ? RestLayers[limb.End] : layers[limb.Frame] + RestLayers[limb.End] - RestLayers[limb.Frame];
        }
    }

    /// <summary>One joint's transform in the skeleton's own space, without computing the rest of the pose.</summary>
    public RigidTransform2D WorldOf(ReadOnlySpan<RigidTransform2D> local, int joint)
    {
        var world = local[joint];
        for (var parent = Joints[joint].Parent; parent != NoParent; parent = Joints[parent].Parent) world = world.Then(local[parent]);
        return world;
    }

    /// <summary>Moves every joint of a pose to the nearest transform its limits allow.</summary>
    public void Clamp(Span<RigidTransform2D> local)
    {
        for (var i = 0; i < Count; i++) local[i] = Joints[i].Limits.Clamp(Joints[i].Rest, local[i]);
    }
}
