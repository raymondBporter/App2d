using App2d.Core.Characters.Geometry;
using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

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
/// An attached collider as the entity authors it: shared convex geometry, a placement binding, its use (category), the
/// default enabled state and the mask of layers it may be tested against. The owner and the stable identity come from the
/// entity. Exactly one binding: a <see cref="Socket"/>, a <see cref="Control"/> (with the socket rules <see cref="Frame"/>,
/// <see cref="Toward"/>, <see cref="OffsetX"/>/<see cref="OffsetY"/>/<see cref="Angle"/>, optionally stretched along the
/// toward control), an equipped <see cref="Prop"/>'s point, or none for the actor itself. Drawing layers never reach the XY
/// collision placement.
/// </summary>
public sealed record ColliderDef
{
    public string Id { get; set; } = "";
    public string Use { get; set; } = ColliderUses.Hurt;
    /// <summary>The shape in the binding's frame; any convex shape definition in authored units.</summary>
    public IShape2D Shape { get; set; } = Rectangle2D.Builder.FromSize(.3f, .3f);
    public string? Socket { get; set; }
    public string? Control { get; set; }
    /// <summary>With <see cref="Control"/>: the control whose rotation orients the frame (socket rules; locomotion keeps the screen axes).</summary>
    public string? Frame { get; set; }
    /// <summary>With <see cref="Control"/>: local +Y points toward this control, as a socket's does.</summary>
    public string? Toward { get; set; }
    /// <summary>With <see cref="Toward"/>: scale the shape uniformly by the distance to that control over its rest distance, so a capsule along a span follows it.</summary>
    public bool Stretch { get; set; }
    public string? Prop { get; set; }
    public string Point { get; set; } = PropAsset.TipPoint;
    /// <summary>Socket and prop bindings: how far the origin is pushed along the projected anchor axis.</summary>
    public float Along { get; set; }
    /// <summary>Actor binding: the offset from the feet; control binding: the socket offset.</summary>
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    /// <summary>Control binding: the socket angle.</summary>
    public float Angle { get; set; }
    /// <summary>The enabled state outside animation: hurt, guard and trigger colliders start enabled; attack colliders are enabled only by their activation windows.</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>The layers the collider may be tested against; all by default.</summary>
    public uint Mask { get; set; } = uint.MaxValue;

    [JsonIgnore]
    public string Binding => Prop is not null ? ColliderBindings.Prop : Socket is not null ? ColliderBindings.Socket : Control is not null ? ColliderBindings.Control : ColliderBindings.Actor;

    /// <summary>Checks the definition on its own; references to the model and equipment are checked when compiling.</summary>
    public void Validate(string field, IReadOnlySet<string> equippedProps)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        EntityVocabulary.Require(Use, ColliderUses.All, field + " use");
        var bindings = new[] { Socket, Control, Prop }.Count(b => b is not null);
        if (bindings > 1)
        {
            throw new InvalidDataException($"{field}: bind to a socket, a control or a prop, not several (none fixes it to the actor).");
        }

        if (Control is null && (Frame is not null || Toward is not null || Stretch || Angle != 0))
        {
            throw new InvalidDataException($"{field}: frame, toward, stretch and angle apply to a control binding only.");
        }

        if (Stretch && Toward is null)
        {
            throw new InvalidDataException($"{field}: stretch needs a toward control.");
        }

        if (Prop is not null)
        {
            if (!equippedProps.Contains(Prop))
            {
                throw new InvalidDataException($"{field}: prop '{Prop}' is not equipped.");
            }

            EntityVocabulary.Require(Point, PropAsset.PointNames, field + " point");
        }
        new Limit(-100, 100).Check(Along, field + " along");
        new Limit(-100, 100).Check(OffsetX, field + " offsetX");
        new Limit(-100, 100).Check(OffsetY, field + " offsetY");
        new Limit(-MathF.PI * 4, MathF.PI * 4).Check(Angle, field + " angle");
    }
}

/// <summary>
/// An attached collider with every reference resolved: the shape built once, the binding's socket or equipment looked up and
/// the rest span measured for stretching. Shared by every actor of the entity; placement is a pure function of a pose.
/// </summary>
public sealed class ResolvedCollider
{
    private ResolvedCollider(ColliderDef definition, string source, IConvexShape2D shape)
    {
        Definition = definition;
        Source = source;
        Shape = shape;
    }

