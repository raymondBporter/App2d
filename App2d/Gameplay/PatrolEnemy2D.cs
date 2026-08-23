using System.Numerics;
using App2d.Engine;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;

namespace App2d.Gameplay;

public sealed class PatrolEnemy2D
{
    private readonly Dictionary<object, int> _lastAttackIds =
        new(ReferenceEqualityComparer.Instance);
    private readonly IShader2D _normalShader;
    private readonly IShader2D _hitShader;
    private float _direction = 1f;
    private float _hitFlashSeconds;
    private float _stunSeconds;

    public PatrolEnemy2D(
        WorldObject2D worldObject,
        PhysicsBody2D body,
        float patrolMinX,
        float patrolMaxX,
        float speed,
        int health,
        IShader2D normalShader,
        IShader2D hitShader)
    {
        if (patrolMinX >= patrolMaxX)
            throw new ArgumentException("Patrol minimum must be below maximum.");
        if (!float.IsFinite(speed) || speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed));

        WorldObject = worldObject;
        Body = body;
        PatrolMinX = patrolMinX;
        PatrolMaxX = patrolMaxX;
        Speed = speed;
        Health = new Health2D(health);
        _normalShader = normalShader;
        _hitShader = hitShader;
    }

    public WorldObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public float PatrolMinX { get; }
    public float PatrolMaxX { get; }
    public float Speed { get; }
    public bool IsAlive => Health.IsAlive;

    public bool TryRegisterHit(object attackSource, int attackId)
    {
        ArgumentNullException.ThrowIfNull(attackSource);
        if (_lastAttackIds.TryGetValue(attackSource, out var lastAttackId) &&
            lastAttackId == attackId)
        {
            return false;
        }

        _lastAttackIds[attackSource] = attackId;
        return true;
    }

    public void Update(float deltaSeconds)
    {
        if (!IsAlive)
            return;

        _hitFlashSeconds = Math.Max(0f, _hitFlashSeconds - deltaSeconds);
        _stunSeconds = Math.Max(0f, _stunSeconds - deltaSeconds);
        WorldObject.Shader = _hitFlashSeconds > 0f ? _hitShader : _normalShader;

        var x = WorldObject.Transform.Position.X;
        if (x <= PatrolMinX)
            _direction = 1f;
        else if (x >= PatrolMaxX)
            _direction = -1f;

        if (_stunSeconds <= 0f)
        {
            Body.LinearVelocity = new Vector2(
                _direction * Speed,
                Body.LinearVelocity.Y);
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
            WorldObject.IsVisible = false;
            Body.IsCollider = false;
            Body.MotionType = BodyMotionType2D.Static;
            Body.LinearVelocity = Vector2.Zero;
            return true;
        }

        Body.LinearVelocity = knockback;
        _hitFlashSeconds = 0.11f;
        _stunSeconds = 0.18f;
        WorldObject.Shader = _hitShader;
        return true;
    }
}
