using System.Numerics;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Physics;
using App2d.Rendering;
using SkiaSharp;

namespace App2d.Gameplay.World;

public sealed class MovingPlatform2D : IDisposable
{
    private const float MinimumSupportNormalY = 0.55f;

    private readonly PhysicsWorld2D _physics;
    private readonly Scene2D _scene;
    private readonly Vector2 _pathDirection;
    private readonly float _pathLength;
    private readonly float _speed;
    private float _distanceAlongPath;
    private float _travelDirection = 1f;

    public MovingPlatform2D(
        Scene2D scene,
        PhysicsWorld2D physics,
        Vector2 start,
        Vector2 travel,
        Vector2 size,
        float speed,
        uint collisionLayer,
        uint collisionMask,
        SKColor color)
    {
        ArgGuard.ThrowIfNull(scene);
        _scene = scene;
        _physics = ArgGuard.RequireNotNull(physics);
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(travel);
        ArgGuard.ThrowIfNotPositive(size);
        ArgGuard.ThrowIfNotPositive(speed);
        if (travel.LengthSquared() <= float.Epsilon)
            ArgGuard.ThrowOutOfRange(travel, "A moving platform needs a non-zero travel path.");

        Start = start;
        _pathLength = travel.Length();
        _pathDirection = travel / _pathLength;
        _speed = speed;

        WorldObject = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(size),
            new SolidColorShader(color));
        WorldObject.Transform.Position = start;
        scene.Add(WorldObject);

        Body = physics.AddBody(WorldObject, BodyMotionType2D.Kinematic);
        Body.Restitution = 0f;
        Body.IsOneWayPlatform = true;
        Body.CollisionLayer = collisionLayer;
        Body.CollisionMask = collisionMask;
    }

    public WorldObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Vector2 Start { get; }
    public Vector2 End => Start + _pathDirection * _pathLength;

    public void Dispose()
    {
        _physics.RemoveBody(Body);
        _scene.Remove(WorldObject);
    }

    public void Update(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        if (deltaSeconds == 0f)
        {
            Body.LinearVelocity = Vector2.Zero;
            return;
        }

        var nextDistance = Advance(deltaSeconds);
        var target = Start + _pathDirection * nextDistance;
        var displacement = target - WorldObject.Transform.Position;
        var velocity = displacement / deltaSeconds;
        CarrySupportedBodies(displacement, velocity);
        Body.LinearVelocity = velocity;
        _distanceAlongPath = nextDistance;
    }

    private float Advance(float deltaSeconds)
    {
        var distance = _distanceAlongPath;
        var remaining = _speed * deltaSeconds;
        while (remaining > 0f)
        {
            var endpoint = _travelDirection > 0f ? _pathLength : 0f;
            var available = MathF.Abs(endpoint - distance);
            if (remaining <= available)
            {
                distance += _travelDirection * remaining;
                break;
            }

            distance = endpoint;
            remaining -= available;
            _travelDirection = -_travelDirection;
        }

        return distance;
    }

    private void CarrySupportedBodies(Vector2 displacement, Vector2 velocity)
    {
        if (displacement == Vector2.Zero)
            return;

        foreach (var contact in _physics.LastContacts)
        {
            PhysicsBody2D? rider = null;
            var supportNormal = Vector2.Zero;
            if (ReferenceEquals(contact.First, Body))
            {
                rider = contact.Second;
                supportNormal = -contact.Geometry.Normal;
            }
            else if (ReferenceEquals(contact.Second, Body))
            {
                rider = contact.First;
                supportNormal = contact.Geometry.Normal;
            }

            if (rider is not { MotionType: BodyMotionType2D.Dynamic } ||
                rider.IsSensor ||
                supportNormal.Y < MinimumSupportNormalY ||
                rider.IsIgnoringOneWayPlatform(Body))
            {
                continue;
            }

            // Physics resolves motion into the support normal. Carry only the
            // tangential displacement here, then match normal velocity so a
            // platform reversal does not leave its rider behind.
            var normalDisplacement = supportNormal *
                Vector2.Dot(displacement, supportNormal);
            rider.WorldObject.Transform.Position +=
                displacement - normalDisplacement;

            var normalVelocityDelta =
                Vector2.Dot(velocity - rider.LinearVelocity, supportNormal);
            rider.LinearVelocity += supportNormal * normalVelocityDelta;
        }
    }
}
