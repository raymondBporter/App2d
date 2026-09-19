using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed partial class PatrolEnemy2D : ICombatant2D, IContactDamageSource2D
{
    private readonly Dictionary<EntityId2D, int> _lastAttackIds = [];
    private float _direction = 1f;
    private float _stunSeconds;

    public PatrolEnemy2D(EntityId2D id,
        SpatialObject2D worldObject, PhysicsBody2D body, float patrolMinX, float patrolMaxX, float speed, int health)
    {
        if (!id.IsValid)
            throw new ArgumentException("An enemy requires a valid entity ID.", nameof(id));
        Id = id;
        ArgGuard.ThrowIfGreaterThanOrEqual(patrolMinX, patrolMaxX);
        ArgGuard.ThrowIfNotPositive(speed);

        WorldObject = worldObject;
        Body = body;
        Body.EntityId = Id;
        PatrolMinX = patrolMinX;
        PatrolMaxX = patrolMaxX;
        Speed = speed;
        Health = new Health2D(health);
    }

    public EntityId2D Id { get; }
    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction => CombatFaction2D.Enemy;
    public int ContactDamage => 1;
    public float PatrolMinX { get; }
    public float PatrolMaxX { get; }
    public float Speed { get; }
    public float Facing => _direction;
    public bool IsStunned => _stunSeconds > 0f;
    public bool IsAlive => Health.IsAlive;

    public void SetSimulationEnabled(bool isEnabled)
    {
        if (!IsAlive)
            return;

        Body.IsCollider = isEnabled;
        Body.MotionType = isEnabled
            ? BodyMotionType2D.Dynamic
            : BodyMotionType2D.Static;
        if (!isEnabled)
            Body.LinearVelocity = Vector2.Zero;
    }

    public bool TryRegisterHit(EntityId2D attackSourceId, int attackId)
    {
        if (!attackSourceId.IsValid)
            throw new ArgumentException("An attack source ID is required.", nameof(attackSourceId));
        if (_lastAttackIds.TryGetValue(attackSourceId, out var lastAttackId) &&
            lastAttackId == attackId)
        {
            return false;
        }

        _lastAttackIds[attackSourceId] = attackId;
        return true;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsAlive)
            return;

        _stunSeconds = Math.Max(0f, _stunSeconds - deltaSeconds);

        var x = WorldObject.Transform.Position.X;
        if (x <= PatrolMinX)
            _direction = 1f;
        else if (x >= PatrolMaxX)
            _direction = -1f;

        if (_stunSeconds <= 0f)
        {
            Body.LinearVelocity = new Vector2(_direction * Speed, Body.LinearVelocity.Y);
        }

        Body.AngularVelocity = 0f;
        WorldObject.Transform.Rotation = 0f;
    }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        if (!Health.Damage(damage))
            return false;

        if (!IsAlive)
        {
            Body.IsCollider = false;
            Body.MotionType = BodyMotionType2D.Static;
            Body.LinearVelocity = Vector2.Zero;
            return true;
        }

        Body.LinearVelocity = knockback;
        _stunSeconds = 0.18f;
        return true;
    }
}
