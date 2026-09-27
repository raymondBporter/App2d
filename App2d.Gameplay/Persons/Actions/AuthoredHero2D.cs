using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>
/// The traversal player's action timing and geometry from its authored entity (<c>hero</c>). The sword swings are the attack
/// action and the actions its <see cref="ResolvedAction.Next"/> chain reaches: each takes its duration and damage window
/// from its clip's markers and its hit from a box fixed to the player, and a press chains to the next swing while one runs
/// or its recovery clip still holds the sword. The gun muzzle comes from the same poses the player is drawn with. Sizes use
/// the same pixels per unit (the collider height over the model's drawn height); positions are offsets from the collider
/// centre, Y up.
/// </summary>
public sealed class AuthoredHero2D
{
    public const string EntityId = "hero";
    private readonly float _halfHeight;
    private readonly Dictionary<string, IShape2D> _shapes = new(StringComparer.Ordinal);

    public AuthoredHero2D(ResolvedEntity entity, Vector2 colliderSize)
    {
        Entity = entity;
        if (entity.Controller.Id != EntityControllers.Traversal) throw new InvalidDataException($"Entity '{entity.Id}' must use the '{EntityControllers.Traversal}' controller to drive the game's player.");
        Attack = entity.Actions.GetValueOrDefault(EntityControllers.Attack) ?? throw new InvalidDataException($"Entity '{entity.Id}' has no attack action.");
        PixelsPerUnit = colliderSize.Y / entity.Model.DrawnHeight();
        _halfHeight = colliderSize.Y / 2;
        for (var swing = Attack; swing is not null && !_shapes.ContainsKey(swing.Id); swing = swing.Next is { } next ? entity.Actions[next] : null)
        {
            if (swing.Hits is not [{ Window: { Socket: null, Prop: null } hit }])
                throw new InvalidDataException($"Entity '{entity.Id}' action '{swing.Id}' needs exactly one hit window, fixed to the actor (no socket or prop): the player's sword hit is a box on the player.");
            _shapes[swing.Id] = AxisAlignedRectangle2D.FromSize(new Vector2(hit.Width, hit.Height) * PixelsPerUnit);
        }
    }

    public ResolvedEntity Entity { get; }
    public ResolvedAction Attack { get; }
    public ResolvedAction Swing(string? id) => id is null ? Attack : Entity.Actions[id];
    public float PixelsPerUnit { get; }

    public IShape2D Shape(string? swing = null) => _shapes[Swing(swing).Id];
    public int Damage(string? swing = null) => Swing(swing).Hits[0].Window.Damage;

    /// <summary>
    /// A swing's timing and its fixed box, centred where the entity puts it (mirrored by facing when placed). A press any
    /// time during the swing is held for its end, so mashing never drops one.
    /// </summary>
    internal MeleeAttackProfile2D Profile(string? id)
    {
        var swing = Swing(id); var hit = swing.Hits[0];
        var centre = ToWorld(new(hit.Window.OffsetX, hit.Window.OffsetY));
        return new(swing.Clip.Duration, hit.Start, hit.Finish, swing.Clip.Duration, centre.X, centre.Y) { Shape = _shapes[swing.Id] };
    }

    /// <summary>
    /// The swing a press plays <paramref name="sinceEnd"/> seconds after <paramref name="last"/> ended (zero while it runs):
    /// its next while its recovery still holds the sword out, otherwise the attack from the top.
    /// </summary>
    public string Chain(string? last, float sinceEnd)
    {
        if (last is null || Swing(last).Next is not { } next) return Attack.Id;
        var recovery = Swing(last).Recovery;
        var window = recovery is null ? 0 : recovery.Markers.FirstOrDefault(m => m.Id == PersonLoadout.SheatheMarker)?.Time ?? recovery.Duration;
        return sinceEnd <= window ? next : Attack.Id;
    }

    /// <summary>The player's bolt from the shoot action's projectile, in pixels.</summary>
    internal GunPersonWeapon2D.Shot? Shot => Entity.Actions.GetValueOrDefault(EntityControllers.Shoot)?.Projectile is { } p
        ? new(new Vector2(p.Width, p.Height) * PixelsPerUnit, p.Speed * PixelsPerUnit, p.Lifetime, p.Damage) : null;

    /// <summary>Where a shot leaves the pistol: the muzzle at the shoot (or wall-shot) action's fire event.</summary>
    public Vector2 Muzzle(float facing, bool wallGrip)
    {
        var action = (wallGrip ? Entity.Actions.GetValueOrDefault(EntityControllers.WallShot) : null) ?? Entity.Actions.GetValueOrDefault(EntityControllers.Shoot)
            ?? throw new InvalidOperationException($"Entity '{Entity.Id}' has no shoot action.");
        var fire = action.Events.First(e => e.Event.Id == EntityControllers.Fire).Seconds;
        var pose = Pose(action.Clip, fire, facing);
        var gun = Entity.Equipment.First(e => e.Prop.Muzzle is not null);
        var muzzle = ActorPose.PropPoint(pose.Socket(gun.Socket), gun.Prop, gun.Prop.Muzzle!.Value);
        return ToWorld(new(muzzle.X, muzzle.Y));
    }

    private ActorPose Pose(MotionClip clip, float seconds, float facing) =>
        new(PoseEvaluator.Sample(Entity.Model, clip, Math.Clamp(seconds, 0, clip.Duration), input: new() { InPlace = true }), Vector2.Zero, facing < 0 ? -1 : 1);

    private Vector2 ToWorld(Vector2 units) => units * PixelsPerUnit - new Vector2(0, _halfHeight);
}
