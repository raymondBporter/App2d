using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public enum UnarmedAttackKind2D
{
    Punch,
    Kick
}

/// <summary>Weapon-free punch and kick actions shared by human and AI persons.</summary>
public sealed class UnarmedPersonActions2D : IPersonActionSet2D
{
    private readonly CombatSystem2D _combat;
    private readonly PhysicsBody2D _ownerBody;
    private readonly CombatFaction2D _ownerFaction;
    private readonly uint _targetLayer;
    private readonly Attack _punch;
    private readonly Attack _kick;

    public UnarmedPersonActions2D(
        PhysicsBody2D ownerBody,
        CombatFaction2D ownerFaction,
        uint targetLayer,
        CombatSystem2D combat)
    {
        _ownerBody = ArgGuard.RequireNotNull(ownerBody);
        _combat = ArgGuard.RequireNotNull(combat);
        _ownerFaction = ownerFaction;
        _targetLayer = targetLayer;
        _punch = new Attack(
            UnarmedAttackKind2D.Punch,
            AxisAlignedRectangle2D.FromSize(new Vector2(48f, 48f)),
            new MeleeAttackProfile2D(
                durationSeconds: 0.28f,
                damageStartSeconds: 0.07f,
                damageEndSeconds: 0.17f,
                inputBufferSeconds: 0.09f,
                forwardOffset: 39f,
                verticalOffset: 7f),
            damage: 1,
            knockback: new Vector2(310f, 145f));
        _kick = new Attack(
            UnarmedAttackKind2D.Kick,
            AxisAlignedRectangle2D.FromSize(new Vector2(66f, 42f)),
            new MeleeAttackProfile2D(
                durationSeconds: 0.42f,
                damageStartSeconds: 0.14f,
                damageEndSeconds: 0.29f,
                inputBufferSeconds: 0.11f,
                forwardOffset: 48f,
                verticalOffset: -9f),
            damage: 2,
            knockback: new Vector2(470f, 205f));
    }

    public event Action<UnarmedAttackKind2D, float>? AttackStarted;

    public bool IsAttackActive => _punch.Action.IsInProgress || _kick.Action.IsInProgress;

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        if (_punch.Action.IsVisible)
            yield return _punch.Action.WorldObject;
        if (_kick.Action.IsVisible)
            yield return _kick.Action.WorldObject;
    }

    public void BeginFrame(float deltaSeconds)
    {
    }

    public void UpdateBeforePhysics(float deltaSeconds)
    {
    }

    public void UpdateAfterPhysics(float deltaSeconds, float facing)
    {
        UpdateAttack(_punch, deltaSeconds, facing);
        UpdateAttack(_kick, deltaSeconds, facing);
    }

    public float UsePrimary(Vector2? aimTarget, float facing) =>
        Use(_punch, facing);

    public float UseSecondary(Vector2? aimTarget, float facing) =>
        Use(_kick, facing);

    public void SelectNext()
    {
    }

    public void Reset()
    {
        _punch.Action.Cancel();
        _kick.Action.Cancel();
    }

    private float Use(Attack requested, float facing)
    {
        var other = ReferenceEquals(requested, _punch) ? _kick : _punch;
        if (other.Action.IsInProgress)
            return facing;

        requested.Direction = MathF.Sign(facing);
        if (requested.Action.TryStart())
            AttackStarted?.Invoke(requested.Kind, requested.Action.DurationSeconds);
        return facing;
    }

    private void UpdateAttack(Attack attack, float deltaSeconds, float facing)
    {
        if (attack.Action.Update(
            deltaSeconds,
            _ownerBody.WorldObject.Transform.Position,
            attack.Direction))
        {
            attack.Direction = MathF.Sign(facing);
            attack.Action.Update(
                0f,
                _ownerBody.WorldObject.Transform.Position,
                attack.Direction);
            AttackStarted?.Invoke(attack.Kind, attack.Action.DurationSeconds);
        }

        if (attack.Action.IsDamageActive)
        {
            _combat.ResolveAttack(
                attack.Action.WorldObject,
                attack.Action,
                attack.Action.AttackId,
                _ownerFaction,
                _targetLayer,
                attack.Damage,
                _ => new Vector2(
                    attack.Direction * attack.Knockback.X,
                    attack.Knockback.Y));
        }
    }

    private sealed class Attack(
        UnarmedAttackKind2D kind,
        IShape2D hitboxShape,
        MeleeAttackProfile2D profile,
        int damage,
        Vector2 knockback)
    {
        public UnarmedAttackKind2D Kind { get; } = kind;
        public MeleeAttack2D Action { get; } =
            new(new SpatialObject2D(hitboxShape), profile);
        public int Damage { get; } = damage;
        public Vector2 Knockback { get; } = knockback;
        public float Direction { get; set; } = 1f;
    }
}
