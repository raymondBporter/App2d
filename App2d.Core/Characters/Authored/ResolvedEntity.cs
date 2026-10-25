using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>An effective role assignment and where it came from: the entity's own override, or its selected motion set.</summary>
public sealed record RoleClip(string Role, MotionClip Clip, string Source);

/// <summary>
/// An activation window with marker times in seconds and the attack collider it enables over [Start, Finish). The window's
/// id is the hit group: one attack instance damages each target once per window, however many hurt colliders it crosses.
/// </summary>
public sealed record ResolvedHit(HitWindow Window, float Start, float Finish, ResolvedCollider Collider)
{
    /// <summary>The collider's convex shape in its anchor frame.</summary>
    public IConvexShape2D Shape => Collider.Shape;

    /// <summary>
    /// Whether the window is active at any moment of a step from <paramref name="previous"/> to <paramref name="now"/>: the
    /// half-open interval is honoured even when the whole of it lies inside the step, so a short window is never lost.
    /// </summary>
    public bool Active(double previous, double now) => previous < Finish && now >= Start;
}
public sealed record ResolvedEvent(ActionEvent Event, float Seconds);
/// <summary>An enabled action. <see cref="Mask"/>, when set, names the targets it owns over locomotion.</summary>
public sealed record ResolvedAction(string Id, MotionClip Clip, string Source, IReadOnlyList<ResolvedHit> Hits, IReadOnlyList<ResolvedEvent> Events)
{
    public IReadOnlySet<string>? Mask { get; init; }
    public ProjectileDef? Projectile { get; init; }
    public float BlendIn { get; init; }
    public float BlendOut { get; init; }
    /// <summary>The action a further press chains to (see <see cref="EntityActionDef.Next"/>).</summary>
    public string? Next { get; init; }
    /// <summary>The clip played once the action ends, when nothing else takes over.</summary>
    public MotionClip? Recovery { get; init; }

    /// <summary>The masked layer's weight at a time in the action: a linear fade in, then out over the clip's end. One without a mask.</summary>
    public float Weight(double seconds)
    {
        if (Mask is null)
        {
            return 1;
        }

        var t = (float)seconds;
        var weight = 1f;
        if (BlendIn > 0)
        {
            weight = MathF.Min(weight, t / BlendIn);
        }

        if (BlendOut > 0)
        {
            weight = MathF.Min(weight, (Clip.Duration - t) / BlendOut);
        }

        return Math.Clamp(weight, 0, 1);
    }
}
public sealed record ResolvedEquipment(PropAsset Prop, ModelSocket Socket);

/// <summary>
/// An entity with every reference resolved and checked: model, effective role clips, enabled actions with marker times
/// in seconds, equipment and hurt regions. Compiled when assets load or change, then shared by every actor.
/// </summary>
public sealed class ResolvedEntity
{
    private ResolvedEntity(EntityAsset asset, ResolvedModel model, ControllerSpec controller)
    {
        Asset = asset;
        Model = model;
        Controller = controller;
        MovementShape = (IConvexShape2D)asset.Movement.Shape.Validated();
    }

    public EntityAsset Asset { get; }
    /// <summary>The movement shape built once: feet at the origin, +X toward facing.</summary>
    public IConvexShape2D MovementShape { get; }
    public string Id => Asset.Id;
    public string Name => Asset.Name;
    public ResolvedModel Model { get; }
    public ControllerSpec Controller { get; }
    public IReadOnlyDictionary<string, RoleClip> Roles { get; private set; } = new Dictionary<string, RoleClip>();
    public IReadOnlyDictionary<string, ResolvedAction> Actions { get; private set; } = new Dictionary<string, ResolvedAction>();
    public IReadOnlyList<ResolvedEquipment> Equipment { get; private set; } = [];
    public IReadOnlyDictionary<string, ModelSocket> Sockets { get; private set; } = new Dictionary<string, ModelSocket>();
    /// <summary>The legacy hurt layout's regions (pose-derived boxes); empty once the entity's hurt geometry is attached colliders.</summary>
    public IReadOnlyList<HurtShape> Hurt { get; private set; } = [];
    /// <summary>
    /// Every attached collider the entity declares or its legacy fields imply, each with a stable ID: explicit entries first,
    /// then one attack collider per legacy hit window with inline geometry and the legacy guard. Actors register these once.
    /// </summary>
    public IReadOnlyList<ResolvedCollider> Colliders { get; private set; } = [];
    /// <summary>The guard collider, when the entity has a guard.</summary>
    public ResolvedCollider? GuardCollider { get; private set; }
    /// <summary>Whether hurt geometry is attached colliders (found by the ordinary broad phase) rather than the legacy layout scan.</summary>
    public bool HurtRegistered => Colliders.Any(c => c.Use == ColliderUses.Hurt);

