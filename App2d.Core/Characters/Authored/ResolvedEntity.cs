using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>An effective role assignment and where it came from: the entity's own override, or its selected motion set.</summary>
public sealed record RoleClip(string Role, MotionClip Clip, string Source);

public sealed record ResolvedHit(HitWindow Window, float Start, float Finish);
public sealed record ResolvedEvent(ActionEvent Event, float Seconds);
public sealed record ResolvedAction(string Id, MotionClip Clip, string Source, IReadOnlyList<ResolvedHit> Hits, IReadOnlyList<ResolvedEvent> Events);
public sealed record ResolvedEquipment(PropAsset Prop, ModelSocket Socket);

/// <summary>
/// An entity with every reference resolved and checked: model, effective role clips, enabled actions with marker times
/// in seconds, equipment and hurt regions. Compiled when assets load or change, then shared by every actor.
/// </summary>
public sealed class ResolvedEntity
{
    private ResolvedEntity(EntityAsset asset, ResolvedModel model, ControllerSpec controller) { Asset = asset; Model = model; Controller = controller; }

    public EntityAsset Asset { get; }
    public string Id => Asset.Id;
    public ResolvedModel Model { get; }
    public ControllerSpec Controller { get; }
    public IReadOnlyDictionary<string, RoleClip> Roles { get; private set; } = new Dictionary<string, RoleClip>();
    public IReadOnlyDictionary<string, ResolvedAction> Actions { get; private set; } = new Dictionary<string, ResolvedAction>();
    public IReadOnlyList<ResolvedEquipment> Equipment { get; private set; } = [];
    public IReadOnlyDictionary<string, ModelSocket> Sockets { get; private set; } = new Dictionary<string, ModelSocket>();
    public IReadOnlyList<HurtShape> Hurt { get; private set; } = [];

    public bool Enabled(string action) => Actions.ContainsKey(action);
    public MotionClip? Clip(string role) => Roles.TryGetValue(role, out var assigned) ? assigned.Clip : null;

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
        try { model = resolve(asset.Model); }
        catch (KeyNotFoundException) { throw new InvalidDataException($"{owner} model: no model or variant '{asset.Model}'."); }
        var entity = new ResolvedEntity(asset, model, controller);

        MotionClip Clip(string id, string field)
        {
            var clip = clipOf(id) ?? throw new InvalidDataException($"{field}: no animation '{id}'.");
            try { clip.Validate(model); }
            catch (InvalidDataException ex) { throw new InvalidDataException($"{field}: {ex.Message}"); }
            return clip;
        }

        var set = model.Base.MotionSets.FirstOrDefault(s => s.Id == asset.MotionSet)
            ?? throw new InvalidDataException($"{owner} motionSet: '{model.Base.Id}' has no motion set '{asset.MotionSet}'.");
        var roles = new Dictionary<string, RoleClip>(StringComparer.Ordinal);
        foreach (var (role, clip) in set.Roles) roles[role] = new(role, Clip(clip, $"{owner} motion set '{set.Id}' role '{role}'"), $"set:{set.Id}");
        foreach (var (role, clip) in asset.Roles) roles[role] = new(role, Clip(clip, $"{owner} roles.{role}"), "entity");
        foreach (var role in controller.RequiredRoles)
            if (!roles.ContainsKey(role)) throw new InvalidDataException($"{owner}: the '{controller.Id}' controller needs role '{role}', which is unassigned.");
        foreach (var role in controller.RequiredRoles.Append(EntityControllers.Run))
            if (roles.TryGetValue(role, out var locomotion) && !locomotion.Clip.Loop) throw new InvalidDataException($"{owner} role '{role}': locomotion clip '{locomotion.Clip.Id}' must loop.");
        entity.Roles = roles;

