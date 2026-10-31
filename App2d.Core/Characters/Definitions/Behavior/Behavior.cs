using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Diagnostics.CodeAnalysis;

namespace App2d.Core.Characters.Authored;

/// <summary>The impact kinds a strike may name; the game maps them to its presentation and audio.</summary>
public static class ImpactKinds
{
    public const string Generic = "generic";
    public const string Sword = "sword";
    public static readonly IReadOnlyList<string> All = [Generic, Sword];
}

/// <summary>
/// Damage on contact (P9a): what an accepted contact of one hit group does. Keyed in <see cref="Behavior.Strikes"/>
/// by the hit group: a hit window's id, or an action's id for the projectile it fires. Damage is an integer, the sound an
/// asset reference; neither lives on the window or the projectile geometry any more.
/// </summary>
public sealed record StrikeDef
{
    public int Damage { get; set; } = 1;
    /// <summary>Played where the contact is accepted; null plays the game's default impact.</summary>
    public string? Sound { get; set; }
    public string Impact { get; set; } = ImpactKinds.Generic;

    public void Validate(string field)
    {
        new Limit(0, 10000).Check(Damage, field + " damage");
        if (Sound is not null)
        {
            AuthoredAsset.RequireId(Sound, field + " sound");
        }

        EntityVocabulary.Require(Impact, ImpactKinds.All, field + " impact");
    }
}

/// <summary>Receiving damage: the health pool, knockback resistance, and how long a hit shows and staggers.</summary>
public sealed record ReceiveDamageDef
{
    public int Health { get; set; } = 3;
    /// <summary>Gameplay multiplier dividing knockback, independent of the physical body's mass.</summary>
    public float KnockbackResistance { get; set; } = 1;
    /// <summary>How long the hurt expression and reaction state last after an accepted contact.</summary>
    public float HurtSeconds { get; set; } = .45f;
    /// <summary>The stagger after a hit when the entity has no hit clip (the clip's length otherwise).</summary>
    public float StaggerSeconds { get; set; } = EntityReaction.DefaultStaggerSeconds;

    public void Validate(string field)
    {
        new Limit(1, 10000).Check(Health, field + " health");
        new Limit(.1f, 100).Check(KnockbackResistance, field + " knockbackResistance");
        new Limit(0, 10).Check(HurtSeconds, field + " hurtSeconds");
        new Limit(0, 10).Check(StaggerSeconds, field + " staggerSeconds");
    }
}

/// <summary>Guarding (P9a): the event of an action after which the guard is down, and the sound an accepted block plays.</summary>
public sealed record GuardBehaviorDef
{
    public string Collider { get; set; } = "";
    public string OpenEvent { get; set; } = "guard-open";
    public string? BlockSound { get; set; } = "shield-block";

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(OpenEvent, field + " openEvent");
        if (BlockSound is not null)
        {
            AuthoredAsset.RequireId(BlockSound, field + " blockSound");
        }
    }
}

/// <summary>
/// Optional gameplay configuration on a Part: controller, animation roles, action timing, strikes, receiving damage,
/// guard and custom properties. Geometry IDs select collision entries; this aggregate owns no attached shapes or placement.
/// </summary>
public sealed record Behavior
{
    public string MotionSet { get; set; } = "";
    public Dictionary<string, string> Roles { get; set; } = [];
    public ControllerConfig Controller { get; set; } = new();
    public string Movement { get; set; } = ColliderUses.Movement;
    /// <summary>Stable geometry IDs mapped to gameplay roles; shapes and placement belong to Part.Geometry.</summary>
    public Dictionary<string, string> Colliders { get; set; } = [];
    public List<EntityActionDef> Actions { get; set; } = [];

    /// <summary>Damage on contact per hit group: a window id, or an action id for its projectile.</summary>
    public Dictionary<string, StrikeDef> Strikes { get; set; } = [];
    public ReceiveDamageDef Damage { get; set; } = new();
    /// <summary>Present when the entity guards; null otherwise.</summary>
    public GuardBehaviorDef? Guard { get; set; }
    /// <summary>Custom authored properties by name; values are text, read explicitly by the behavior that wants them.</summary>
    public Dictionary<string, string> Properties { get; set; } = [];

