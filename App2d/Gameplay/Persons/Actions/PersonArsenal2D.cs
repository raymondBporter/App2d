using App2d.Core.Validation;
using App2d.Contracts.Combat;
using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Geometry;
using App2d.Core.Physics;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Simulation;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class PersonArsenal2D : ISessionPlayerActions2D
{
    private readonly SwordPersonWeapon2D _sword;
    private readonly GunPersonWeapon2D _gun;

    public PersonArsenal2D(
        EntityIdAllocator2D ids,
        PhysicsBody2D ownerBody,
        Vector2 muzzleOffset,
        CollisionSystem2D collision,
        uint worldLayer,
        uint targetLayer,
        CombatFaction2D ownerFaction,
        CombatSystem2D combat,
        Func<Rect2D, bool>? overlapsSpikes = null,
        AuthoredHero2D? hero = null, Func<float, Vector2>? muzzle = null,
        Health2D? health = null, SpellTuning2D? spells = null)
    {
        ArgGuard.ThrowIfNull(ids);
        ArgGuard.ThrowIfNull(ownerBody);
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(combat);
        _spellHealth = health ?? (combat.Combatants.Find(ownerBody.EntityId) as Person2D)?.Health;
        _spellTuning = (spells ?? new SpellTuning2D()).Validate();
        _spellPosition = () => ownerBody.WorldObject.Transform.Position;
        _energy = _spellTuning.StartingEnergy;

        _sword = new SwordPersonWeapon2D(ids, ownerBody, ownerFaction, targetLayer, combat,
            duration => MeleeAttackStarted?.Invoke(duration), Publish,
            duration => DownAttackStarted?.Invoke(duration), overlapsSpikes,
            hero);
        _gun = new GunPersonWeapon2D(ids, ownerBody, muzzleOffset, collision, worldLayer, targetLayer,
            ownerFaction, combat, () => ShotStarted?.Invoke(), Publish, muzzle, hero?.Shot,
            _spellTuning.ShotChargeSeconds, _spellTuning.ShotRecoverySeconds,
            () => SpendEnergy(_spellTuning.ShotCost));
    }

    public event Action<WeaponEvent2D>? WeaponOccurred;
    private void Publish(WeaponEvent2D occurrence) => WeaponOccurred?.Invoke(occurrence);
    public WeaponState2D CaptureWeaponState() => _gun.CaptureState();
    public PersonActionState2D CaptureActionState() => _sword.IsAttackActive
        ? _sword.CaptureActionState() : _gun.CaptureActionState();

    public event Action<float>? MeleeAttackStarted;
    public event Action<float>? DownAttackStarted;
    public event Action? ShotStarted;
    public event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted { add { } remove { } }

    public bool ConsumeDownAttackBounce() => _sword.ConsumeBounce();
    public bool IsChargingPrimary => _gun.IsCharging;
    public void InterruptPrimary()
    {
        _gun.CancelCharge();
        _sword.Reset();
        CancelHealing();
        _healNeedsRelease = true;
    }

    public bool IsMeleeAttackActive => _sword.IsAttackActive;
    public EquipmentKind2D Equipment => EquipmentKind2D.Sword;

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        foreach (var hitbox in _sword.ActiveHitboxes) yield return hitbox;
        foreach (var hitbox in _gun.ActiveHitboxes) yield return hitbox;
    }

    public IEnumerable<SpatialObject2D> GetActiveSwordHitboxes() =>
        _sword.ActiveHitboxes;

    public void BeginFrame(float deltaSeconds)
    {
        _sword.BeginFrame(deltaSeconds);
        _gun.BeginFrame(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        RechargeEnergy(deltaSeconds);
        _sword.UpdateAfterPhysics(deltaSeconds, facing);
        _gun.UpdateAfterPhysics(deltaSeconds, facing);
        UpdateHealing(deltaSeconds);
    }

    public void Reset()
    {
        _sword.Reset();
        _gun.Reset();
        CancelHealing();
        _healNeedsRelease = true;
        _energy = _spellTuning.StartingEnergy;
        _energyRecharge = 0;
    }

    public float UsePrimary(float facing) =>
        SpellBusy ? facing : _sword.Use(facing);

    public float UseDownwardPrimary(float facing) =>
        SpellBusy ? facing : _sword.UseDownward(facing);

    public float UseSecondary(float facing) => facing;
}
