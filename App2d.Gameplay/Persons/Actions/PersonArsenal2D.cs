using App2d.Collision;
using App2d.Core;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed class PersonArsenal2D : IPersonActionSet2D
{
    private readonly IPersonWeapon2D[] _weapons;
    private readonly UnarmedPersonActions2D _unarmed;
    private readonly Texture2D _unarmedHud;
    private int _equipmentIndex;

    public PersonArsenal2D(
        Scene2D scene,
        PhysicsBody2D ownerBody,
        TextureCache2D textures,
        CollisionSystem2D collision,
        uint worldLayer,
        uint targetLayer,
        CombatFaction2D ownerFaction,
        CombatSystem2D combat,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(ownerBody);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(combat);
        ArgGuard.ThrowIfNull(sounds);

        var swordHud = textures.Load("ui/hud/weapons/sword.png");
        var gunHud = textures.Load("ui/hud/weapons/gun.png");
        _unarmedHud = textures.Load("ui/hud/weapons/unarmed.png");
        _weapons =
        [
            new SwordPersonWeapon2D("SWORD","sword",ownerBody,swordHud,ownerFaction,targetLayer,combat,duration => MeleeAttackStarted?.Invoke(duration),sounds),
            new GunPersonWeapon2D(scene,ownerBody,textures,gunHud,collision,worldLayer,targetLayer,ownerFaction,combat,() => ShotStarted?.Invoke(),sounds)
        ];
        _unarmed = new UnarmedPersonActions2D(
            ownerBody,
            ownerFaction,
            targetLayer,
            combat);
        _unarmed.AttackStarted += (kind, duration) =>
            UnarmedAttackStarted?.Invoke(kind, duration);
    }

    public event Action<string>? EquipmentChanged;
    public event Action<float>? MeleeAttackStarted;
    public event Action? ShotStarted;
    public event Action<UnarmedAttackKind2D, float>? UnarmedAttackStarted;

    public bool IsMeleeAttackActive =>
        IsUnarmed
            ? _unarmed.IsAttackActive
            : EquippedWeapon is MeleePersonWeapon2D { IsAttackActive: true };
    public string WeaponStatus => IsUnarmed
        ? "Q/Y: SWITCH   J/CLICK or X: PUNCH   K/RIGHT CLICK: KICK"
        : $"Q/Y: SWITCH   J/CLICK or X: {EquippedWeapon.Status}";
    public string WeaponName => IsUnarmed ? "FISTS" : EquippedWeapon.Name;
    public string EquipmentId => IsUnarmed ? "unarmed" : EquippedWeapon.EquipmentId;
    public Texture2D WeaponHudTexture => IsUnarmed ? _unarmedHud : EquippedWeapon.HudTexture;

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

    public void UpdateBeforePhysics(float deltaSeconds)
    {
        foreach (var weapon in _weapons)
            weapon.UpdateBeforePhysics(deltaSeconds);
        _unarmed.UpdateBeforePhysics(deltaSeconds);
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

    public float UseWeapon(Vector2? aimTarget, float facing) =>
        IsUnarmed
            ? _unarmed.UsePrimary(aimTarget, facing)
            : EquippedWeapon.Use(aimTarget, facing);

    public float UsePrimary(Vector2? aimTarget, float facing) =>
        UseWeapon(aimTarget, facing);

    public float UseSecondary(Vector2? aimTarget, float facing) =>
        IsUnarmed
            ? _unarmed.UseSecondary(aimTarget, facing)
            : facing;

    public void SelectNextWeapon()
    {
        if (IsUnarmed)
            _unarmed.Reset();
        else
            EquippedWeapon.OnDeselected();
        _equipmentIndex = (_equipmentIndex + 1) % (_weapons.Length + 1);
        EquipmentChanged?.Invoke(EquipmentId);
    }

    public void SelectNext() => SelectNextWeapon();
}