    public bool Enabled(string action) => Actions.ContainsKey(action);
    public MotionClip? Clip(string role) => Roles.TryGetValue(role, out var assigned) ? assigned.Clip : null;
    public ResolvedCollider? Collider(string id) => Colliders.FirstOrDefault(c => c.Id == id);

    /// <summary>
    /// Compiles an entity against its assets. Every missing reference, unsupported action, incompatible clip and missing
    /// marker is an error naming the entity and field; nothing is dropped or defaulted silently.
    /// </summary>
    public static ResolvedEntity Compile(EntityAsset asset, Func<string, ResolvedModel> resolve, Func<string, MotionClip?> clipOf, Func<string, PropAsset?> propOf)
    {
        asset.Validate();
        var owner = $"Entity '{asset.Id}'";
        var controller = EntityControllers.Get(asset.Controller.Kind, $"{owner} controller.kind");
        ResolvedModel model;
        try
        {
            model = resolve(asset.Model);
        }
        catch (KeyNotFoundException)
        {
            throw new InvalidDataException($"{owner} model: no model or variant '{asset.Model}'.");
        }
        var entity = new ResolvedEntity(asset, model, controller);

        MotionClip Clip(string id, string field)
        {
            var clip = clipOf(id) ?? throw new InvalidDataException($"{field}: no animation '{id}'.");
            try
            {
                clip.Validate(model);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{field}: {ex.Message}");
            }
            return clip;
        }

        var set = model.Base.MotionSets.FirstOrDefault(s => s.Id == asset.MotionSet)
            ?? throw new InvalidDataException($"{owner} motionSet: '{model.Base.Id}' has no motion set '{asset.MotionSet}'.");
        var roles = new Dictionary<string, RoleClip>(StringComparer.Ordinal);
        foreach (var (role, clip) in set.Roles)
        {
            roles[role] = new(role, Clip(clip, $"{owner} motion set '{set.Id}' role '{role}'"), $"set:{set.Id}");
        }

        // A variant's animation-role overrides (P6a) sit between the base's motion set and the entity's own assignments.
        foreach (var (role, clip) in model.RoleClips)
        {
            roles[role] = new(role, Clip(clip, $"{owner} variant '{model.Id}' role '{role}'"), $"variant:{model.Id}");
        }

        foreach (var (role, clip) in asset.Roles)
        {
            roles[role] = new(role, Clip(clip, $"{owner} roles.{role}"), "entity");
        }

        foreach (var role in controller.RequiredRoles)
        {
            if (!roles.ContainsKey(role))
            {
                throw new InvalidDataException($"{owner}: the '{controller.Id}' controller needs role '{role}', which is unassigned.");
            }
        }

        foreach (var role in controller.RequiredRoles.Append(EntityControllers.Run))
        {
            if (roles.TryGetValue(role, out var locomotion) && !locomotion.Clip.Loop)
            {
                throw new InvalidDataException($"{owner} role '{role}': locomotion clip '{locomotion.Clip.Id}' must loop.");
            }
        }

        entity.Roles = roles;

        entity.Sockets = model.Base.Sockets.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var equipment = new List<ResolvedEquipment>();
        foreach (var binding in asset.Equipment)
        {
            var prop = propOf(binding.Prop) ?? throw new InvalidDataException($"{owner} equipment: no prop '{binding.Prop}'.");
            if (prop.BackView is { } back && propOf(back) is null)
            {
                throw new InvalidDataException($"{owner} equipment '{prop.Id}': no back-view appearance '{back}'.");
            }

            if (!entity.Sockets.TryGetValue(binding.Socket, out var socket))
            {
                throw new InvalidDataException($"{owner} equipment '{binding.Prop}': '{model.Base.Id}' has no socket '{binding.Socket}'.");
            }

            equipment.Add(new(prop, socket));
        }
        entity.Equipment = equipment;

        // Attached colliders: the explicit entries, then the legacy inline geometry of windows and the guard as colliders of
        // their own, so every actor registers one set and every query runs over it (P8a). A legacy window keeps its own id
        // when no other window of the entity shares it, otherwise "{action}-{window}".
        var colliders = new List<ResolvedCollider>();
        foreach (var definition in asset.Colliders)
        {
            colliders.Add(ResolvedCollider.Resolve(definition, "collider", $"{owner} collider '{definition.Id}'", model, entity.Sockets, equipment));
        }
        var windowIds = asset.Actions.SelectMany(a => a.Hits).Where(h => h.Collider is null).Select(h => h.Id).ToLookup(id => id, StringComparer.Ordinal);
        ResolvedCollider Synthesize(ColliderDef definition, string source, string field)
        {
            if (colliders.Any(c => c.Id == definition.Id))
            {
                throw new InvalidDataException($"{field}: its inline geometry would register as collider '{definition.Id}', which the entity already declares; name that collider instead.");
            }

            var resolved = ResolvedCollider.Resolve(definition, source, field, model, entity.Sockets, equipment);
            colliders.Add(resolved);
            return resolved;
        }
        var windowColliders = new Dictionary<(string Action, string Window), ResolvedCollider>();
        foreach (var action in asset.Actions)
        {
            var field = $"{owner} action '{action.Id}'";
            foreach (var hit in action.Hits)
            {
                if (hit.Collider is { } named)
                {
                    windowColliders[(action.Id, hit.Id)] = colliders.FirstOrDefault(c => c.Id == named && c.Use == ColliderUses.Attack)
                        ?? throw new InvalidDataException($"{field} hit '{hit.Id}': no attack collider '{named}'.");
                    continue;
                }

                windowColliders[(action.Id, hit.Id)] = Synthesize(new ColliderDef
                {
                    Id = windowIds[hit.Id].Count() == 1 ? hit.Id : $"{action.Id}-{hit.Id}",
                    Use = ColliderUses.Attack,
                    Shape = hit.Shape,
                    Socket = hit.Socket,
                    Prop = hit.Prop,
                    Point = hit.Point,
                    Along = hit.Along,
                    OffsetX = hit.OffsetX,
                    OffsetY = hit.OffsetY,
                    Enabled = true,
                }, "window", $"{field} hit '{hit.Id}'");
            }
        }
        if (asset.Guard is { } guard)
        {
            entity.GuardCollider = guard.Collider is { } named
                ? colliders.First(c => c.Id == named)
                : Synthesize(new ColliderDef { Id = ColliderUses.Guard, Use = ColliderUses.Guard, Shape = guard.Shape, Prop = guard.Prop, Point = PropAsset.GripPoint }, "guard", $"{owner} guard");
        }
        entity.Colliders = colliders;

        var actions = new Dictionary<string, ResolvedAction>(StringComparer.Ordinal);
        foreach (var action in asset.Actions)
        {
            var field = $"{owner} action '{action.Id}'";
            var (clip, source) = action.Clip is { } direct
                ? (Clip(direct, field + " clip"), "clip")
                : (roles.TryGetValue(action.Role!, out var assigned) ? assigned.Clip : throw new InvalidDataException($"{field}: role '{action.Role}' is unassigned."), $"role:{action.Role}");
            float Seconds(ActionTime time, string what)
            {
                if (time.Marker is null)
                {
                    return time.At * clip.Duration;
                }

                var marker = clip.Markers.FirstOrDefault(m => m.Id == time.Marker)
                    ?? throw new InvalidDataException($"{field} {what}: animation '{clip.Id}' has no marker '{time.Marker}'.");
                return marker.Time;
            }
            var hits = new List<ResolvedHit>();
            foreach (var hit in action.Hits)
            {
                var start = Seconds(hit.Start, $"hit '{hit.Id}' start");
                var finish = Seconds(hit.Finish, $"hit '{hit.Id}' finish");
                if (finish <= start)
                {
                    throw new InvalidDataException($"{field} hit '{hit.Id}': finish must follow start.");
                }

                hits.Add(new(hit, start, finish, windowColliders[(action.Id, hit.Id)]));
            }
            var events = action.Events.Select(e => new ResolvedEvent(e, Seconds(e.At, $"event '{e.Id}'"))).OrderBy(e => e.Seconds).ToList();
            if (action.Id == EntityControllers.Jump && !events.Any(e => e.Event.Id == EntityControllers.Launch))
            {
                throw new InvalidDataException($"{field}: a jump needs a '{EntityControllers.Launch}' event.");
            }

            if (events.Any(e => e.Event.Id == EntityControllers.Fire) && !equipment.Any(e => e.Prop.Muzzle is not null))
            {
                throw new InvalidDataException($"{field}: a '{EntityControllers.Fire}' event needs an equipped prop with a muzzle.");
            }

            if (action.Id is EntityControllers.Shoot or EntityControllers.WallShot && !events.Any(e => e.Event.Id == EntityControllers.Fire))
            {
                throw new InvalidDataException($"{field}: a shot needs a '{EntityControllers.Fire}' event.");
            }

            IReadOnlySet<string>? mask = null;
            if (action.Mask is { } maskId)
            {
                var group = model.Base.Groups.FirstOrDefault(g => g.Id == maskId)
                    ?? throw new InvalidDataException($"{field} mask: '{model.Base.Id}' has no control group '{maskId}'.");
                mask = group.Targets.ToHashSet(StringComparer.Ordinal);
                if (action.Id == EntityControllers.Jump)
                {
                    throw new InvalidDataException($"{field}: a jump moves the whole body and cannot be masked.");
                }

                if (action.BlendIn + action.BlendOut > clip.Duration + 1e-4f)
                {
                    throw new InvalidDataException($"{field}: blend in and out ({action.BlendIn + action.BlendOut:0.###}s) exceed the clip ({clip.Duration:0.###}s).");
                }
            }
            var recovery = action.Recovery is { } recoveryId ? Clip(recoveryId, field + " recovery") : null;
            actions[action.Id] = new(action.Id, clip, source, hits, events)
            {
                Mask = mask,
                BlendIn = action.BlendIn,
                BlendOut = action.BlendOut,
                Projectile = action.Projectile,
                Next = action.Next,
                Recovery = recovery
            };
        }
        if (asset.Controller.Flight is not null)
        {
            if (!actions.TryGetValue(EntityControllers.Attack, out var swoop))
            {
                throw new InvalidDataException($"{owner}: flying controller needs an attack action.");
            }

            float previous = -1;
            foreach (var phase in new[] { "commit", "rush", "brake", "low", "reset" })
            {
                var moment = swoop.Events.SingleOrDefault(e => e.Event.Id == phase)
                    ?? throw new InvalidDataException($"{owner}: flight attack needs '{phase}' event.");
                if (moment.Seconds <= previous || moment.Seconds >= swoop.Clip.Duration)
                {
                    throw new InvalidDataException($"{owner}: flight phases must be ordered commit, rush, brake, low, reset before action end.");
                }

                previous = moment.Seconds;
            }
        }
        entity.Actions = actions;

        var hurt = new List<HurtShape>();
        if (asset.Hurt.Layout is { } layoutId)
        {
            var layout = model.Base.HurtLayouts.FirstOrDefault(l => l.Id == layoutId)
                ?? throw new InvalidDataException($"{owner} hurt.layout: '{model.Base.Id}' has no hurt layout '{layoutId}'.");
            foreach (var (id, _) in asset.Hurt.Regions)
            {
                if (!layout.Regions.Any(r => r.Id == id))
                {
                    throw new InvalidDataException($"{owner} hurt.regions.{id}: layout '{layoutId}' has no such region.");
                }
            }

            foreach (var region in layout.Regions)
            {
                var change = asset.Hurt.Regions.GetValueOrDefault(region.Id);
                if (change?.Disabled == true)
                {
                    continue;
                }

                hurt.Add(region with
                {
                    Pad = change?.Pad ?? region.Pad
                });
            }
        }
        else if (asset.Hurt.Regions.Count > 0)
        {
            throw new InvalidDataException($"{owner} hurt.regions: overrides need a layout.");
        }

        entity.Hurt = hurt;
        return entity;
    }
}

