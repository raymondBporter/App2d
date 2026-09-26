using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Simulation;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class PersonArsenal2D : ISessionPlayerActions2D
{
    private readonly SwordPersonWeapon2D _sword;
    private readonly GunPersonWeapon2D _gun;
    private readonly IPersonWeapon2D[] _weapons;
    private readonly UnarmedPersonActions2D _unarmed;
    private int _equipmentIndex;

    public PersonArsenal2D(
        EntityIdAllocator2D ids,
        PhysicsBody2D ownerBody,
        Vector2 muzzleOffset,
        CollisionSystem2D collision,
        uint worldLayer,
        uint targetLayer,
        CombatFaction2D ownerFaction,
        CombatSystem2D combat,
        Func<Bounds2D, bool>? overlapsSpikes = null,
        AuthoredHero2D? hero = null, Func<float, Vector2>? muzzle = null)
    {
        ArgGuard.ThrowIfNull(ids);
        ArgGuard.ThrowIfNull(ownerBody);
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(combat);

        _sword = new SwordPersonWeapon2D(ids, ownerBody, ownerFaction, targetLayer, combat,
            duration => MeleeAttackStarted?.Invoke(duration), Publish,
            duration => DownAttackStarted?.Invoke(duration), overlapsSpikes,
            hero);
        _gun = new GunPersonWeapon2D(ids, ownerBody, muzzleOffset, collision, worldLayer, targetLayer,
            ownerFaction, combat, () => ShotStarted?.Invoke(), Publish, muzzle, hero?.Shot);
        _weapons = [_sword, _gun];
        _unarmed = new UnarmedPersonActions2D(
            ids,
            ownerBody,
            ownerFaction,
            targetLayer,
            combat);
        _unarmed.AttackStarted += (kind, duration) =>
            UnarmedAttackStarted?.Invoke(kind, duration);
    }

    public event Action<WeaponEvent2D>? WeaponOccurred;
    private void Publish(WeaponEvent2D occurrence) => WeaponOccurred?.Invoke(occurrence);
    public WeaponState2D CaptureWeaponState() => _gun.CaptureState();
    public PersonActionState2D CaptureActionState() => IsUnarmed ? _unarmed.CaptureActionState() : EquippedWeapon.CaptureActionState();

    public event Action<EquipmentKind2D>? EquipmentChanged;
    public event Action<float>? MeleeAttackStarted;
    public event Action<float>? DownAttackStarted;
    public event Action? ShotStarted;
    public event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;

    public bool ConsumeDownAttackBounce() => _sword.ConsumeBounce();
    public bool IsChargingPrimary => !IsUnarmed && EquippedWeapon == _gun && _gun.IsCharging;
    public void SetPrimaryInput(bool held, bool canCharge, bool released = false) =>
        _gun.SetInput(held, canCharge && !IsUnarmed && EquippedWeapon == _gun);
    public void InterruptPrimary()
    {
        _gun.CancelCharge();
        _sword.Reset();
    }

    public bool IsMeleeAttackActive =>
        IsUnarmed
            ? _unarmed.IsAttackActive
            : EquippedWeapon is MeleePersonWeapon2D { IsAttackActive: true };
    public EquipmentKind2D Equipment => IsUnarmed ? EquipmentKind2D.Unarmed : EquippedWeapon.Kind;

    private bool IsUnarmed => _equipmentIndex == _weapons.Length;
    private IPersonWeapon2D EquippedWeapon => _weapons[_equipmentIndex];

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        foreach (var weapon in _weapons)
        {
            foreach (var hitbox in weapon.ActiveHitboxes)
                yield return hitbox;
        }
        foreach (var hitbox in _unarmed.GetActiveAttackHitboxes())
            yield return hitbox;
    }

    public void BeginFrame(float deltaSeconds)
    {
        foreach (var weapon in _weapons)
            weapon.BeginFrame(deltaSeconds);
        _unarmed.BeginFrame(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        foreach (var weapon in _weapons)
            weapon.UpdateAfterPhysics(deltaSeconds, facing);
        _unarmed.UpdateAfterPhysics(deltaSeconds, facing);
    }

    public void Reset()
    {
        foreach (var weapon in _weapons)
            weapon.Reset();
        _unarmed.Reset();
    }

    public float UsePrimary(float facing) =>
        IsUnarmed
            ? _unarmed.UsePrimary(facing)
            : EquippedWeapon.Use(facing);

    public float UseDownwardPrimary(float facing) =>
        !IsUnarmed && EquippedWeapon is SwordPersonWeapon2D sword
            ? sword.UseDownward(facing)
            : UsePrimary(facing);

    public float UseSecondary(float facing) =>
        IsUnarmed
            ? _unarmed.UseSecondary(facing)
            : facing;

    public void SelectNext()
    {
        if (IsUnarmed)
            _unarmed.Reset();
        else
            EquippedWeapon.OnDeselected();
        _equipmentIndex = (_equipmentIndex + 1) % (_weapons.Length + 1);
        EquipmentChanged?.Invoke(Equipment);
    }
}
