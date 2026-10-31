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
/// The gameplay use of an attached collider: the category the combat queries filter on. Every use but the movement body is a
/// sensor; what a hit, a block or a trigger contact means belongs to gameplay (P9), not to the geometry.
/// </summary>
public static class ColliderUses
{
    public const string Hurt = "hurt";
    public const string Attack = "attack";
    public const string Guard = "guard";
    public const string Trigger = "trigger";
    /// <summary>The stable terrain movement body, registered by the actor's owner (a rigid body in the game, a plain collider in the arena).</summary>
    public const string Movement = "movement";
    /// <summary>The uses an entity may declare.</summary>
    public static readonly IReadOnlyList<string> All = [Hurt, Attack, Guard, Trigger];
}

/// <summary>How an attached collider is placed: the binding kinds, derived from which fields a definition sets.</summary>
public static class ColliderBindings
{
    /// <summary>Fixed to the actor: its offset from the feet, +X toward facing.</summary>
    public const string Actor = "actor";
    /// <summary>On a model socket's 3D frame (the clip's tilt/twist/turn applied), projected to XY like a prop.</summary>
    public const string Socket = "socket";
    /// <summary>On an equipped prop's named point, along the prop's projected axis.</summary>
    public const string Prop = "prop";
    /// <summary>On a control's planar placement, with the socket rules for frame, toward, offset and angle.</summary>
    public const string Control = "control";
}

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

/// <summary>
/// One actor's registered instance of a resolved collider: the persistent <see cref="Collider2D"/> in the collision system,
/// created once at attach and moved every fixed step. Its region is a read-only view of the collider as registered.
/// </summary>
public sealed class AttachedCollider
{
    internal AttachedCollider(ResolvedCollider definition, Collider2D collider)
    {
        Definition = definition;
        Collider = collider;
    }

    public ResolvedCollider Definition { get; }
    public Collider2D Collider { get; }
    public SpatialObject2D Object => Collider.WorldObject;
    public string Id => Definition.Id;
    public string Use => Definition.Use;
    public bool Enabled => Collider.IsEnabled;
    /// <summary>The owner this collider belongs to; self and ally filtering read it.</summary>
    public EntityId2D Owner => Collider.EntityId;
    /// <summary>The registered placement, as a region in the collider's units.</summary>
    public EntityRegion Region => new(Id, Definition.Shape, Object.CollisionPose);
    public Rect2D Bounds => Object.WorldBounds;

    internal void Place(Similarity2D pose)
    {
        var transform = Object.Transform;
        var mirrored = pose.IsMirrored;
        var forward = mirrored ? -pose.XAxis : pose.XAxis;
        var rotation = MathF.Atan2(forward.Y, forward.X);
        var scale = new Vector2(mirrored ? -pose.Scale : pose.Scale, pose.Scale);
        if (transform.Position != pose.Translation)
        {
            transform.Position = pose.Translation;
        }

        if (transform.Rotation != rotation)
        {
            transform.Rotation = rotation;
        }

        if (transform.Scale != scale)
        {
            transform.Scale = scale;
        }
    }

    internal void SetEnabled(bool enabled) => Collider.IsEnabled = enabled;
}

/// <summary>
/// The attached colliders of one actor, registered in a <see cref="CollisionSystem2D"/> at a lifecycle boundary (spawn) and
/// removed at the other (<see cref="Detach"/>); between them <see cref="Sync"/> moves and enables them from the authoritative
/// fixed-step pose and never recreates one. Every collider carries the owner's entity ID and gameplay layer, so the
/// ordinary broad phase finds hurt geometry that extends past the movement body; the use is the attachment's category.
/// </summary>
public sealed class ActorColliders
{
    private readonly List<AttachedCollider> _all = [];
    private readonly List<AttachedCollider> _hurt = [];
    private bool _active = true;

    /// <summary>
    /// Registers one collider per resolved collider of the entity, plus the movement shape when <paramref name="movement"/>
    /// is set (an actor without a rigid body, as in the arena). <paramref name="scale"/> converts authored units into the
    /// system's units; <paramref name="layer"/> is the owner's gameplay layer.
    /// </summary>
    public ActorColliders(ResolvedEntity entity, CollisionSystem2D system, EntityId2D owner, uint layer, float scale = 1, bool movement = false)
    {
        ArgGuard.ThrowIfNull(entity);
        ArgGuard.ThrowIfNull(system);
        ArgGuard.Positive(scale);
        Entity = entity;
        System = system;
        Owner = owner;
        Scale = scale;
        if (movement)
        {
            Movement = Register(entity.Movement, layer);
        }
        foreach (var collider in entity.Colliders)
        {
            Register(collider, layer);
        }
    }