/// <summary>
/// The final pose placed in the world: the evaluated pose in actor-local units (facing +X, feet origin) plus the actor's
/// position and facing. Rendering draws <see cref="Local"/> through a mirrored matrix; collision and props use
/// <see cref="Place"/>, so both read one pose.
/// </summary>
public sealed record ActorPose(EvaluatedPose Local, Vector2 Position, int Facing)
{
    /// <summary>
    /// The entity-to-world placement: actor-local XY mirrored to the facing at the actor's position, with no layer
    /// offset, so a character's draw order is the same whichever way it faces.
    /// </summary>
    public LayeredFrame2D Placement => new(new Frame2D(Position, new(Facing, 0), Vector2.UnitY), 0);

    /// <summary>The same placement for real 3D prop geometry: the planar frame extended with an unchanged Z axis.</summary>
    public Frame3D Placement3D => new(new(Position, 0), new(Facing, 0, 0), Vector3.UnitY, Vector3.UnitZ);

    public Vector3 Place(Vector3 local) => Placement.At(local);
    public Vector3 ToLocal(Vector3 world) => Placement.ToLocal(world);
    public Vector3 World(string control) => Place(Local.Points[control]);
    /// <summary>A control's placement in the world: its evaluated frame and layer carried by the actor's placement.</summary>
    public LayeredFrame2D ControlPlacement(string control) => Local.Placement(control).Then(Placement);