        entity.Sockets = model.Base.Sockets.ToDictionary(s => s.Id, StringComparer.Ordinal);
        var equipment = new List<ResolvedEquipment>();
        foreach (var binding in asset.Equipment)
        {
            var prop = propOf(binding.Prop) ?? throw new InvalidDataException($"{owner} equipment: no prop '{binding.Prop}'.");
            if (!entity.Sockets.TryGetValue(binding.Socket, out var socket)) throw new InvalidDataException($"{owner} equipment '{binding.Prop}': '{model.Base.Id}' has no socket '{binding.Socket}'.");
            equipment.Add(new(prop, socket));
        }
        entity.Equipment = equipment;

        var actions = new Dictionary<string, ResolvedAction>(StringComparer.Ordinal);
        foreach (var action in asset.Actions)
        {
            var field = $"{owner} action '{action.Id}'";
            var (clip, source) = action.Clip is { } direct
                ? (Clip(direct, field + " clip"), "clip")
                : (roles.TryGetValue(action.Role!, out var assigned) ? assigned.Clip : throw new InvalidDataException($"{field}: role '{action.Role}' is unassigned."), $"role:{action.Role}");
            float Seconds(ActionTime time, string what)
            {
                if (time.Marker is null) return time.At * clip.Duration;
                var marker = clip.Markers.FirstOrDefault(m => m.Id == time.Marker)
                    ?? throw new InvalidDataException($"{field} {what}: animation '{clip.Id}' has no marker '{time.Marker}'.");
                return marker.Time;
            }
            var hits = new List<ResolvedHit>();
            foreach (var hit in action.Hits)
            {
                if (hit.Socket is not null && !entity.Sockets.ContainsKey(hit.Socket)) throw new InvalidDataException($"{field} hit '{hit.Id}': '{model.Base.Id}' has no socket '{hit.Socket}'.");
                if (hit.Prop is not null && equipment.First(e => e.Prop.Id == hit.Prop).Prop.Point(hit.Point) is null) throw new InvalidDataException($"{field} hit '{hit.Id}': prop '{hit.Prop}' has no '{hit.Point}' point.");
                var start = Seconds(hit.Start, $"hit '{hit.Id}' start"); var finish = Seconds(hit.Finish, $"hit '{hit.Id}' finish");
                if (finish <= start) throw new InvalidDataException($"{field} hit '{hit.Id}': finish must follow start.");
                hits.Add(new(hit, start, finish));
            }
            var events = action.Events.Select(e => new ResolvedEvent(e, Seconds(e.At, $"event '{e.Id}'"))).OrderBy(e => e.Seconds).ToList();
            if (action.Id == EntityControllers.Jump && !events.Any(e => e.Event.Id == EntityControllers.Launch))
                throw new InvalidDataException($"{field}: a jump needs a '{EntityControllers.Launch}' event.");
            actions[action.Id] = new(action.Id, clip, source, hits, events);
        }
        entity.Actions = actions;

        var hurt = new List<HurtShape>();
        if (asset.Hurt.Layout is { } layoutId)
        {
            var layout = model.Base.HurtLayouts.FirstOrDefault(l => l.Id == layoutId)
                ?? throw new InvalidDataException($"{owner} hurt.layout: '{model.Base.Id}' has no hurt layout '{layoutId}'.");
            foreach (var (id, _) in asset.Hurt.Regions)
                if (!layout.Regions.Any(r => r.Id == id)) throw new InvalidDataException($"{owner} hurt.regions.{id}: layout '{layoutId}' has no such region.");
            foreach (var region in layout.Regions)
            {
                var change = asset.Hurt.Regions.GetValueOrDefault(region.Id);
                if (change?.Disabled == true) continue;
                hurt.Add(region with { Pad = change?.Pad ?? region.Pad });
            }
        }
        else if (asset.Hurt.Regions.Count > 0) throw new InvalidDataException($"{owner} hurt.regions: overrides need a layout.");
        entity.Hurt = hurt;
        return entity;
    }
}