    private void ValidateEffects(string owner)
    {
        if (Strikes is null || Damage is null || Properties is null)
        {
            throw new InvalidDataException($"{owner} behaviors: collections cannot be null.");
        }

        foreach (var (group, strike) in Strikes)
        {
            AuthoredAsset.RequireId(group, $"{owner} behaviors.strikes key");
            if (strike is null)
            {
                throw new InvalidDataException($"{owner} behaviors.strikes.{group}: null strike.");
            }

            strike.Validate($"{owner} behaviors.strikes.{group}");
        }
        Damage.Validate($"{owner} behaviors.damage");
        Guard?.Validate($"{owner} behaviors.guard");
        foreach (var (key, value) in Properties)
        {
            AuthoredAsset.RequireId(key, $"{owner} behaviors.properties key");
            if (value is null)
            {
                throw new InvalidDataException($"{owner} behaviors.properties.{key}: null value.");
            }
        }
    }
    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    /// <summary>A convex shape definition that builds and fits the authored limits: extents between .01 and the size limit, centre within 100 units.</summary>
    private static void CheckShape(IShape2D? shape, string field, float sizeLimit = 100)
    {
        Require(shape is not null, $"{field}: a shape is required.");
        IShape2D built;
        try
        {
            built = shape.Validated();
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            throw new InvalidDataException($"{field}: {error.Message}", error);
        }
        Require(built is IConvexShape2D, $"{field}: the shape must be convex; composites and half-spaces are not supported here.");
        var bounds = ShapeBounds2D.Calculate(built);
        new Limit(.01f, sizeLimit).Check(bounds.Width, field + " width");
        new Limit(.01f, sizeLimit).Check(bounds.Height, field + " height");
        new Limit(-100, 100).Check(bounds.Center.X, field + " centre x");
        new Limit(-100, 100).Check(bounds.Center.Y, field + " centre y");
    }