    /// <summary>
    /// The carrier a socket attaches to, in actor-local units: its control's point turned by the frame control's angle
    /// (screen axes for the locomotion frame), or by the direction toward another control when one is named.
    /// </summary>
    private LayeredFrame2D SocketCarrier(ModelSocket socket)
    {
        var angle = socket.Frame == CharacterModel.Locomotion ? 0 : Local.Angles[socket.Frame ?? socket.Control];
        if (socket.Toward is { } toward)
        {
            var direction = Local.Points[toward] - Local.Points[socket.Control];
            if (direction.X * direction.X + direction.Y * direction.Y > 1e-10f)
            {
                angle = MathF.Atan2(-direction.X, direction.Y);
            }
        }
        return LayeredFrame2D.FromRotation(angle, Local.Points[socket.Control]);
    }

    /// <summary>
    /// An ordinary socket's planar placement in the world: its authored offset, angle and layer offset on its carrier,
    /// carried by the actor's placement. Clothing, picking and editor handles read this; props lift it to 3D.
    /// </summary>
    public LayeredFrame2D SocketPlacement(ModelSocket socket) =>
        LayeredFrame2D.Local(Vector2.One, socket.Angle, new(socket.OffsetX, socket.OffsetY), socket.OffsetZ).Then(SocketCarrier(socket)).Then(Placement);