/// <summary>A socket's placed frame: origin, and unit axis/across directions in world XY.</summary>
public readonly record struct SocketFrame(Vector3 Origin, Vector2 Axis, Vector2 Across)
{
    public Vector3 At(float along, float across, float depth = 0) => Origin + new Vector3(Axis * along + Across * across, depth);
}

/// <summary>
/// The final pose placed in the world: the evaluated pose in actor-local units (facing +X, feet origin) plus the actor's
/// position and facing. Rendering draws <see cref="Local"/> through a mirrored matrix; collision and props use
/// <see cref="Place"/>, so both read one pose.
/// </summary>
public sealed record ActorPose(EvaluatedPose Local, Vector2 Position, int Facing)
{
    public Vector3 Place(Vector3 local) => new(Position.X + local.X * Facing, Position.Y + local.Y, local.Z);
    public Vector3 ToLocal(Vector3 world) => new((world.X - Position.X) * Facing, world.Y - Position.Y, world.Z);
    public Vector3 World(string control) => Place(Local.Points[control]);

    public SocketFrame Socket(ModelSocket socket)
    {
        var angle = Local.Angles[socket.Frame ?? socket.Control];
        var origin = Local.Points[socket.Control] + PoseEvaluator.RotateXY(new(socket.OffsetX, socket.OffsetY, 0), angle);
        var (sin, cos) = MathF.SinCos(angle + socket.Angle);
        return new(Place(origin), new(cos * Facing, sin), new(-sin * Facing, cos));
    }

    /// <summary>A prop's local point in the world, with its grip on the socket. The one transform art, hits and muzzles share.</summary>
    public static Vector3 PropPoint(SocketFrame frame, PropAsset prop, PuppetPoint local) =>
        frame.At(local.X - prop.Grip.X, local.Y - prop.Grip.Y, local.Z - prop.Grip.Z);
}

/// <summary>Movement, hurt and attack regions from one placed final pose. Graphics-free.</summary>
public static class EntityCollision
{
    public static EntityRegion Movement(ResolvedEntity entity, Vector2 position, int facing)
    {
        var box = entity.Asset.Movement;
        return EntityRegion.Box("movement", new(position.X + box.OffsetX * facing, position.Y + box.Height / 2), new(box.Width, box.Height));
    }

    public static List<EntityRegion> Hurt(ResolvedEntity entity, ActorPose pose)
    {
        var regions = new List<EntityRegion>(entity.Hurt.Count);
        foreach (var shape in entity.Hurt)
        {
            var min = new Vector2(float.PositiveInfinity); var max = new Vector2(float.NegativeInfinity);
            foreach (var control in shape.Controls) { var p = pose.World(control); var xy = new Vector2(p.X, p.Y); min = Vector2.Min(min, xy); max = Vector2.Max(max, xy); }
            min -= new Vector2(shape.Pad); max += new Vector2(shape.Pad);
            regions.Add(new(shape.Id, [min, new(max.X, min.Y), max, new(min.X, max.Y)]));
        }
        return regions;
    }

    /// <summary>The frame a hit window is anchored to: a socket, or an equipped prop's named point with the prop's orientation.</summary>
    public static SocketFrame Anchor(ResolvedEntity entity, ActorPose pose, HitWindow hit)
    {
        if (hit.Socket is not null) return pose.Socket(entity.Sockets[hit.Socket]);
        var equipment = entity.Equipment.First(e => e.Prop.Id == hit.Prop);
        var frame = pose.Socket(equipment.Socket);
        return frame with { Origin = ActorPose.PropPoint(frame, equipment.Prop, equipment.Prop.Point(hit.Point)!.Value) };
    }

    public static EntityRegion Attack(ResolvedEntity entity, ActorPose pose, ResolvedHit hit)
    {
        var anchor = Anchor(entity, pose, hit.Window).At(hit.Window.Along, 0);
        return EntityRegion.Box(hit.Window.Id, new(anchor.X, anchor.Y), new(hit.Window.Width, hit.Window.Height));
    }
}
