using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// An attached collider with every reference resolved: the shape built once, the binding's socket or equipment looked up and
/// the rest span measured for stretching. Shared by every actor of the entity; placement is a pure function of a pose.
/// </summary>
public sealed class ResolvedCollider : ResolvedGeometry
{
    private ResolvedCollider(PartGeometry definition, string use, string source, IConvexShape2D shape, ResolvedModel model,
        IReadOnlyDictionary<string, ModelSocket> sockets, IReadOnlyList<ResolvedEquipment> equipment, string field)
        : base(definition, model, sockets, equipment, field)
    {
        Use = use;
        Source = source;
        Shape = shape;
    }

    public string Use { get; }
    /// <summary>Whether this geometry supplies a gameplay sensor or the stable movement body.</summary>
    public string Source { get; }
    public IConvexShape2D Shape { get; }
    public bool Sensor => Use != ColliderUses.Movement;
    public bool DefaultEnabled => Definition.Collision!.Enabled;
    public uint Mask => Definition.Collision!.Mask;

    /// <summary>Resolves a definition against the entity's model, sockets and equipment, refusing dangling references by name.</summary>
    public static ResolvedCollider Resolve(PartGeometry definition, string use, string source, string field, ResolvedModel model, IReadOnlyDictionary<string, ModelSocket> sockets, IReadOnlyList<ResolvedEquipment> equipment)
    {
        IConvexShape2D shape;
        try
        {
            shape = (definition.Geometry as IShape2D)?.Validated() as IConvexShape2D ?? throw new InvalidDataException("the shape must be convex.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            throw new InvalidDataException($"{field} shape: {error.Message}", error);
        }
        return new(definition, use, source, shape, model, sockets, equipment, field);
    }

    /// <summary>The placed shape as a region value, for outlines, reference capture and legacy overlap tests.</summary>
    public EntityRegion Region(ActorPose pose, float scale = 1) => new(Id, Shape, Place(pose, scale));
}