    public ResolvedEntity Entity { get; }
    public CollisionSystem2D System { get; }
    public EntityId2D Owner { get; }
    public float Scale { get; }
    /// <summary>Every registered collider, the movement collider first when this set owns one.</summary>
    public IReadOnlyList<AttachedCollider> All => _all;
    public AttachedCollider? Movement { get; }
    /// <summary>The hurt colliders; empty for an entity whose hurt geometry is still the legacy layout.</summary>
    public IReadOnlyList<AttachedCollider> Hurt => _hurt;
    public bool HasHurt => _hurt.Count > 0;
    public bool Attached { get; private set; } = true;

    public AttachedCollider? Find(string id) => _all.FirstOrDefault(c => c.Id == id);
    public IEnumerable<AttachedCollider> OfUse(string use) => _all.Where(c => c.Use == use);
    public IEnumerable<AttachedCollider> Enabled => _all.Where(c => c.Enabled);

    private AttachedCollider Register(ResolvedCollider definition, uint layer)
    {
        var collider = System.AddCollider(new SpatialObject2D(definition.Shape), ColliderMobility2D.Dynamic);
        collider.IsSensor = definition.Sensor;
        collider.CollisionLayer = layer;
        collider.CollisionMask = definition.Mask;
        collider.EntityId = Owner;
        collider.IsEnabled = false;
        var attached = new AttachedCollider(definition, collider);
        collider.UserData = attached;
        _all.Add(attached);
        if (definition.Use == ColliderUses.Hurt)
        {
            _hurt.Add(attached);
        }

        return attached;
    }

    /// <summary>
    /// The pose/collider synchronization point: places every collider from the authoritative pose and sets the enabled
    /// states for this step. Attack colliders are enabled by the windows active at any moment of the step
    /// (<see cref="EntityAnimator.ActiveHits"/>: half-open intervals, a whole interval crossed inside one step included);
    /// hurt and trigger colliders by the actor being alive, the guard by <paramref name="guardUp"/>; all honour the
    /// definition's default enabled state and the set being active.
    /// </summary>
    public void Sync(ActorPose pose, IEnumerable<ResolvedHit>? activeHits, bool alive, bool guardUp = true)
    {
        ArgGuard.ThrowIfNull(pose);
        StateGuard.ThrowIf(!Attached, "The colliders were detached.");
        if (Movement is { } movement)
        {
            movement.Place(Similarity2D.FromAxis(pose.Position * Scale, new(pose.Facing, 0), mirror: pose.Facing < 0, Scale));
            movement.SetEnabled(_active);
        }
        foreach (var attached in _all)
        {
            if (ReferenceEquals(attached, Movement))
            {
                continue;
            }

            var definition = attached.Definition;
            attached.Place(definition.Place(pose, Scale));
            var enabled = _active && definition.DefaultEnabled && definition.Use switch
            {
                ColliderUses.Attack => activeHits is not null && Activates(activeHits, definition),
                ColliderUses.Guard => alive && guardUp,
                _ => alive,
            };
            attached.SetEnabled(enabled);
        }
    }

    private static bool Activates(IEnumerable<ResolvedHit> hits, ResolvedCollider collider)
    {
        foreach (var hit in hits)
        {
            if (ReferenceEquals(hit.Collider, collider))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Streaming or despawn without removal: an inactive set keeps its colliders registered but disabled until the next <see cref="Sync"/>.</summary>
    public void SetActive(bool active)
    {
        _active = active;
        if (!active)
        {
            foreach (var attached in _all)
            {
                attached.SetEnabled(false);
            }
        }
    }

    /// <summary>The other lifecycle boundary: removes every collider from the system. The set cannot be synchronized afterwards.</summary>
    public void Detach()
    {
        if (!Attached)
        {
            return;
        }

        Attached = false;
        foreach (var attached in _all)
        {
            System.RemoveCollider(attached.Collider);
        }
    }

}