    public ColliderDef Definition { get; }
    public string Id => Definition.Id;
    public string Use => Definition.Use;
    /// <summary>Where the collider came from: an explicit entry ("collider"), a legacy hit window's inline geometry ("window") or the legacy guard ("guard").</summary>
    public string Source { get; }
    public IConvexShape2D Shape { get; }
    public bool Sensor => Use != ColliderUses.Movement;
    public bool DefaultEnabled => Definition.Enabled;
    public uint Mask => Definition.Mask;
    public string Binding => Definition.Binding;
    /// <summary>The socket the collider rides: the named model socket, or the anonymous one a control binding declares.</summary>
    public ModelSocket? Socket { get; private set; }
    public ResolvedEquipment? Equipment { get; private set; }
    public PuppetPoint PropPoint { get; private set; }
    /// <summary>The rest distance from the control to its toward control, for <see cref="ColliderDef.Stretch"/>.</summary>
    public float RestSpan { get; private set; }

    /// <summary>Resolves a definition against the entity's model, sockets and equipment, refusing dangling references by name.</summary>
    public static ResolvedCollider Resolve(ColliderDef definition, string source, string field, ResolvedModel model, IReadOnlyDictionary<string, ModelSocket> sockets, IReadOnlyList<ResolvedEquipment> equipment)
    {
        IConvexShape2D shape;
        try
        {
            shape = definition.Shape.Validated() as IConvexShape2D ?? throw new InvalidDataException("the shape must be convex.");
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException or InvalidDataException)
        {
            throw new InvalidDataException($"{field} shape: {error.Message}", error);
        }
        var resolved = new ResolvedCollider(definition, source, shape);
        switch (definition.Binding)
        {
            case ColliderBindings.Socket:
                resolved.Socket = sockets.TryGetValue(definition.Socket!, out var socket) ? socket
                    : throw new InvalidDataException($"{field}: '{model.Base.Id}' has no socket '{definition.Socket}'.");
                break;
            case ColliderBindings.Prop:
                resolved.Equipment = equipment.FirstOrDefault(e => e.Prop.Id == definition.Prop)
                    ?? throw new InvalidDataException($"{field}: prop '{definition.Prop}' is not equipped.");
                resolved.PropPoint = resolved.Equipment.Prop.Point(definition.Point)
                    ?? throw new InvalidDataException($"{field}: prop '{definition.Prop}' has no '{definition.Point}' point.");
                break;
            case ColliderBindings.Control:
                foreach (var (name, control) in new[] { ("control", definition.Control), ("frame", definition.Frame), ("toward", definition.Toward) })
                {
                    if (control is not null && control != CharacterModel.Locomotion && !model.Controls.ContainsKey(control))
                    {
                        throw new InvalidDataException($"{field} {name}: '{model.Base.Id}' has no control '{control}'.");
                    }
                }
                if (definition.Toward == CharacterModel.Locomotion || definition.Control == CharacterModel.Locomotion)
                {
                    throw new InvalidDataException($"{field}: the locomotion frame orients a binding; it cannot be its control or toward target.");
                }

                resolved.Socket = new ModelSocket
                {
                    Id = definition.Id,
                    Control = definition.Control!,
                    Frame = definition.Frame,
                    Toward = definition.Toward,
                    OffsetX = definition.OffsetX,
                    OffsetY = definition.OffsetY,
                    Angle = definition.Angle,
                };
                if (definition.Toward is { } toward)
                {
                    resolved.RestSpan = Vector2.Distance(model.Rest[definition.Control!].XY, model.Rest[toward].XY);
                }

                break;
        }
        return resolved;
    }

    /// <summary>
    /// The collider's XY placement for a pose, in the pose's units scaled uniformly by <paramref name="scale"/> (a unit
    /// conversion). Socket and prop bindings follow the lifted 3D frame projected with <see cref="PropPlacement3D.ProjectAxis"/>
    /// (the facing when head-on) and keep +Y toward the frame's across direction, so a mirrored actor keeps up up; actor
    /// bindings use the actor's own axes; control bindings are the planar socket placement.
    /// </summary>
    public Similarity2D Place(ActorPose pose, float scale = 1)
    {
        switch (Binding)
        {
            case ColliderBindings.Control:
            {
                var socket = Socket!;
                var placement = pose.SocketPlacement(socket);
                var xAxis = placement.XAxis;
                var mirror = new Vector2d(xAxis).Cross(new(placement.YAxis)) < 0d;
                var stretch = 1f;
                if (Definition.Stretch && RestSpan > 1e-6f)
                {
                    stretch = Vector2.Distance(pose.World(socket.Control).XY, pose.World(socket.Toward!).XY) / RestSpan;
                }

                return Similarity2D.FromAxis(placement.Origin.XY * scale, xAxis, mirror, xAxis.Length() * stretch * scale);
            }
            default:
            {
                var frame = Anchor(pose);
                var axis = PropPlacement3D.ProjectAxis(frame, pose.Facing);
                var mirror = new Vector2d(axis).Cross(new(frame.YAxis.XY)) < 0d;
                return Similarity2D.FromAxis(frame.TransformPoint(new(Definition.Along, 0, 0)).XY * scale, axis, mirror, scale);
            }
        }
    }

