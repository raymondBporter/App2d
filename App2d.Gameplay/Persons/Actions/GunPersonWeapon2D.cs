using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay;

/// <summary>
/// Minimal horizontal pistol used by the baked stick-figure prototype.
/// </summary>
internal sealed class GunPersonWeapon2D : PersonWeapon2DBase
{
    private const int PoolSize = 16;
    private const float ReleaseTime = 0.08f;

    private readonly PhysicsBody2D _ownerBody;
    private readonly CollisionSystem2D _collision;
    private readonly uint _worldLayer;
    private readonly uint _targetLayer;
    private readonly CombatFaction2D _ownerFaction;
    private readonly CombatSystem2D _combat;
    private readonly Action _shotStarted;
    private readonly ISoundEffectSink2D _sounds;
    private readonly List<BulletState> _bullets = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private float _cooldown;
    private float _pendingSeconds;
    private BulletState? _pending;

    public GunPersonWeapon2D(
        Scene2D scene,
        PhysicsBody2D ownerBody,
        TextureCache2D textures,
        Texture2D hudTexture,
        CollisionSystem2D collision,
        uint worldLayer,
        uint targetLayer,
        CombatFaction2D ownerFaction,
        CombatSystem2D combat,
        Action shotStarted,
        ISoundEffectSink2D sounds)
        : base("GUN", "gun", hudTexture)
    {
        ArgGuard.ThrowIfNull(scene);
        _ownerBody = ArgGuard.RequireNotNull(ownerBody);
        ArgGuard.ThrowIfNull(textures);
        _collision = ArgGuard.RequireNotNull(collision);
        _worldLayer = worldLayer;
        _targetLayer = targetLayer;
        _ownerFaction = ownerFaction;
        _combat = ArgGuard.RequireNotNull(combat);
        _shotStarted = ArgGuard.RequireNotNull(shotStarted);
        _sounds = ArgGuard.RequireNotNull(sounds);

        var bulletTexture = textures.Load("effects/bullet/orange.png");
        for (var index = 0; index < PoolSize; index++)
        {
            var shader = new SpriteShader2D(bulletTexture);
            var worldObject = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(28f, 12f)),
                shader)
            {
                IsVisible = false,
                ZIndex = 2
            };
            var projectile = new Projectile2D(worldObject);
            _bullets.Add(new BulletState(projectile, shader));
            scene.Add(worldObject);
        }
    }

    public override IEnumerable<SpatialObject2D> ActiveHitboxes
    {
        get
        {
            foreach (var bullet in _bullets)
            {
                if (bullet.Projectile.IsActive)
                    yield return bullet.Projectile.WorldObject;
            }
        }
    }

    public override float Use(Vector2? aimTarget, float facing)
    {
        if (aimTarget is { } target)
        {
            var deltaX = target.X - _ownerBody.WorldObject.Transform.Position.X;
            if (MathF.Abs(deltaX) > 1f)
                facing = MathF.Sign(deltaX);
        }

        if (_cooldown > 0f || _pending is not null)
            return facing;

        foreach (var bullet in _bullets)
        {
            if (bullet.Projectile.IsActive)
                continue;

            _pending = bullet;
            _pendingSeconds = 0f;
            _cooldown = 0.22f;
            _shotStarted();
            break;
        }
        return facing;
    }

    public override void BeginFrame(float deltaSeconds) =>
        _cooldown = Math.Max(0f, _cooldown - deltaSeconds);

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        foreach (var bullet in _bullets)
        {
            var projectile = bullet.Projectile;
            if (!projectile.IsActive)
                continue;

            projectile.Update(deltaSeconds);
            if (!projectile.IsActive)
                continue;

            var direction = MathF.Sign(projectile.Velocity.X);
            var hit = _combat.TryDamageFirst(
                projectile.WorldObject,
                _ownerFaction,
                _targetLayer,
                damage: 2,
                _ => new Vector2(direction * 450f, 140f));
            if (!hit)
            {
                hit = _collision.Overlap(
                    projectile.WorldObject,
                    _overlaps,
                    _worldLayer,
                    includeSensors: false) > 0;
            }

            if (hit)
            {
                projectile.Deactivate();
                _sounds.Play(SoundEffect2D.FireballImpact);
            }
        }

        if (_pending is not null)
        {
            _pendingSeconds += deltaSeconds;
            if (_pendingSeconds >= ReleaseTime)
            {
                _pending.Shader.FlipX = facing < 0f;
                _pending.Projectile.Launch(
                    _ownerBody.WorldObject.Transform.Position +
                        new Vector2(facing * 64f, 10f),
                    new Vector2(facing * 1250f, 0f),
                    lifetime: 1.5f);
                _pending = null;
                _pendingSeconds = 0f;
                _sounds.Play(SoundEffect2D.FireballLaunch);
            }
        }
    }

    public override void OnDeselected()
    {
        _pending = null;
        _pendingSeconds = 0f;
    }

    public override void Reset()
    {
        _pending = null;
        _pendingSeconds = 0f;
        foreach (var bullet in _bullets)
            bullet.Projectile.Deactivate();
    }

    private sealed record BulletState(Projectile2D Projectile, SpriteShader2D Shader);
}
