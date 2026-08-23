using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Contacts;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class PlayerCharacter2D
{
    private readonly CharacterMotor2D _motor;

    public PlayerCharacter2D(
        Scene2D scene,
        PhysicsWorld2D physics,
        TraversalMetrics2D traversal,
        Vector2 spawnPoint,
        uint playerLayer,
        uint worldLayer)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(traversal);

        WorldObject = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(46f, 70f)),
            new SolidColorShader(SKColors.Transparent))
        {
            IsVisible = false
        };
        WorldObject.Transform.Position = spawnPoint;
        scene.Add(WorldObject);

        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.Restitution = 0f;
        Body.Mass = 1f;
        Body.CollisionLayer = playerLayer;
        Body.CollisionMask = worldLayer;
        _motor = new CharacterMotor2D(physics, Body, traversal);
    }

    public WorldObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; } = new(5);
    public Vector2 Position => WorldObject.Transform.Position;
    public float Facing { get; private set; } = 1f;
    public float InvulnerabilitySeconds { get; private set; }
    public bool IsGrounded => _motor.IsGrounded;

    public void BeginFrame(float deltaSeconds) =>
        InvulnerabilitySeconds = Math.Max(0f, InvulnerabilitySeconds - deltaSeconds);

    public void UpdateBeforePhysics(PlayerIntent2D intent, float deltaSeconds)
    {
        if (MathF.Abs(intent.MoveX) > 0.01f)
            Facing = MathF.Sign(intent.MoveX);
        _motor.UpdateBeforePhysics(intent, deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds) =>
        _motor.UpdateAfterPhysics(deltaSeconds);

    public void Face(float direction)
    {
        if (MathF.Abs(direction) > 0.01f)
            Facing = MathF.Sign(direction);
    }

    public bool ResolveEnemyTouches(IEnumerable<PatrolEnemy2D> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive ||
                !WorldObject.WorldBounds.Intersects(enemy.WorldObject.WorldBounds) ||
                !ShapeCollision2D.TryGetContact(WorldObject, enemy.WorldObject, out var contact))
            {
                continue;
            }

            WorldObject.Transform.Position += contact.MinimumTranslationVector;
            if (InvulnerabilitySeconds > 0f)
                continue;

            Health.Damage(1);
            InvulnerabilitySeconds = 0.9f;
            float knockbackDirection = MathF.Sign(
                Position.X - enemy.WorldObject.Transform.Position.X);
            if (knockbackDirection == 0f)
                knockbackDirection = -Facing;
            Body.LinearVelocity = new Vector2(knockbackDirection * 470f, 410f);
            return !Health.IsAlive;
        }

        return false;
    }

    public void Reset(Vector2 spawnPoint)
    {
        WorldObject.Transform.Position = spawnPoint;
        Body.LinearVelocity = Vector2.Zero;
        Body.AngularVelocity = 0f;
        _motor.Reset();
        InvulnerabilitySeconds = Math.Max(InvulnerabilitySeconds, 0.35f);
    }
}
