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
    private IPersonWeapon2D _equippedWeapon;
    private int _weaponIndex;

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
        _weapons =
        [
            new SwordPersonWeapon2D("SWORD","sword",ownerBody,swordHud,ownerFaction,targetLayer,combat,duration => MeleeAttackStarted?.Invoke(duration),sounds),
            new GunPersonWeapon2D(scene,ownerBody,textures,gunHud,collision,worldLayer,targetLayer,ownerFaction,combat,() => ShotStarted?.Invoke(),sounds)
        ];
        _equippedWeapon = _weapons[0];
    }

    public event Action<string>? EquipmentChanged;
    public event Action<float>? MeleeAttackStarted;
    public event Action? ShotStarted;

    public bool IsMeleeAttackActive =>
        _equippedWeapon is MeleePersonWeapon2D { IsAttackActive: true };
    public string WeaponStatus =>
        $"Q/Y: SWITCH   J/CLICK or X: {_equippedWeapon.Status}";
    public string WeaponName => _equippedWeapon.Name;
    public string EquipmentId => _equippedWeapon.EquipmentId;
    public Texture2D WeaponHudTexture => _equippedWeapon.HudTexture;

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        foreach (var weapon in _weapons)
        {
            foreach (var hitbox in weapon.ActiveHitboxes)
                yield return hitbox;
        }
    }

    public void BeginFrame(float deltaSeconds)
    {
        foreach (var weapon in _weapons)
            weapon.BeginFrame(deltaSeconds);
    }

    public void UpdateBeforePhysics(float deltaSeconds)
    {
        foreach (var weapon in _weapons)
            weapon.UpdateBeforePhysics(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        foreach (var weapon in _weapons)
            weapon.UpdateAfterPhysics(deltaSeconds, facing);
    }

    public void Reset()
    {
        foreach (var weapon in _weapons)
            weapon.Reset();
    }

    public float UseWeapon(Vector2? aimTarget, float facing) =>
        _equippedWeapon.Use(aimTarget, facing);

    public float UsePrimary(Vector2? aimTarget, float facing) =>
        UseWeapon(aimTarget, facing);

    public void SelectNextWeapon()
    {
        _equippedWeapon.OnDeselected();
        _weaponIndex = (_weaponIndex + 1) % _weapons.Length;
        _equippedWeapon = _weapons[_weaponIndex];
        EquipmentChanged?.Invoke(_equippedWeapon.EquipmentId);
    }

    public void SelectNext() => SelectNextWeapon();
}
