using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal abstract partial class MeleePersonWeapon2D(
    EquipmentKind2D kind,
    EntityId2D attackSourceId,
    PhysicsBody2D ownerBody,
    IShape2D hitboxShape,
    MeleeAttackProfile2D attackProfile,
    int damage,
    Vector2 knockback,
    CombatFaction2D ownerFaction,
    uint targetLayer,
    CombatSystem2D combat,
    Action<float> attackStarted,
    Action<WeaponEvent2D> publish, AuthoredHero2D? authored = null) : PersonWeapon2DBase(kind)
{
    private readonly PhysicsBody2D _ownerBody = ArgGuard.RequireNotNull(ownerBody);
    private readonly CombatSystem2D _combat = ArgGuard.RequireNotNull(combat);
    private readonly Action<float> _attackStarted = ArgGuard.RequireNotNull(attackStarted);
    private readonly Action<WeaponEvent2D> _publish = ArgGuard.RequireNotNull(publish);
    private readonly MeleeAttack2D _attack = new(attackSourceId, new SpatialObject2D(hitboxShape), attackProfile);
    private float _attackDirection = 1f;
    private float _bufferedAttackDirection = 1f;
    // With an authored hero, which of its swings is playing and which one starts next: a press chains to the next swing of
    // the combo while one runs or its recovery still holds the blade out (see AuthoredHero2D.Chain).
    private float _sinceSwing = float.PositiveInfinity;
    private string? _swing, _nextSwing;

    public virtual bool IsAttackActive => _attack.IsInProgress;
    public override PersonActionState2D CaptureActionState() => _attack.IsInProgress
        ? new(Simulation.PlayerAttackKind2D.Melee, _attack.ElapsedSeconds, _attack.DurationSeconds, _swing) : default;

    public override IEnumerable<SpatialObject2D> ActiveHitboxes
    {
        get
        {
            if (_attack.IsVisible)
                yield return _attack.WorldObject;
        }
    }

    public override float Use(float facing)
    {
        var direction = MathF.Sign(facing);
        if (_attack.IsInProgress)
            _bufferedAttackDirection = direction;
        if (authored is not null)
        {
            _nextSwing = authored.Chain(_swing, _attack.IsInProgress ? 0 : _sinceSwing);
            _attack.NextProfile = authored.Profile(_nextSwing);
        }
        if (_attack.TryStart())
        {
            _attackDirection = direction;
            NotifyAttackStarted();
        }
        return facing;
    }

    public override void OnDeselected() => _attack.Cancel();
    public override void Reset() => _attack.Cancel();

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        _sinceSwing = _attack.IsInProgress ? 0 : _sinceSwing + deltaSeconds;
        if (_attack.Update(
            deltaSeconds,
            _ownerBody.WorldObject.Transform.Position,
            _attackDirection))
        {
            _attackDirection = _bufferedAttackDirection;
            _attack.Update(
                0f,
                _ownerBody.WorldObject.Transform.Position,
                _attackDirection);
            NotifyAttackStarted();
        }

        if (_attack.IsDamageActive &&
            _combat.ResolveAttack(
                _attack.WorldObject,
                _attack.SourceId,
                _attack.AttackId,
                ownerFaction,
                targetLayer,
                damage,
                _ => new Vector2(_attackDirection * knockback.X, knockback.Y)))
        {
            ReportImpact(_attack.WorldObject.Transform.Position);
            OnHit();
        }
    }

    protected Vector2 OwnerPosition => _ownerBody.WorldObject.Transform.Position;
    protected void ReportImpact(Vector2 position) => _publish(new SwordImpact2D(position));
    protected virtual void OnHit() { }

    private void NotifyAttackStarted() { _swing = _nextSwing; _attackStarted(_attack.DurationSeconds); }
}
