using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;
using SkiaSharp;

namespace App2d.Gameplay;

internal class FireballPlayerWeapon2D : PlayerWeapon2DBase
{
    private const int PoolSize = 16;
    private const float ReleaseTime = 0.2f;

    private readonly PhysicsBody2D _ownerBody;
    private readonly CollisionSystem2D _collision;
    private readonly uint _worldLayer;
    private readonly CombatSystem2D _combat;
    private readonly PlayerPresentation2D _presentation;
    private readonly ISoundEffectSink2D _sounds;
    private readonly List<Projectile2D> _fireballs = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private float _cooldown;
    private Projectile2D? _pending;

    public FireballPlayerWeapon2D(
        string name,
        string equipmentId,
        Scene2D scene,
        PhysicsBody2D ownerBody,
        TextureCache2D textures,
        Texture2D hudTexture,
        CollisionSystem2D collision,
        uint worldLayer,
        CombatSystem2D combat,
        PlayerPresentation2D presentation,
        ISoundEffectSink2D sounds)
        : base(name, equipmentId, hudTexture)
    {
        _ownerBody = ArgGuard.RequireNotNull(ownerBody);
        _collision = ArgGuard.RequireNotNull(collision);
        _worldLayer = worldLayer;
        _combat = ArgGuard.RequireNotNull(combat);
        _presentation = ArgGuard.RequireNotNull(presentation);
        _sounds = ArgGuard.RequireNotNull(sounds);
        var shader = new TextureShader2D(
            textures.Load("effects/fireball/ember-energy.png"),
            new Vector2(96f, 96f),
            SKShaderTileMode.Mirror,
            SKShaderTileMode.Mirror);
        for (var index = 0; index < PoolSize; index++)
        {
            var worldObject = new WorldObject2D(new Circle2D(13f), shader)
            {
                IsVisible = false
            };
            _fireballs.Add(new Projectile2D(worldObject));
            scene.Add(worldObject);
        }
    }

    public override IEnumerable<SpatialObject2D> ActiveHitboxes
    {
        get
        {
            foreach (var fireball in _fireballs)
            {
                if (fireball.IsActive)
                    yield return fireball.WorldObject;
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

        if (_cooldown <= 0f && _pending is null)
        {
            foreach (var fireball in _fireballs)
            {
                if (fireball.IsActive)
                    continue;
                _pending = fireball;
                _cooldown = _presentation.ShotAnimationDuration;
                _presentation.PlayShot();
                break;
            }
        }
        return facing;
    }

    public override void BeginFrame(float deltaSeconds) =>
        _cooldown = Math.Max(0f, _cooldown - deltaSeconds);

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        foreach (var fireball in _fireballs)
        {
            if (!fireball.IsActive)
                continue;
            fireball.Update(deltaSeconds);
            if (!fireball.IsActive)
                continue;

            var direction = MathF.Sign(fireball.Velocity.X);
            var hit = _combat.TryDamageFirst(
                fireball.WorldObject,
                damage: 1,
                _ => new Vector2(direction * 390f, 190f));
            if (!hit)
            {
                hit = _collision.Overlap(
                    fireball.WorldObject,
                    _overlaps,
                    _worldLayer,
                    includeSensors: false) > 0;
            }

            if (hit)
            {
                fireball.Deactivate();
                _sounds.Play(SoundEffect2D.FireballImpact);
            }
        }
    }

    public override void ReleasePending(float facing)
    {
        if (_pending is null)
            return;
        if (!_presentation.IsPlayingShot)
        {
            _pending = null;
            return;
        }
        if (_presentation.ShotAnimationElapsedSeconds < ReleaseTime)
            return;

        _pending.Launch(
            _ownerBody.WorldObject.Transform.Position + new Vector2(facing * 55f, 8f),
            new Vector2(facing * 920f, 0f),
            lifetime: 2.25f);
        _pending = null;
        _sounds.Play(SoundEffect2D.FireballLaunch);
    }

    public override void OnDeselected() => _pending = null;

    public override void Reset()
    {
        _pending = null;
        foreach (var fireball in _fireballs)
            fireball.Deactivate();
    }
}
