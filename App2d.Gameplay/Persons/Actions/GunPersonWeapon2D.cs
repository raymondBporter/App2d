using App2d.Collision;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Gameplay.Assets;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>Hold to charge one shot, fired automatically as soon as charging completes.</summary>
internal sealed class GunPersonWeapon2D : PersonWeapon2DBase
{
    private const float ChargeSeconds = 0.6f;
    private const float BoltWidth = 30f;
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
    private readonly Texture2D[] _chargeHud = new Texture2D[61];
    private readonly WorldObject2D _glow;
    private readonly SpriteShader2D _glowShader;
    private readonly AnimationPlayer2D<Texture2D> _chargeAnimation = new();
    private readonly WorldObject2D _flash;
    private readonly SpatialObject2D _barrelPath;
    private readonly Vector2 _muzzleOffset;
    private SoundEffectVoice2D _chargeVoice;
    private float _chargeTime;
    private float _direction = 1f;
    private float _flashSeconds;
    private float _cancelSeconds;
    private float _cancelProgress;
    private bool _canCharge;
    private bool _needsRelease;

    public GunPersonWeapon2D(
        Scene2D scene, PhysicsBody2D ownerBody, TextureCache2D textures,
        Texture2D hudTexture, CollisionSystem2D collision,
        uint worldLayer, uint targetLayer, CombatFaction2D ownerFaction,
        CombatSystem2D combat, Action shotStarted, ISoundEffectSink2D sounds)
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

