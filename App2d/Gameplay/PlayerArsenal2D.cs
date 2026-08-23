using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Intersections;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class PlayerArsenal2D
{
    private static readonly PlayerWeapon2D[] WeaponOrder =
    [
        PlayerWeapon2D.Sword,
        PlayerWeapon2D.BionicArm,
        PlayerWeapon2D.BallAndChain
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
    private readonly SwordAttack2D _sword;
    private readonly GrappleArm2D _grappleArm;
    private readonly BallAndChain2D _ballAndChain;
    private readonly List<Projectile2D> _fireballs = [];
    private float _fireballCooldown;
    private Projectile2D? _pendingFireball;
    private PlayerWeapon2D _activeWeapon = PlayerWeapon2D.Sword;

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
        uint worldLayer)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        _ownerBody = ownerBody ?? throw new ArgumentNullException(nameof(ownerBody));
        ArgumentNullException.ThrowIfNull(textures);
        _traversal = traversal ?? throw new ArgumentNullException(nameof(traversal));
        _platforms = platforms ?? throw new ArgumentNullException(nameof(platforms));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

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

    public string ActiveWeaponStatus
    {
        get
        {
            if (_grappleArm.IsLatched)
                return "BIONIC ARM - SWINGING - CLICK TO RELEASE";
            if (_ballAndChain.IsLanded)
                return "BALL & CHAIN - CLICK TO YANK";
            if (_ballAndChain.IsFlying)
                return "BALL & CHAIN - THROWN (CLICK TO YANK)";
            return ActiveWeaponName;
        }
    }

    public string ActiveWeaponName => _activeWeapon switch
    {
        PlayerWeapon2D.Sword => "SWORD",
        PlayerWeapon2D.BionicArm => "BIONIC ARM",
        PlayerWeapon2D.BallAndChain => "BALL & CHAIN",
        _ => throw new ArgumentOutOfRangeException(nameof(_activeWeapon))
    };

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

    public void CycleWeapon(int direction)
    {
        if (direction == 0)
            return;

        var currentIndex = Array.IndexOf(WeaponOrder, _activeWeapon);
        var nextIndex =
            (currentIndex + Math.Sign(direction) + WeaponOrder.Length) % WeaponOrder.Length;
        _activeWeapon = WeaponOrder[nextIndex];

        _sword.Cancel();
        if (_activeWeapon != PlayerWeapon2D.BionicArm)
            _grappleArm.BeginRetract();
        if (_activeWeapon != PlayerWeapon2D.BallAndChain)
            _ballAndChain.Cancel();
    }

    public float UseActiveWeapon(Vector2? aimTarget, float facing)
    {
        switch (_activeWeapon)
        {
            case PlayerWeapon2D.Sword:
                if (_sword.TryStart())
                    _presentation.PlaySwordAttack();
                break;

            case PlayerWeapon2D.BionicArm:
                facing = UseGrappleArm(aimTarget, facing);
                break;

            case PlayerWeapon2D.BallAndChain:
                facing = UseBallAndChain(aimTarget, facing);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(_activeWeapon));
        }

        return facing;
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
        _grappleArm.FinishExtensionStep();
        _ballAndChain.UpdateBeforePhysics(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        _sword.Update(deltaSeconds, _ownerBody.WorldObject.Transform.Position, facing);
        ResolveSwordHits(facing);
        _grappleArm.UpdateVisuals();
        ResolveGrappleArmHits(facing);
        _ballAndChain.UpdateAfterPhysics(_physics);
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
            _grappleArm.Release();
            return facing;
        }

        if (_grappleArm.IsActive)
        {
            _grappleArm.BeginRetract();
            return facing;
        }

        var origin = _ownerBody.WorldObject.Transform.Position;
        var target = aimTarget ??
            origin + Vector2.Normalize(new Vector2(facing, 1.25f)) * _grappleArm.MaxReach;
        if (MathF.Abs(target.X - origin.X) > 1f)
            facing = MathF.Sign(target.X - origin.X);
        _grappleArm.TryFire(target);
        return facing;
    }

    private float UseBallAndChain(Vector2? aimTarget, float facing)
    {
        if (_ballAndChain.TryYank() || _ballAndChain.IsActive)
            return facing;

        var origin = _ownerBody.WorldObject.Transform.Position;
        var target = aimTarget ?? origin + new Vector2(facing * 300f, 190f);
        if (MathF.Abs(target.X - origin.X) > 1f)
            facing = MathF.Sign(target.X - origin.X);
        _ballAndChain.TryThrow(target);
        return facing;
    }

    private void ResolveSwordHits(float facing)
    {
        if (!_sword.IsActive)
            return;

        _combat.ResolveAttack(
            _sword.WorldObject,
            _sword,
            _sword.AttackId,
            damage: 2,
            _ => new Vector2(facing * 520f, 285f));
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
            _grappleArm.BeginRetract();
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
                return;
        }

        if (_grappleArm.IsExtending)
            _grappleArm.BeginRetract();
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
                fireball.Deactivate();
        }
    }
}
