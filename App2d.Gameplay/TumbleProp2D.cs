using App2d.Core;
using App2d.Core.Geometry;
using App2d.Physics;
using App2d.Rendering;
using SkiaSharp;
using System.Numerics;

namespace App2d.Gameplay;

/// <summary>
/// An indestructible composite dumbbell that tumbles when hit. The visual
/// object is also the physics collider, so nothing needs syncing.
/// </summary>
internal sealed class TumbleProp2D : IEnemyActor2D, IEnemyCombatant2D
{
    private const float HitVelocityScale = 0.8f;
    private const float HitAngularKick = 4.5f;

    private readonly Dictionary<object, int> _lastAttackIds = new(ReferenceEqualityComparer.Instance);

    public TumbleProp2D(Scene2D scene, PhysicsWorld2D physics, Vector2 position, uint worldLayer, uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNotFinite(position);

        var shape = new CompositeShape2D([
            Rectangle2D.FromSize(new Vector2(72f, 18f)),
            new Circle2D(16f, new Vector2(-42f, 0f)),
            new Circle2D(16f, new Vector2(42f, 0f))]);
        var visual = new WorldObject2D(shape, new SolidColorShader(new SKColor(0xFF, 0x9A, 0x3B))) { ZIndex = 1 };
        visual.Transform.Position = position;
        scene.Add(visual);
        WorldObject = visual;

        Body = physics.AddBody(visual, BodyMotionType2D.Dynamic);
        Body.UserData = this;
        Body.Mass = 2f;
        Body.MomentOfInertia = 1_800f;
        Body.FreezeRotation = false;
        Body.Friction = 0.4f;
        Body.Restitution = 0.15f;
        Body.CollisionLayer = enemyLayer;
        Body.CollisionMask = worldLayer;
        Health = new Health2D(1_000_000);
    }

    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public bool IsAlive => true;
    public IEnemyCombatant2D Combatant => this;

    public void SetSimulationEnabled(bool isEnabled)
    {
        Body.IsCollider = isEnabled;
        Body.MotionType = isEnabled ? BodyMotionType2D.Dynamic : BodyMotionType2D.Static;
        if (!isEnabled)
        {
            Body.LinearVelocity = Vector2.Zero;
            Body.AngularVelocity = 0f;
        }
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
    }

    public void SyncAfterPhysics()
    {
    }

    public bool TryRegisterHit(object attackSource, int attackId)
    {
        ArgGuard.ThrowIfNull(attackSource);
        if (_lastAttackIds.TryGetValue(attackSource, out var lastAttackId) && lastAttackId == attackId)
            return false;

        _lastAttackIds[attackSource] = attackId;
        return true;
    }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        Body.LinearVelocity += knockback * HitVelocityScale;
        Body.AngularVelocity += -MathF.Sign(knockback.X) * HitAngularKick;
        return true;
    }
}