    /// <summary>The 3D anchor frame of an actor, socket or prop binding (the frame a hit window was anchored to before P8).</summary>
    public Frame3D Anchor(ActorPose pose) => Binding switch
    {
        ColliderBindings.Socket => pose.Socket(Socket!),
        ColliderBindings.Prop => Prop(pose),
        ColliderBindings.Actor => pose.Placement3D.WithOrigin(pose.Place(new(Definition.OffsetX, Definition.OffsetY, 0))),
        _ => throw new InvalidOperationException($"Collider '{Id}' is bound to a control; it has no 3D anchor."),
    };

    private Frame3D Prop(ActorPose pose)
    {
        var equipment = Equipment!;
        var frame = pose.Socket(equipment.Socket);
        return frame.WithOrigin(PropPlacement3D.PropPoint(frame, equipment.Prop, PropPoint));
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
            Movement = Register(ResolvedCollider.Resolve(new ColliderDef
            {
                Id = ColliderUses.Movement,
                Use = ColliderUses.Movement,
                Shape = entity.Asset.Movement.Shape,
                Enabled = true,
            }, "movement", $"Entity '{entity.Id}' movement", entity.Model, entity.Sockets, entity.Equipment), layer);
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

    /// <summary>
    /// Where an attack landed on the registered hurt geometry: the centre of the overlap of the hit bounds with the closest
    /// enabled hurt collider's bounds, or null when none overlaps. The same rule the legacy hurt scan used, over the
    /// colliders as registered.
    /// </summary>
    public Vector2? HurtContact(Rect2D hit)
    {
        Vector2? closest = null;
        var distance = float.PositiveInfinity;
        foreach (var hurt in _hurt)
        {
            if (!hurt.Enabled)
            {
                continue;
            }

            var bounds = hurt.Bounds;
            if (!bounds.Intersects(hit))
            {
                continue;
            }

            var point = (Vector2.Max(bounds.Min, hit.Min) + Vector2.Min(bounds.Max, hit.Max)) / 2;
            var d = Vector2.DistanceSquared(point, hit.Center);
            if (d < distance)
            {
                distance = d;
                closest = point;
            }
        }
        return closest;
    }
}

/// <summary>
/// Overlap queries over attached colliders through the ordinary broad phase. The filter is reconciled with collider-pair
/// collection: both masks count (the target's layer must be in the query's mask and the target's mask must accept the
/// query's layer, as <see cref="Collider2D.CanCollideWith"/> requires of a pair), the owner's own colliders are excluded
/// and the use is the category; gameplay rules (faction, alive, hit groups) stay with the caller.
/// </summary>
public static class ColliderQueries
{
    /// <summary>
    /// The enabled attached colliders of <paramref name="use"/> on other owners that overlap the placed query shape, in
    /// broad-phase order. <paramref name="owner"/>'s own colliders are excluded (self filtering); <paramref name="layerMask"/>
    /// selects the owners' gameplay layers (ally filtering) and <paramref name="queryLayer"/> is the layer the query itself
    /// is on, which the targets' masks must accept (an undeclared query is one-sided); <paramref name="accept"/> may refuse further.
    /// </summary>
    public static int Overlap(CollisionSystem2D system, SpatialObject2D query, EntityId2D owner, string use, List<AttachedCollider> results, List<CollisionOverlap2D> scratch, uint layerMask = uint.MaxValue, Func<AttachedCollider, bool>? accept = null, Collider2D? excluded = null, uint? queryLayer = null)
    {
        ArgGuard.ThrowIfNull(system);
        ArgGuard.ThrowIfNull(results);
        ArgGuard.ThrowIfNull(scratch);
        results.Clear();
        system.Overlap(query, scratch, layerMask, includeSensors: true, excluded, queryLayer);
        foreach (var overlap in scratch)
        {
            if (overlap.Collider.UserData is AttachedCollider attached && attached.Use == use && attached.Owner != owner && (accept?.Invoke(attached) ?? true))
            {
                results.Add(attached);
            }
        }

        return results.Count;
    }

    /// <summary>The same query from a registered collider: it is excluded itself, its owner's colliders are skipped, its mask and layer are the query's.</summary>
    public static int Overlap(AttachedCollider query, string use, List<AttachedCollider> results, List<CollisionOverlap2D> scratch, uint layerMask = uint.MaxValue, Func<AttachedCollider, bool>? accept = null)
    {
        ArgGuard.ThrowIfNull(query);
        return Overlap(query.Collider.System, query.Object, query.Owner, use, results, scratch, layerMask & query.Collider.CollisionMask, accept, query.Collider, query.Collider.CollisionLayer);
    }
}
