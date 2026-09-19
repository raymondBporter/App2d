using App2d.Core.Geometry;
using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal sealed partial class SwordPersonWeapon2D(
    EntityIdAllocator2D ids,
    PhysicsBody2D ownerBody,
    CombatFaction2D ownerFaction,
    uint targetLayer,
    CombatSystem2D combat,
    Action<float> attackStarted,
    Action<WeaponEvent2D> publish,
    Action<float> downAttackStarted,
    Func<Bounds2D, bool>? overlapsSpikes = null) : MeleePersonWeapon2D(
        EquipmentKind2D.Sword,
        ids.Allocate(),
        ownerBody,
        AxisAlignedRectangle2D.FromSize(new Vector2(56f, 72f)),
        new MeleeAttackProfile2D(
            durationSeconds: 0.35f,
            damageStartSeconds: 0.10f,
            damageEndSeconds: 0.27f,
            inputBufferSeconds: 0.10f,
            forwardOffset: 52f),
        damage: 2,
        knockback: new Vector2(520f, 285f),
        ownerFaction,
        targetLayer,
        combat,
        attackStarted,
        publish)
{
    private readonly DownwardSwing _downAttack = new(
        ids.Allocate(), ownerBody, ownerFaction, targetLayer, combat, downAttackStarted, publish, overlapsSpikes);

    public bool ConsumeBounce() => _downAttack.ConsumeBounce();
    public override PersonActionState2D CaptureActionState() => _downAttack.IsAttackActive
        ? _downAttack.CaptureActionState() with { Kind = Simulation.PlayerAttackKind2D.Downward }
        : base.CaptureActionState();

    public override bool IsAttackActive => base.IsAttackActive || _downAttack.IsAttackActive;

    public override IEnumerable<SpatialObject2D> ActiveHitboxes =>
        base.ActiveHitboxes.Concat(_downAttack.ActiveHitboxes);

    public override float Use(float facing) =>
        _downAttack.IsAttackActive ? facing : base.Use(facing);

    public float UseDownward(float facing) =>
        base.IsAttackActive ? facing : _downAttack.Use(facing);

    public override void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        base.UpdateAfterPhysics(deltaSeconds, facing);
        _downAttack.UpdateAfterPhysics(deltaSeconds, facing);
    }

    public override void OnDeselected() => Reset();

    public override void Reset()
    {
        base.Reset();
        _downAttack.Reset();
    }

    private sealed class DownwardSwing(
        EntityId2D sourceId,
        PhysicsBody2D body,
        CombatFaction2D faction,
        uint layer,
        CombatSystem2D combatSystem,
        Action<float> started,
        Action<WeaponEvent2D> emit,
        Func<Bounds2D, bool>? spikeOverlap) : MeleePersonWeapon2D(
            EquipmentKind2D.Sword, sourceId, body,
            AxisAlignedRectangle2D.FromSize(new Vector2(64f, 48f)),
            new MeleeAttackProfile2D(
                durationSeconds: 0.25f,
                // Strike immediately; the slash flash spans the first two poses at 24 fps.
                damageStartSeconds: 0f,
                damageEndSeconds: 2f / 24f,
                // A new down attack must recheck airborne eligibility through Person2D.
                inputBufferSeconds: 0f,
                forwardOffset: 0f,
                verticalOffset: -32f),
            damage: 2,
            knockback: new Vector2(0f, -285f),
            faction, layer, combatSystem, started, emit)
    {
        internal (bool HasBounced, bool BouncePending) CaptureBounce() => (_hasBounced, _bouncePending);
        internal void RestoreBounce(bool hasBounced, bool bouncePending)
        {
            _hasBounced = hasBounced;
            _bouncePending = bouncePending;
        }

        private bool _hasBounced;
        private bool _bouncePending;

        public override float Use(float facing)
        {
            if (!IsAttackActive)
            {
                _hasBounced = false;
                _bouncePending = false;
            }
            return base.Use(facing);
        }

        public override void UpdateAfterPhysics(float deltaSeconds, float facing)
        {
            base.UpdateAfterPhysics(deltaSeconds, facing);
            if (!_hasBounced && spikeOverlap is not null &&
                ActiveHitboxes.Any(hitbox => spikeOverlap(hitbox.WorldBounds)))
            {
                ReportImpact(OwnerPosition);
                OnHit();
            }
        }

        protected override void OnHit()
        {
            if (_hasBounced)
                return;

            _hasBounced = true;
            _bouncePending = true;
        }

        public bool ConsumeBounce()
        {
            var pending = _bouncePending;
            _bouncePending = false;
            return pending;
        }

        public override void Reset()
        {
            base.Reset();
            _hasBounced = false;
            _bouncePending = false;
        }
    }
}
