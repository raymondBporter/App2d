using System.Numerics;
using App2d.Engine;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

public sealed class PlayerArsenal2D
{
    private readonly PlayerPresentation2D _presentation;
    private readonly IPlayerWeapon2D[] _weapons;
    private IPlayerWeapon2D _equippedWeapon;
    private int _weaponIndex;

    public PlayerArsenal2D(
        Scene2D scene,
        PhysicsBody2D ownerBody,
        TextureCache2D textures,
        IReadOnlyList<SpatialObject2D> platforms,
        CombatSystem2D combat,
        PlayerPresentation2D presentation,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(ownerBody);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(platforms);
        ArgGuard.ThrowIfNull(combat);
        ArgGuard.ThrowIfNull(presentation);
        ArgGuard.ThrowIfNull(sounds);

        _presentation = presentation;
        var meleeHud = textures.Load("ui/hud/weapons/sword.png");
        var magicHud = textures.Load("ui/hud/weapons/fireball.png");
        _weapons =
        [
            new SwordPlayerWeapon2D("SWORD A", "right-hand-sword-a", ownerBody, meleeHud, combat, presentation, sounds),
            new SwordPlayerWeapon2D("SWORD B", "right-hand-sword-b", ownerBody, meleeHud, combat, presentation, sounds),
            new SwordPlayerWeapon2D("SWORD C", "right-hand-sword-c", ownerBody, meleeHud, combat, presentation, sounds),
            new SwordPlayerWeapon2D("SWORD D", "right-hand-sword-d", ownerBody, meleeHud, combat, presentation, sounds),
            new SwordPlayerWeapon2D("SWORD E", "right-hand-sword-e", ownerBody, meleeHud, combat, presentation, sounds),
            new AxePlayerWeapon2D("AXE A", "right-hand-axe-a", ownerBody, meleeHud, combat, presentation, sounds),
            new AxePlayerWeapon2D("AXE B", "right-hand-axe-b", ownerBody, meleeHud, combat, presentation, sounds),
            new AxePlayerWeapon2D("AXE C", "right-hand-axe-c", ownerBody, meleeHud, combat, presentation, sounds),
            new DaggerPlayerWeapon2D("DAGGER A", "right-hand-dagger-a", ownerBody, meleeHud, combat, presentation, sounds),
            new DaggerPlayerWeapon2D("DAGGER B", "right-hand-dagger-b", ownerBody, meleeHud, combat, presentation, sounds),
            new HammerPlayerWeapon2D("HAMMER A", "right-hand-hammer-a", ownerBody, meleeHud, combat, presentation, sounds),
            new HammerPlayerWeapon2D("HAMMER B", "right-hand-hammer-b", ownerBody, meleeHud, combat, presentation, sounds),
            new HammerPlayerWeapon2D("HAMMER C", "right-hand-hammer-c", ownerBody, meleeHud, combat, presentation, sounds),
            new WandPlayerWeapon2D(
                scene,
                ownerBody,
                textures,
                magicHud,
                platforms,
                combat,
                presentation,
                sounds)
        ];
        _equippedWeapon = _weapons[0];
        _presentation.EquipRightHandWeapon(_equippedWeapon.EquipmentId);
    }

    public bool IsMeleeAttackActive =>
        _equippedWeapon is MeleePlayerWeapon2D { IsAttackActive: true };
    public string WeaponStatus =>
        $"Q/RB: SWITCH   H/J/L or X/Y/B: {_equippedWeapon.Status}";
    public string WeaponName => _equippedWeapon.Name;
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

    public void ReleasePendingWeapons(float facing)
    {
        foreach (var weapon in _weapons)
            weapon.ReleasePending(facing);
    }

    public void Reset()
    {
        foreach (var weapon in _weapons)
            weapon.Reset();
    }

    public float UseWeapon(Vector2? aimTarget, float facing) =>
        _equippedWeapon.Use(aimTarget, facing);

    public void SelectNextWeapon()
    {
        _equippedWeapon.OnDeselected();
        _weaponIndex = (_weaponIndex + 1) % _weapons.Length;
        _equippedWeapon = _weapons[_weaponIndex];
        _presentation.EquipRightHandWeapon(_equippedWeapon.EquipmentId);
    }
}
