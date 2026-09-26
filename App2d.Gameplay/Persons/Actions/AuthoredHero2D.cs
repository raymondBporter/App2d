using App2d.Core.Characters;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>
/// The traversal player's action timing and geometry from its authored entity (<c>hero</c>): the sword's duration and damage
/// window come from its attack clip's markers (the draw-slash from the sheath, or the follow-up slash when the blade is out), and the hit box and gun muzzle from the same poses the player is drawn with,
/// at the same pixels per unit (the collider height over the model's drawn height). Positions are offsets from the collider
/// centre, Y up.
/// </summary>
public sealed class AuthoredHero2D
{
    public const string EntityId = "hero";
    private readonly float _halfHeight;

    public AuthoredHero2D(ResolvedEntity entity, Vector2 colliderSize)
    {
        Entity = entity;
        if (entity.Controller.Id != EntityControllers.Traversal) throw new InvalidDataException($"Entity '{entity.Id}' must use the '{EntityControllers.Traversal}' controller to drive the game's player.");
        Attack = entity.Actions.GetValueOrDefault(EntityControllers.Attack) ?? throw new InvalidDataException($"Entity '{entity.Id}' has no attack action.");
        FollowUp = entity.Actions.GetValueOrDefault(EntityControllers.FollowUp) ?? Attack;
        foreach (var swing in new[] { Attack, FollowUp })
        {
            if (swing.Hits.Count != 1) throw new InvalidDataException($"Entity '{entity.Id}' action '{swing.Id}' needs exactly one hit window; the sword hit box is one shape.");
            if (swing.Hits[0].Window.Width != Attack.Hits[0].Window.Width || swing.Hits[0].Window.Height != Attack.Hits[0].Window.Height || swing.Hits[0].Window.Damage != Attack.Hits[0].Window.Damage)
                throw new InvalidDataException($"Entity '{entity.Id}': the attack and follow-up hit boxes must share size and damage.");
        }
        PixelsPerUnit = colliderSize.Y / entity.Model.DrawnHeight();
        _halfHeight = colliderSize.Y / 2;
    }

    public ResolvedEntity Entity { get; }
    public ResolvedAction Attack { get; }
    /// <summary>The swing played when the blade is already out; the attack itself when the entity defines none.</summary>
    public ResolvedAction FollowUp { get; }
    public ResolvedAction Swing(bool followUp) => followUp ? FollowUp : Attack;
    public float PixelsPerUnit { get; }
    private ResolvedHit Hit => Attack.Hits[0];

    public IShape2D Shape => AxisAlignedRectangle2D.FromSize(new Vector2(Hit.Window.Width, Hit.Window.Height) * PixelsPerUnit);
    internal MeleeAttackProfile2D Profile(bool followUp)
    {
        var swing = Swing(followUp);
        return new(swing.Clip.Duration, swing.Hits[0].Start, swing.Hits[0].Finish, .1f, 0);
    }
    public int Damage => Hit.Window.Damage;

    /// <summary>The player's bolt from the shoot action's projectile, in pixels.</summary>
    internal GunPersonWeapon2D.Shot? Shot => Entity.Actions.GetValueOrDefault(EntityControllers.Shoot)?.Projectile is { } p
        ? new(new Vector2(p.Width, p.Height) * PixelsPerUnit, p.Speed * PixelsPerUnit, p.Lifetime, p.Damage) : null;

    /// <summary>The hit box centre at a time into the attack, for the given facing.</summary>
    public Vector2 Offset(float seconds, float facing, bool followUp = false)
    {
        var swing = Swing(followUp);
        var pose = Pose(swing.Clip, seconds, facing);
        return ToWorld(Bounds2D.FromPoints([.. EntityCollision.Attack(Entity, pose, swing.Hits[0]).Points]).Center);
    }

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
