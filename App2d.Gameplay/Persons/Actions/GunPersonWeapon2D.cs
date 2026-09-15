using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Physics;
using System.Numerics;
using System.Collections.Immutable;

namespace App2d.Gameplay.Persons.Actions;

/// <summary>Hold to charge one shot, fired automatically as soon as charging completes.</summary>
internal sealed partial class GunPersonWeapon2D : PersonWeapon2DBase
{
    private const float ChargeSeconds = 0.6f;
    private const float RecoverySeconds = 0.06f;
    private const float BoltWidth = 30f;
    private readonly PhysicsBody2D _ownerBody;
    private readonly CollisionSystem2D _collision;
    private readonly uint _worldLayer;
    private readonly uint _targetLayer;
    private readonly CombatFaction2D _ownerFaction;
    private readonly CombatSystem2D _combat;
    private readonly Action _shotStarted;
    private readonly Action<WeaponEvent2D> _publish;
    private readonly List<Projectile2D> _bullets = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly SpatialObject2D _barrelPath;
    private readonly Vector2 _muzzleOffset;
    private readonly EntityIdSequence2D _projectileIds = new();
    private float _chargeTime;
    private float _direction = 1f;
    private float _recoverySeconds;
    private float? _secondsSinceShot;
    private bool _canCharge;
    private bool _needsRelease;

    public GunPersonWeapon2D(
        PhysicsBody2D ownerBody, Vector2 muzzleOffset, CollisionSystem2D collision,
        uint worldLayer, uint targetLayer, CombatFaction2D ownerFaction,
        CombatSystem2D combat, Action shotStarted, Action<WeaponEvent2D> publish)
        : base("gun")
    {
        _ownerBody = ArgGuard.RequireNotNull(ownerBody);
        _collision = ArgGuard.RequireNotNull(collision);
        _worldLayer = worldLayer;
        _targetLayer = targetLayer;
        _ownerFaction = ownerFaction;
        _combat = ArgGuard.RequireNotNull(combat);
        _shotStarted = ArgGuard.RequireNotNull(shotStarted);
        _publish = ArgGuard.RequireNotNull(publish);
        ArgGuard.ThrowIfNotFinite(muzzleOffset);
        _muzzleOffset = muzzleOffset;
        _barrelPath = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(
            new Vector2(_muzzleOffset.X + BoltWidth, 4f)));
        for (var index = 0; index < 16; index++)
            _bullets.Add(new Projectile2D(new SpatialObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(BoltWidth, 10f)))));
    }

    public WeaponState2D CaptureState() => new(IsCharging, ChargeProgress, MuzzlePosition,
        _bullets.Where(b => b.IsActive).Select(b => new ProjectileState2D(
            b.Id, b.WorldObject.Transform.Position, b.Velocity, b.Origin)).ToImmutableArray());

    public override PersonActionState2D CaptureActionState() => _secondsSinceShot is { } elapsed
        ? new(Simulation.PlayerAttackKind2D.Shot, elapsed, RecoverySeconds) : default;

    public bool IsCharging { get; private set; }
    public float ChargeProgress => Math.Clamp(_chargeTime / ChargeSeconds, 0f, 1f);
    public override IEnumerable<SpatialObject2D> ActiveHitboxes
    {
        get
        {
            foreach (var bullet in _bullets)
                if (bullet.IsActive)
                    yield return bullet.WorldObject;
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

    public override float Use(float facing)
    {
        if (_needsRelease)
            return facing;
        _needsRelease = true;
        if (!_canCharge || _recoverySeconds > 0f)
            return facing;
        _direction = facing;
        _chargeTime = 0f;
        IsCharging = true;
        _secondsSinceShot = null;
        _publish(new ChargeStarted2D(MuzzlePosition));
        return facing;
    }

    public override void BeginFrame(float deltaSeconds)
    {
        if (_secondsSinceShot.HasValue) _secondsSinceShot += deltaSeconds;
        _recoverySeconds = Math.Max(0f, _recoverySeconds - deltaSeconds);
    }

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        if (IsCharging) _direction = facing;
        foreach (var projectile in _bullets)
        {
            projectile.Update(deltaSeconds);
            if (projectile.IsActive) ResolveHit(projectile);
        }
        if (IsCharging)
        {
            _chargeTime += deltaSeconds;
            if (_chargeTime + 0.000001f >= ChargeSeconds) Fire();
        }
    }

    private Vector2 MuzzlePosition => _ownerBody.WorldObject.Transform.Position +
        new Vector2(_direction * _muzzleOffset.X, _muzzleOffset.Y);

    private void Fire()
    {
        IsCharging = false;
        _chargeTime = 0f;
        var muzzle = MuzzlePosition;
        _recoverySeconds = RecoverySeconds;
        _secondsSinceShot = 0f;
        _shotStarted();
        _publish(new GunFired2D(muzzle));
        // Never spawn a projectile beyond a thin wall intersecting the barrel.
        _barrelPath.Transform.Position = _ownerBody.WorldObject.Transform.Position +
            new Vector2(_direction * (_muzzleOffset.X + BoltWidth) * 0.5f, _muzzleOffset.Y);
        if (_collision.Overlap(_barrelPath, _overlaps, _worldLayer, includeSensors: false) > 0)
        {
            _publish(new ProjectileImpact2D(muzzle, EntityId2D.None));
            return;
        }
        foreach (var projectile in _bullets)
        {
            // Availability is simulation state; a fading client trail cannot delay a shot.
            if (projectile.IsActive) continue;
            projectile.Launch(muzzle + new Vector2(_direction * BoltWidth * 0.5f, 0f),
                new Vector2(_direction * 1250f, 0f), lifetime: 1.5f, origin: muzzle, id: _projectileIds.Next());
            ResolveHit(projectile);
            break;
        }
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
            _publish(new ProjectileImpact2D(projectile.WorldObject.Transform.Position, projectile.Id));
        }
    }

    public void CancelCharge()
    {
        _canCharge = false;
        if (!IsCharging)
            return;
        var progress = ChargeProgress;
        IsCharging = false;
        _chargeTime = 0f;
        _publish(new ChargeCancelled2D(MuzzlePosition, progress));
    }

    public override void OnDeselected()
    {
        CancelCharge();
        _recoverySeconds = 0f;
        _secondsSinceShot = null;
    }

    public override void Reset()
    {
        OnDeselected();
        _needsRelease = true;
        foreach (var projectile in _bullets) projectile.Deactivate();
    }
}