    /// <summary>Checks the file on its own. References to models, clips and props are checked when compiling.</summary>
    public void Validate(Part part)
    {
        var owner = $"Part '{part.Id}' behavior";
        AuthoredAsset.RequireId(part.Model, $"{owner} model");
        AuthoredAsset.RequireId(MotionSet, $"{owner} motionSet");
        Require(Roles is not null && Controller is not null && Movement is not null && Colliders is not null && Actions is not null,
            $"{owner}: collections cannot be null.");
        ValidateEffects(owner);
        foreach (var (role, clip) in Roles)
        {
            AuthoredAsset.RequireId(role, $"{owner} roles");
            AuthoredAsset.RequireId(clip, $"{owner} roles.{role}");
        }
        var spec = EntityControllers.Get(Controller.Kind, $"{owner} controller.kind");
        new Limit(0, 100).Check(Controller.WalkSpeed, $"{owner} controller.walkSpeed");
        new Limit(0, 100).Check(Controller.RunSpeed, $"{owner} controller.runSpeed");
        new Limit(0, 100).Check(Controller.JumpSpeed, $"{owner} controller.jumpSpeed");
        new Limit(0, 100).Check(Controller.Range, $"{owner} controller.range");
        new Limit(0, 60).Check(Controller.Cooldown, $"{owner} controller.cooldown");
        new Limit(0, 100).Check(Controller.VerticalRange, $"{owner} controller.verticalRange");
        new Limit(0, 100).Check(Controller.RetreatRange, $"{owner} controller.retreatRange");
        new Limit(0, 10).Check(Controller.RetreatSeconds, $"{owner} controller.retreatSeconds");
        new Limit(0, 10).Check(Controller.RetreatPause, $"{owner} controller.retreatPause");
        new Limit(0, 100).Check(Controller.ChargeSpeed, $"{owner} controller.chargeSpeed");
        new Limit(0, 30).Check(Controller.ChargeStartSeconds, $"{owner} controller.chargeStartSeconds");
        new Limit(0, 30).Check(Controller.ChargeEndSeconds, $"{owner} controller.chargeEndSeconds");
        new Limit(.01f, 10).Check(Controller.BrakeSeconds, $"{owner} controller.brakeSeconds");
        Require(Controller.ChargeSpeed == 0 || Controller.ChargeEndSeconds > Controller.ChargeStartSeconds, $"{owner}: charge end must follow charge start.");
        Require((Controller.Kind == EntityControllers.Flying) == (Controller.Flight is not null), $"{owner}: flying controller requires flight settings; ground controllers cannot use them.");
        if (Controller.Flight is { } flight)
        {
            new Limit(.5f, 20).Check(flight.HoverHeight, $"{owner} flight.hoverHeight");
            new Limit(.1f, 30).Check(flight.Speed, $"{owner} flight.speed");
            new Limit(.1f, 30).Check(flight.DiveSpeed, $"{owner} flight.diveSpeed");
            new Limit(0, 10).Check(flight.LowSpeed, $"{owner} flight.lowSpeed");
        }
        var movement = part.Geometry.FirstOrDefault(g => g.Id == Movement);
        Require(movement?.Collision is not null, $"{owner}: no movement geometry '{Movement}'.");
        Require(movement.A == PartLocation.Origin && movement.B is null && movement.Frame is null && movement.Socket is null && movement.Prop is null && movement.OffsetX == 0 && movement.OffsetY == 0 && movement.Along == 0 && movement.Angle == 0 && movement.ScaleX == 1 && movement.ScaleY == 1,
            $"{owner}: movement geometry must use the part root; author its dimensions in the shape.");
        CheckShape(movement.Geometry as IShape2D, $"{owner} movement.shape");
        var props = part.Equipment.Select(e => e.Prop).ToHashSet(StringComparer.Ordinal);
        foreach (var (id, use) in Colliders)
        {
            AuthoredAsset.RequireId(id, $"{owner} collider id");
            EntityVocabulary.Require(use, ColliderUses.All, $"{owner} collider '{id}' use");
            var geometry = part.Geometry.FirstOrDefault(g => g.Id == id);
            Require(geometry?.Collision is not null, $"{owner}: no collision geometry '{id}'.");
            Require(id != Movement, $"{owner}: movement geometry cannot also be a gameplay sensor.");
            CheckShape(geometry.Geometry as IShape2D, $"{owner} collider '{id}' shape");
            Require(geometry.Prop is null || props.Contains(geometry.Prop), $"{owner}: prop '{geometry.Prop}' is not equipped.");
        }
        void ColliderOf(string id, string use, string field)
        {
            AuthoredAsset.RequireId(id, field);
            Require(Colliders.TryGetValue(id, out var named), $"{field}: no collider '{id}'.");
            Require(named == use, $"{field}: collider '{id}' is a {named} collider, not {use}.");
        }
        var actions = new HashSet<string>(StringComparer.Ordinal);
        if (Guard is { } guard)
        {
            ColliderOf(guard.Collider, ColliderUses.Guard, $"{owner} guard.collider");
        }
        var chained = Actions.Where(a => a?.Next is not null).Select(a => a.Next).ToHashSet(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            Require(action?.Hits is not null && action.Events is not null, $"{owner}: incomplete action.");
            AuthoredAsset.RequireId(action.Id, $"{owner} action id");
            var field = $"{owner} action '{action.Id}'";
            Require(actions.Add(action.Id), $"{owner}: duplicate action '{action.Id}'.");
            Require(spec.Actions.Contains(action.Id) || chained.Contains(action.Id), $"{field}: the '{spec.Id}' controller does not support it (supports: {string.Join(", ", spec.Actions)}, and any action another names as next).");
            if (action.Next is not null)
            {
                AuthoredAsset.RequireId(action.Next, field + " next");
                Require(Actions.Any(a => a?.Id == action.Next), $"{field} next: no action '{action.Next}'.");
            }
            if (action.Recovery is not null)
            {
                AuthoredAsset.RequireId(action.Recovery, field + " recovery");
            }

            Require((action.Role is null) != (action.Clip is null), $"{field}: name exactly one of role or clip.");
            if (action.Role is not null)
            {
                AuthoredAsset.RequireId(action.Role, field + " role");
            }

            if (action.Clip is not null)
            {
                AuthoredAsset.RequireId(action.Clip, field + " clip");
            }

            if (action.Mask is not null)
            {
                AuthoredAsset.RequireId(action.Mask, field + " mask");
            }

            new Limit(0, 5).Check(action.BlendIn, field + " blendIn");
            new Limit(0, 5).Check(action.BlendOut, field + " blendOut");
            Require(action.Mask is not null || action.BlendIn == 0 && action.BlendOut == 0, $"{field}: blending needs a mask; a whole-body action replaces locomotion at once.");
            var fires = action.Events.Any(e => e?.Id == EntityControllers.Fire);
            Require(fires == (action.Projectile is not null), fires ? $"{field}: a '{EntityControllers.Fire}' event needs a projectile." : $"{field}: a projectile needs a '{EntityControllers.Fire}' event to launch it.");
            if (action.Projectile is { } shot)
            {
                new Limit(.1f, 200).Check(shot.Speed, field + " projectile.speed");
                CheckShape(shot.Shape, field + " projectile.shape", 10);
                new Limit(.05f, 30).Check(shot.Lifetime, field + " projectile.lifetime");
                new Limit(0, 200).Check(shot.Gravity, field + " projectile.gravity");
                new Limit(0, 10).Check(shot.FlightSeconds, field + " projectile.flightSeconds");
            }
            var hits = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hit in action.Hits)
            {
                Require(hit?.Start is not null && hit.Finish is not null, $"{field}: incomplete hit window.");
                AuthoredAsset.RequireId(hit.Id, field + " hit id");
                Require(hits.Add(hit.Id), $"{field}: duplicate hit window '{hit.Id}'.");
                CheckTime(hit.Start, $"{field} hit '{hit.Id}' start");
                CheckTime(hit.Finish, $"{field} hit '{hit.Id}' finish");
                ColliderOf(hit.Collider, ColliderUses.Attack, $"{field} hit '{hit.Id}' collider");
            }
            foreach (var cue in action.Events)
            {
                Require(cue?.At is not null, $"{field}: incomplete event.");
                AuthoredAsset.RequireId(cue.Id, field + " event id");
                CheckTime(cue.At, $"{field} event '{cue.Id}'");
                if (cue.Sound is not null)
                {
                    AuthoredAsset.RequireId(cue.Sound, $"{field} event '{cue.Id}' sound");
                }
            }
        }
    }

    private static void CheckTime(ActionTime time, string field)
    {
        if (time.Marker is not null)
        {
            AuthoredAsset.RequireId(time.Marker, field + " marker");
            Require(time.At == 0, $"{field}: use a marker or a normalized time, not both.");
        }
        else
        {
            new Limit(0, 1).Check(time.At, field + " at");
        }
    }
}
