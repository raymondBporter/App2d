using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Intersections;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using App2d.Gameplay.Audio;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class PlayerArsenal2D
{
    private static readonly PlayerWeapon2D[] WeaponOrder =
    [
        PlayerWeapon2D.Sword,
        PlayerWeapon2D.BionicArm,
        PlayerWeapon2D.BallAndChain,
        PlayerWeapon2D.Fireball
    ];

    private const float FireballReleaseTime = 0.2f;
    private const float GrappleLatchMaxNormalY = -0.9f;
    private const int FireballPoolSize = 16;

    private readonly PhysicsWorld2D _physics;
    private readonly PhysicsBody2D _ownerBody;
    private readonly TraversalMetrics2D _traversal;
    private readonly IReadOnlyList<WorldObject2D> _platforms;
    private readonly CombatSystem2D _combat;
    private readonly PlayerPresentation2D _presentation;
    private readonly ISoundEffectSink2D _sounds;
    private readonly SwordAttack2D _sword;
    private readonly GrappleArm2D _grappleArm;
    private readonly BallAndChain2D _ballAndChain;
    private readonly Texture2D _swordHudTexture;
    private readonly Texture2D _bionicArmHudTexture;
    private readonly Texture2D _ballAndChainHudTexture;
    private readonly Texture2D _fireballHudTexture;
    private readonly List<Projectile2D> _fireballs = [];
    private float _fireballCooldown;
    private Projectile2D? _pendingFireball;
    private PlayerWeapon2D _leftWeapon = PlayerWeapon2D.Sword;
    private PlayerWeapon2D _rightWeapon = PlayerWeapon2D.Fireball;

    public PlayerArsenal2D(
        Scene2D scene,
        PhysicsWorld2D physics,
        PhysicsBody2D ownerBody,
        TextureCache2D textures,
        TraversalMetrics2D traversal,
        IReadOnlyList<WorldObject2D> platforms,
        CombatSystem2D combat,
        PlayerPresentation2D presentation,
        uint playerLayer,
        uint worldLayer,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(scene);
        _physics = ArgGuard.RequireNotNull(physics);
        _ownerBody = ArgGuard.RequireNotNull(ownerBody);
        ArgGuard.ThrowIfNull(textures);
        _traversal = ArgGuard.RequireNotNull(traversal);
        _platforms = ArgGuard.RequireNotNull(platforms);
        _combat = ArgGuard.RequireNotNull(combat);
        _presentation = ArgGuard.RequireNotNull(presentation);
        _sounds = ArgGuard.RequireNotNull(sounds);
        _swordHudTexture = textures.Load(Path.Combine("Hud", "weapon-sword.png"));
        _bionicArmHudTexture = textures.Load(Path.Combine("Hud", "weapon-bionic-arm.png"));
        _ballAndChainHudTexture = textures.Load(Path.Combine("Hud", "weapon-ball-and-chain.png"));
        _fireballHudTexture = textures.Load(Path.Combine("Hud", "weapon-fireball.png"));

        var fireballShader = new TextureShader2D(
            textures.Load("ember-energy.png"),
            new Vector2(96f, 96f),
            SKShaderTileMode.Mirror,
            SKShaderTileMode.Mirror);
        for (var i = 0; i < FireballPoolSize; i++)
        {
            var worldObject = new WorldObject2D(new Circle2D(13f), fireballShader)
            {
                IsVisible = false
            };
            _fireballs.Add(new Projectile2D(worldObject));
            scene.Add(worldObject);
        }

        var swordObject = new WorldObject2D(
            new Capsule2D(Vector2.Zero, new Vector2(68f, 0f), 8f),
            new SolidColorShader(SKColors.Transparent))
        {
            IsVisible = false
        };
        scene.Add(swordObject);
        _sword = new SwordAttack2D(swordObject);
        _grappleArm = new GrappleArm2D(
            scene,
            physics,
            ownerBody,
            traversal.GrappleReach,
            traversal.GrappleRangeGrace);
        _ballAndChain = new BallAndChain2D(
            scene,
            physics,
            ownerBody,
            playerLayer,
            worldLayer);
    }

    public bool IsSwordActive => _sword.IsActive;

    public string WeaponStatus =>
        $"LEFT: {GetWeaponStatus(_leftWeapon)}   RIGHT: {GetWeaponStatus(_rightWeapon)}";

    public string LeftWeaponName => GetWeaponName(_leftWeapon);
    public string RightWeaponName => GetWeaponName(_rightWeapon);

    public Texture2D LeftWeaponHudTexture => GetWeaponHudTexture(_leftWeapon);
    public Texture2D RightWeaponHudTexture => GetWeaponHudTexture(_rightWeapon);

    public IEnumerable<WorldObject2D> GetActiveAttackHitboxes()
    {
        if (_sword.IsActive)
            yield return _sword.WorldObject;
        if (_grappleArm.IsExtending)
            yield return _grappleArm.Head;
        if (_ballAndChain.DealsDamage)
            yield return _ballAndChain.Ball;

        foreach (var fireball in _fireballs)
        {
            if (fireball.IsActive)
                yield return fireball.WorldObject;
        }
    }

    public void BeginFrame(float deltaSeconds) =>
        _fireballCooldown = Math.Max(0f, _fireballCooldown - deltaSeconds);

    public void CycleLeftWeapon() => CycleWeapon(ref _leftWeapon, _rightWeapon);

    public void CycleRightWeapon() => CycleWeapon(ref _rightWeapon, _leftWeapon);

    public float UseLeftWeapon(Vector2? aimTarget, float facing) =>
        UseWeapon(_leftWeapon, aimTarget, facing);

    public float UseRightWeapon(Vector2? aimTarget, float facing) =>
        UseWeapon(_rightWeapon, aimTarget, facing);

    private void CycleWeapon(ref PlayerWeapon2D weapon, PlayerWeapon2D otherWeapon)
    {
        var nextIndex = Array.IndexOf(WeaponOrder, weapon);
        do
        {
            nextIndex = (nextIndex + 1) % WeaponOrder.Length;
            weapon = WeaponOrder[nextIndex];
        }
        while (weapon == otherWeapon);

        _sounds.Play(SoundEffect2D.WeaponSwitch);

        if (!IsAssigned(PlayerWeapon2D.Sword))
            _sword.Cancel();
        if (!IsAssigned(PlayerWeapon2D.BionicArm))
            PlayGrappleRetract();
        if (!IsAssigned(PlayerWeapon2D.BallAndChain))
            _ballAndChain.Cancel();
    }

    private float UseWeapon(PlayerWeapon2D weapon, Vector2? aimTarget, float facing)
    {
        switch (weapon)
        {
            case PlayerWeapon2D.Sword:
                if (_sword.TryStart())
                {
                    _presentation.PlaySwordAttack();
                    _sounds.Play(SoundEffect2D.SwordSwing);
                }
                break;

            case PlayerWeapon2D.BionicArm:
                facing = UseGrappleArm(aimTarget, facing);
                break;

            case PlayerWeapon2D.BallAndChain:
                facing = UseBallAndChain(aimTarget, facing);
                break;

            case PlayerWeapon2D.Fireball:
                facing = FaceAimTarget(aimTarget, facing);
                TryStartFireballShot();
                break;

            default:
                throw ArgGuard.CreateOutOfRange(
                    weapon,
                    "Unknown weapon.");
        }

        return facing;
    }

    private bool IsAssigned(PlayerWeapon2D weapon) =>
        _leftWeapon == weapon || _rightWeapon == weapon;

    private string GetWeaponStatus(PlayerWeapon2D weapon)
    {
        if (weapon == PlayerWeapon2D.BionicArm && _grappleArm.IsLatched)
            return "BIONIC ARM (PULLING - CLICK TO RELEASE)";
        if (weapon == PlayerWeapon2D.BallAndChain && _ballAndChain.IsLanded)
            return "BALL & CHAIN (CLICK TO YANK)";
        if (weapon == PlayerWeapon2D.BallAndChain && _ballAndChain.IsFlying)
            return "BALL & CHAIN (THROWN - CLICK TO YANK)";
        return GetWeaponName(weapon);
    }

    private static string GetWeaponName(PlayerWeapon2D weapon) => weapon switch
    {
        PlayerWeapon2D.Sword => "SWORD",
        PlayerWeapon2D.BionicArm => "BIONIC ARM",
        PlayerWeapon2D.BallAndChain => "BALL & CHAIN",
        PlayerWeapon2D.Fireball => "FIREBALL",
        _ => throw ArgGuard.CreateOutOfRange(weapon, "Unknown weapon.")
    };

    private Texture2D GetWeaponHudTexture(PlayerWeapon2D weapon) => weapon switch
    {
        PlayerWeapon2D.Sword => _swordHudTexture,
        PlayerWeapon2D.BionicArm => _bionicArmHudTexture,
        PlayerWeapon2D.BallAndChain => _ballAndChainHudTexture,
        PlayerWeapon2D.Fireball => _fireballHudTexture,
        _ => throw ArgGuard.CreateOutOfRange(weapon, "Unknown weapon.")
    };

    private float FaceAimTarget(Vector2? aimTarget, float facing)
    {
        if (aimTarget is not { } target)
            return facing;

        var deltaX = target.X - _ownerBody.WorldObject.Transform.Position.X;
        return MathF.Abs(deltaX) > 1f ? MathF.Sign(deltaX) : facing;
    }

    public void TryStartFireballShot()
    {
        if (_fireballCooldown > 0f || _pendingFireball is not null)
            return;

        foreach (var fireball in _fireballs)
        {
            if (fireball.IsActive)
                continue;

            _pendingFireball = fireball;
            _fireballCooldown = _presentation.ShotAnimationDuration;
            _presentation.PlayShot();
            return;
        }
    }

    public void UpdateBeforePhysics(float deltaSeconds)
    {
        _grappleArm.Update(deltaSeconds);
        TryLatchGrappleArm();
        if (_grappleArm.FinishExtensionStep())
            _sounds.Play(SoundEffect2D.GrappleRetract);
        _ballAndChain.UpdateBeforePhysics(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        _sword.Update(deltaSeconds, _ownerBody.WorldObject.Transform.Position, facing);
        ResolveSwordHits(facing);
        _grappleArm.UpdateVisuals();
        ResolveGrappleArmHits(facing);
        var ballWasFlying = _ballAndChain.IsFlying;
        _ballAndChain.UpdateAfterPhysics(_physics);
        if (ballWasFlying && _ballAndChain.IsLanded)
            _sounds.Play(SoundEffect2D.BallLand);
        ResolveBallAndChainHits();
        UpdateFireballs(deltaSeconds);
    }

    public void ReleasePendingFireball(float facing)
    {
        if (_pendingFireball is null)
            return;
        if (!_presentation.IsPlayingShot)
        {
            _pendingFireball = null;
            return;
        }
        if (_presentation.ShotAnimationElapsedSeconds < FireballReleaseTime)
            return;

        _pendingFireball.Launch(
            _ownerBody.WorldObject.Transform.Position + new Vector2(facing * 55f, 8f),
            new Vector2(facing * 920f, 0f),
            lifetime: 2.25f);
        _pendingFireball = null;
        _sounds.Play(SoundEffect2D.FireballLaunch);
    }

    public void Reset()
    {
        _sword.Cancel();
        _grappleArm.Cancel();
        _ballAndChain.Cancel();
        _pendingFireball = null;
        foreach (var fireball in _fireballs)
            fireball.Deactivate();
    }

    private float UseGrappleArm(Vector2? aimTarget, float facing)
    {
        if (_grappleArm.IsLatched)
        {
            if (_grappleArm.Release())
                _sounds.Play(SoundEffect2D.GrappleRelease);
            return facing;
        }

        if (_grappleArm.IsActive)
        {
            PlayGrappleRetract();
            return facing;
        }

        var origin = _ownerBody.WorldObject.Transform.Position;
        var target = aimTarget ??
            origin + Vector2.Normalize(new Vector2(facing, 1.25f)) * _grappleArm.MaxReach;
        if (MathF.Abs(target.X - origin.X) > 1f)
            facing = MathF.Sign(target.X - origin.X);
        if (_grappleArm.TryFire(target))
            _sounds.Play(SoundEffect2D.GrappleFire);
        return facing;
    }

    private float UseBallAndChain(Vector2? aimTarget, float facing)
    {
        if (_ballAndChain.TryYank())
        {
            _sounds.Play(SoundEffect2D.BallYank);
            return facing;
        }
        if (_ballAndChain.IsActive)
            return facing;

        var origin = _ownerBody.WorldObject.Transform.Position;
        var target = aimTarget ?? origin + new Vector2(facing * 300f, 190f);
        if (MathF.Abs(target.X - origin.X) > 1f)
            facing = MathF.Sign(target.X - origin.X);
        if (_ballAndChain.TryThrow(target))
            _sounds.Play(SoundEffect2D.BallThrow);
        return facing;
    }

    private void ResolveSwordHits(float facing)
    {
        if (!_sword.IsActive)
            return;

        if (_combat.ResolveAttack(
            _sword.WorldObject,
            _sword,
            _sword.AttackId,
            damage: 2,
            _ => new Vector2(facing * 520f, 285f)))
        {
            _sounds.Play(SoundEffect2D.SwordHit);
        }
    }

    private void ResolveGrappleArmHits(float facing)
    {
        if (!_grappleArm.IsExtending)
            return;

        var direction = _grappleArm.Head.Transform.Position -
            _ownerBody.WorldObject.Transform.Position;
        direction = direction.LengthSquared() > float.Epsilon
            ? Vector2.Normalize(direction)
            : new Vector2(facing, 0f);
        if (_combat.ResolveAttack(
                _grappleArm.Head,
                _grappleArm,
                _grappleArm.AttackId,
                damage: 2,
                _ => direction * 540f + new Vector2(0f, 210f),
                stopAfterFirstHit: true))
        {
            PlayGrappleRetract();
        }
    }

    private void ResolveBallAndChainHits()
    {
        if (!_ballAndChain.DealsDamage)
            return;

        _combat.ResolveAttack(
            _ballAndChain.Ball,
            _ballAndChain,
            _ballAndChain.AttackId,
            damage: 3,
            _ => _ballAndChain.TravelDirection * 520f + new Vector2(0f, 230f));
    }

    private void TryLatchGrappleArm()
    {
        if (!_grappleArm.IsExtending)
            return;

        var foundHit = false;
        var earliestHit = default(SweptCircleHit2D);
        foreach (var platform in _platforms)
        {
            if (!SweptCircleAabb2D.TryIntersect(
                    _grappleArm.PreviousHeadPosition,
                    _grappleArm.Head.Transform.Position,
                    _grappleArm.HeadRadius + _traversal.GrappleAimAssist,
                    platform.WorldBounds,
                    out var hit) ||
                foundHit && hit.Time >= earliestHit.Time)
            {
                continue;
            }

            foundHit = true;
            earliestHit = hit;
        }

        if (!foundHit)
            return;

        if (earliestHit.Normal.Y <= GrappleLatchMaxNormalY)
        {
            var resolvedHeadPosition =
                earliestHit.SurfacePoint + earliestHit.Normal * _grappleArm.HeadRadius;
            if (_grappleArm.TryLatch(resolvedHeadPosition))
            {
                _sounds.Play(SoundEffect2D.GrappleLatch);
                return;
            }
        }

        if (_grappleArm.IsExtending)
            PlayGrappleRetract();
    }

    private void UpdateFireballs(float deltaSeconds)
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
                foreach (var platform in _platforms)
                {
                    if (!CombatSystem2D.Intersects(fireball.WorldObject, platform))
                        continue;
                    hit = true;
                    break;
                }
            }

            if (hit)
            {
                fireball.Deactivate();
                _sounds.Play(SoundEffect2D.FireballImpact);
            }
        }
    }

    private void PlayGrappleRetract()
    {
        if (_grappleArm.BeginRetract())
            _sounds.Play(SoundEffect2D.GrappleRetract);
    }
}
