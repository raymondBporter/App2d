using App2d.Core.Geometry.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Motion;

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
        Ensure.ThrowIf(!id.IsValid, "A kinematic entity requires a valid entity ID.", nameof(id));
        Id = id;
        _physics = Ensure.RequireNotNull(physics);
        Motion = Ensure.RequireNotNull(motion);
        WorldObject = new SpatialObject2D(Ensure.RequireNotNull(shape));
        var start = motion.Position(0d);
        Ensure.Finite(start);
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
    public RigidBody2D Body { get; }
    public double TimeSeconds { get; private set; }
    /// <summary>Instantaneous path velocity; the physics body's velocity is the step-average displacement.</summary>
    public Vector2 MotionVelocity => Motion.Velocity(TimeSeconds);

    /// <returns>The world-space displacement scheduled for the next physics step.</returns>
    public Vector2 Update(float deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Ensure.NonNegative(deltaSeconds);
        if (deltaSeconds == 0f)
        {
            Body.LinearVelocity = Vector2.Zero;
            return Vector2.Zero;
        }

        var nextTime = TimeSeconds + deltaSeconds;
        var target = Motion.Position(nextTime);
        var pathVelocity = Motion.Velocity(nextTime);
        Ensure.Finite(target);
        Ensure.Finite(pathVelocity);
        var displacement = target - WorldObject.Transform.Position;
        Ensure.Finite(displacement);
        Body.LinearVelocity = displacement / deltaSeconds;
        Ensure.Finite(Body.LinearVelocity);
        TimeSeconds = nextTime;
        return displacement;
    }

    internal void RestoreTime(double seconds)
    {
        Ensure.NonNegative(seconds);
        TimeSeconds = seconds;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _physics.RemoveBody(Body);
        GC.SuppressFinalize(this);
    }
}