        // Pixel socket (418, 206) in the 512px standing pistol frame. Derive world
        // placement from the same geometry manifest as PersonPresentation2D.
        var geometry = PlayerGeometryAssets2D.Load(textures.ContentRoot);
        _muzzleOffset = new Vector2(
            (418f / 512f - 0.5f) * geometry.VisualSize.X,
            (geometry.FootAnchorYFraction - 206f / 512f) * geometry.VisualSize.Y -
                geometry.StandingColliderSize.Y * 0.5f);
        _barrelPath = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(
            new Vector2(_muzzleOffset.X + BoltWidth, 4f)));
        for (var index = 0; index < _chargeHud.Length; index++)
            _chargeHud[index] = textures.Load($"ui/hud/gun-charge/frame-{index:0000}.png");

        _glow = CreateEffect("charge.png", new Vector2(30f));
        _glowShader = (SpriteShader2D)_glow.Shader;
        _chargeAnimation.Play(new AnimationClip2D<Texture2D>(
            Enumerable.Range(0, 24).Select(frame =>
                textures.Load($"effects/gun/ready/frame-{frame:0000}.png")),
            framesPerSecond: 30f));
        _chargeAnimation.Stop();
        _flash = CreateEffect("flash.png", new Vector2(42.5f, 27.5f));
        var bulletTexture = textures.Load("effects/gun/bolt.png");
        var trailTextures = Enumerable.Range(0, 8)
            .Select(frame => textures.Load($"effects/gun/trail-{frame:00}.png")).ToArray();
        for (var index = 0; index < 16; index++)
        {
            var trail = new GunBoltTrail2D(scene, trailTextures);
            var shader = new SpriteShader2D(bulletTexture);
            var worldObject = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(BoltWidth, 10f)), shader)
            { IsVisible = false, ZIndex = 2 };
            _bullets.Add(new BulletState(new Projectile2D(worldObject), shader, trail));
            scene.Add(worldObject);
        }

        WorldObject2D CreateEffect(string name, Vector2 size)
        {
            var effect = new WorldObject2D(AxisAlignedRectangle2D.FromSize(size),
                new SpriteShader2D(textures.Load($"effects/gun/{name}")))
            { IsVisible = false, ZIndex = 2 };
            scene.Add(effect);
            return effect;
        }
    }

    public bool IsCharging { get; private set; }
    public float ChargeProgress => Math.Clamp(_chargeTime / ChargeSeconds, 0f, 1f);
    public override Texture2D HudTexture => _chargeHud[(int)MathF.Round(60f *
        (IsCharging ? ChargeProgress : _flashSeconds > 0f ? 1f :
            _cancelProgress * _cancelSeconds / 0.14f))];

    public override IEnumerable<SpatialObject2D> ActiveHitboxes
    {
        get
        {
            foreach (var bullet in _bullets)
                if (bullet.Projectile.IsActive)
                    yield return bullet.Projectile.WorldObject;
        }
    }

    public void SetInput(bool held, bool canCharge)
    {
        if (!held)
            _needsRelease = false;
        _canCharge = held && canCharge;
        if (!_canCharge)
            CancelCharge();
    }

    public override float Use(Vector2? aimTarget, float facing)
    {
        if (_needsRelease)
            return facing;
        _needsRelease = true;
        if (!_canCharge || _flashSeconds > 0f)
            return facing;
        if (aimTarget is { } target &&
            MathF.Abs(target.X - _ownerBody.WorldObject.Transform.Position.X) > 1f)
            facing = MathF.Sign(target.X - _ownerBody.WorldObject.Transform.Position.X);
        _direction = facing;
        _chargeTime = _cancelSeconds = 0f;
        _chargeAnimation.Stop();
        _chargeAnimation.Resume();
        _glowShader.Texture = _chargeAnimation.CurrentFrame;
        IsCharging = true;
        _chargeVoice = _sounds.BeginAt(SoundEffect2D.GunCharge, MuzzlePosition);
        return facing;
    }

    public override void BeginFrame(float deltaSeconds)
    {
        _flashSeconds = Math.Max(0f, _flashSeconds - deltaSeconds);
        _cancelSeconds = Math.Max(0f, _cancelSeconds - deltaSeconds);
    }

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        if (IsCharging)
        {
            _direction = facing;
            _chargeVoice.SetPosition(MuzzlePosition);
        }
        foreach (var bullet in _bullets)
        {
            var projectile = bullet.Projectile;
            if (!projectile.IsActive)
            {
                bullet.Trail.Fade(deltaSeconds);
                continue;
            }
            projectile.Update(deltaSeconds);
            if (projectile.IsActive)
                ResolveHit(projectile);
            if (projectile.IsActive)
                FollowTrail(bullet);
            else
                bullet.Trail.Fade(deltaSeconds);
        }
        if (IsCharging)
        {
            _chargeAnimation.Update(deltaSeconds);
            _glowShader.Texture = _chargeAnimation.CurrentFrame;
            _chargeTime += deltaSeconds;
            if (_chargeTime + 0.000001f >= ChargeSeconds)
                Fire();
        }
        _glow.IsVisible = IsCharging || _cancelSeconds > 0f;
        _glow.Transform.Position = MuzzlePosition;
        var glowScale = IsCharging ? 0.2f + 0.8f * ChargeProgress :
            (0.2f + 0.8f * _cancelProgress) * _cancelSeconds / 0.14f;
        _glow.Transform.Scale = new Vector2(Math.Max(0.001f, glowScale));
        _flash.IsVisible = _flashSeconds > 0f;
    }

    private Vector2 MuzzlePosition => _ownerBody.WorldObject.Transform.Position +
        new Vector2(_direction * _muzzleOffset.X, _muzzleOffset.Y);

    private void Fire()
    {
        IsCharging = false;
        _chargeAnimation.Pause();
        _chargeVoice.Stop();
        _chargeTime = 0f;
        var muzzle = MuzzlePosition;
        _flashSeconds = 0.06f;
        _flash.Transform.Position = muzzle;
        _shotStarted();
        _sounds.PlayAt(SoundEffect2D.GunFire, muzzle);
        // Never spawn a projectile beyond a thin wall intersecting the barrel.
        _barrelPath.Transform.Position = _ownerBody.WorldObject.Transform.Position +
            new Vector2(_direction * (_muzzleOffset.X + BoltWidth) * 0.5f, _muzzleOffset.Y);
        if (_collision.Overlap(_barrelPath, _overlaps, _worldLayer, includeSensors: false) > 0)
        {
            _sounds.PlayAt(SoundEffect2D.GunImpact, muzzle);
            return;
        }
        foreach (var bullet in _bullets)
        {
            if (bullet.Projectile.IsActive || bullet.Trail.IsVisible)
                continue;
            bullet.Shader.FlipX = _direction < 0f;
            // Projectile position is its center; its rear meets the muzzle.
            bullet.Projectile.Launch(muzzle + new Vector2(_direction * BoltWidth * 0.5f, 0f),
                new Vector2(_direction * 1250f, 0f), lifetime: 1.5f);
            bullet.Trail.Begin(muzzle, _direction, bullet.Projectile.Velocity.Length());
            ResolveHit(bullet.Projectile);
            if (bullet.Projectile.IsActive)
                FollowTrail(bullet);
            break;
        }
    }

    private static void FollowTrail(BulletState bullet)
    {
        // The brightest core is at x = 0.675 of the pre-baked bolt texture.
        var head = bullet.Projectile.WorldObject.Transform.Position +
            new Vector2(MathF.Sign(bullet.Projectile.Velocity.X) * BoltWidth * 0.175f, 0f);
        bullet.Trail.Follow(head);
    }

    private void ResolveHit(Projectile2D projectile)
    {
        var direction = MathF.Sign(projectile.Velocity.X);
        var hit = _combat.TryDamageFirst(projectile.WorldObject, _ownerFaction,
            _targetLayer, damage: 2, _ => new Vector2(direction * 450f, 140f));
        if (!hit)
            hit = _collision.Overlap(projectile.WorldObject, _overlaps,
                _worldLayer, includeSensors: false) > 0;
        if (hit)
        {
            projectile.Deactivate();
            _sounds.PlayAt(SoundEffect2D.GunImpact, projectile.WorldObject.Transform.Position);
        }
    }

    public void CancelCharge()
    {
        _chargeAnimation.Pause();
        _canCharge = false;
        if (!IsCharging)
            return;
        _cancelProgress = ChargeProgress;
        _cancelSeconds = 0.14f;
        IsCharging = false;
        _chargeTime = 0f;
        _chargeVoice.Stop();
        _sounds.PlayAt(SoundEffect2D.GunCancel, MuzzlePosition);
    }

    public override void OnDeselected()
    {
        CancelCharge();
        _glow.IsVisible = _flash.IsVisible = false;
        _cancelSeconds = _flashSeconds = 0f;
    }

    public override void Reset()
    {
        OnDeselected();
        _needsRelease = true;
        foreach (var bullet in _bullets)
        {
            bullet.Projectile.Deactivate();
            bullet.Trail.Reset();
        }
    }

    private sealed record BulletState(Projectile2D Projectile, SpriteShader2D Shader, GunBoltTrail2D Trail);
}