    /// <summary>The socket's screen-plane turn with its animated orientation applied, shared with editor handles.</summary>
    public float SocketTurn(ModelSocket socket)
    {
        var planar = LayeredFrame2D.Local(Vector2.One, socket.Angle, Vector2.Zero, 0).Then(SocketCarrier(socket));
        return MathF.Atan2(planar.XAxis.Y, planar.XAxis.X) + Local.SocketAngles.GetValueOrDefault(socket.Id).Z;
    }

    /// <summary>The socket's 3D frame for props: its planar placement lifted by the clip's tilt/twist/turn orientation.</summary>
    public Frame3D Socket(ModelSocket socket) =>
        PropPlacement3D.Lift(SocketPlacement(socket), Local.SocketAngles.GetValueOrDefault(socket.Id));
}

/// <summary>
/// Movement, hurt and attack regions from one placed final pose, as values: the attack and registered-hurt regions are the
/// same placement the actors' registered colliders use (<see cref="ResolvedCollider.Place"/>); the legacy hurt layout is the
/// pose-derived box scan until its entities convert. Graphics-free.
/// </summary>
public static class EntityCollision
{
    /// <summary>Radius, in authored units, given to hurt regions whose controls enclose no area.</summary>
    public const float DegenerateHurtRadius = .005f;

