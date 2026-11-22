using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Characters.Attachments;

/// <summary>
/// A skeleton in a pose, as painting needs it: every joint's transform in the skeleton's own space, and optionally each
/// joint's place in the drawing order and how far each mount is turned out of the plane. Leave either of those out and the
/// joints keep their resting layers and the mounts lie flat.
/// </summary>
public readonly ref struct PosedSkeleton2D(Skeleton2D skeleton, ReadOnlySpan<RigidTransform2D> world, ReadOnlySpan<float> layers = default, ReadOnlySpan<Vector3f> turns = default)
{
    public Skeleton2D Skeleton { get; } = skeleton;
    public ReadOnlySpan<RigidTransform2D> World { get; } = world;
    public ReadOnlySpan<float> Layers { get; } = layers;
    public ReadOnlySpan<Vector3f> Turns { get; } = turns;

    /// <summary>A joint's place in the drawing order; smaller is nearer the viewer.</summary>
    public float LayerOf(int joint) => Layers.IsEmpty ? Skeleton.RestLayers[joint] : Layers[joint];

    /// <summary>A mount's place in the drawing order: its joint's, plus the mount's own offset.</summary>
    public float MountLayer(int mount) => LayerOf(Skeleton.Mounts[mount].At) + Skeleton.Mounts[mount].Layer;

    /// <summary>
    /// A mount's frame for drawing: flat in the skeleton's plane, unless its turn tips it out. This is the only place a pose
    /// becomes 3D, and only for what is drawn on that mount.
    /// </summary>
    public Frame3D MountFrame(int mount)
    {
        var flat = Frame3D.FromPlanar(Skeleton.Mounts[mount].World(World).ToFrame());
        return Turns.IsEmpty || Turns[mount] == Vector3f.Zero ? flat : Frame3D.FromRotation(Turns[mount]).Then(flat);
    }
}
