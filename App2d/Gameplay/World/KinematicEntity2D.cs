using App2d.Core;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>
/// A shaped kinematic body driven by a reusable world-space motion. Call Update before
/// the physics step. The body uses the displacement over that step as its velocity so
/// it reaches Position(t + dt), including when a path reverses inside the step.
/// </summary>
public sealed class KinematicEntity2D : IDisposable
{
    private readonly PhysicsWorld2D _physics;
    private bool _disposed;

    public KinematicEntity2D(EntityId2D id, PhysicsWorld2D physics, IShape2D shape,
        IKinematicMotion2D motion, uint collisionLayer, uint collisionMask)
    {
        ArgGuard.ThrowIf(!id.IsValid, "A kinematic entity requires a valid entity ID.", nameof(id));
        Id = id;
        _physics = ArgGuard.RequireNotNull(physics);
        Motion = ArgGuard.RequireNotNull(motion);
        WorldObject = new SpatialObject2D(ArgGuard.RequireNotNull(shape));
        var start = motion.Position(0d);
        ArgGuard.ThrowIfNotFinite(start);
        WorldObject.Transform.Position = start;
        Body = physics.AddBody(WorldObject, BodyMotionType2D.Kinematic);
        Body.EntityId = id;
        Body.Restitution = 0f;
        Body.CollisionLayer = collisionLayer;
        Body.CollisionMask = collisionMask;
    }

    public EntityId2D Id { get; }
    public IKinematicMotion2D Motion { get; }
    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public double TimeSeconds { get; private set; }
    /// <summary>Instantaneous path velocity; the physics body's velocity is the step-average displacement.</summary>
    public Vector2 MotionVelocity => Motion.Velocity(TimeSeconds);

    /// <returns>The world-space displacement scheduled for the next physics step.</returns>
    public Vector2 Update(float deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
        if (deltaSeconds == 0f)
        {
            Body.LinearVelocity = Vector2.Zero;
            return Vector2.Zero;
        }

        var nextTime = TimeSeconds + deltaSeconds;
        var target = Motion.Position(nextTime);
        var pathVelocity = Motion.Velocity(nextTime);
        ArgGuard.ThrowIfNotFinite(target);
        ArgGuard.ThrowIfNotFinite(pathVelocity);
        var displacement = target - WorldObject.Transform.Position;
        ArgGuard.ThrowIfNotFinite(displacement);
        Body.LinearVelocity = displacement / deltaSeconds;
        ArgGuard.ThrowIfNotFinite(Body.LinearVelocity);
        TimeSeconds = nextTime;
        return displacement;
    }

    internal void RestoreTime(double seconds)
    {
        ArgGuard.ThrowIf(!double.IsFinite(seconds) || seconds < 0d,
            "Time must be finite and non-negative.", nameof(seconds));
        TimeSeconds = seconds;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _physics.RemoveBody(Body);
        GC.SuppressFinalize(this);
    }
}