    /// <summary>The movement shape placed at the feet, mirrored to the facing.</summary>
    public static EntityRegion Movement(ResolvedEntity entity, Vector2 position, int facing) =>
        new(ColliderUses.Movement, entity.MovementShape, Similarity2D.FromAxis(position, new(facing, 0), mirror: facing < 0));

    /// <summary>
    /// The hurt regions at a pose: the entity's hurt colliders placed, or, for an entity still on the legacy layout, the
    /// control bounding box of each layout region inflated by its pad (a hairline capsule when degenerate).
    /// </summary>
    public static List<EntityRegion> Hurt(ResolvedEntity entity, ActorPose pose)
    {
        if (entity.HurtRegistered)
        {
            return entity.Colliders.Where(c => c.Use == ColliderUses.Hurt && c.DefaultEnabled).Select(c => c.Region(pose)).ToList();
        }

        var regions = new List<EntityRegion>(entity.Hurt.Count);
        foreach (var shape in entity.Hurt)
        {
            var bounds = Rect2D.FromPoints(shape.Controls, pose, static (control, actor) => actor.World(control).XY).InflatedBy(shape.Pad);
            // Controls on one line with no pad enclose no area: a hairline capsule keeps them hittable without a degenerate box.
            regions.Add(bounds.Width > 0 && bounds.Height > 0
                ? EntityRegion.Box(shape.Id, bounds.Center, bounds.Size)
                : EntityRegion.Capsule(shape.Id, bounds.Min, bounds.Max, DegenerateHurtRadius));
        }
        return regions;
    }

    /// <summary>
    /// The 3D frame an attack collider is anchored to: a socket, an equipped prop's named point with the prop's orientation,
    /// or with neither the actor itself (its offset from the feet, the axis along its facing).
    /// </summary>
    public static Frame3D Anchor(ResolvedEntity entity, ActorPose pose, ResolvedHit hit) => hit.Collider.Anchor(pose);

    /// <summary>Where a shot leaves: the first equipped prop with a muzzle, and its unit axis in world XY (facing included).</summary>
    public static (Vector3 Point, Vector2 Axis) Muzzle(ResolvedEntity entity, ActorPose pose)
    {
        var gun = entity.Equipment.FirstOrDefault(e => e.Prop.Muzzle is not null) ?? throw new InvalidOperationException($"Entity '{entity.Id}' equips no prop with a muzzle.");
        var frame = pose.Socket(gun.Socket);
        // Gameplay stays in XY; foreshortening must not change projectile speed. A head-on barrel has no XY aim.
        return (PropPlacement3D.PropPoint(frame, gun.Prop, gun.Prop.Muzzle!.Value), PropPlacement3D.ProjectAxis(frame, pose.Facing));
    }

    /// <summary>
    /// The window's attack collider placed for the pose, named by the window (the hit group): the one collider placement,
    /// origin pushed along the anchor's axis, +X along that axis (the facing when the axis is foreshortened away) and +Y
    /// toward the frame's across direction so mirroring keeps up in the up direction.
    /// </summary>
    public static EntityRegion Attack(ResolvedEntity entity, ActorPose pose, ResolvedHit hit) =>
        new(hit.Window.Id, hit.Shape, hit.Collider.Place(pose));
}
