using App2d.Core.Validation;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Gameplay.World;

public sealed partial class MovingPlatform2D : IDisposable
{
    private readonly PingPongMotion2D _motion;
    private readonly KinematicEntity2D _kinematic;

    public MovingPlatform2D(
        EntityId2D id,
        PhysicsWorld2D physics,
        Vector2 start,
        Vector2 travel,
        Vector2 size,
        float speed,
        uint collisionLayer,
        uint collisionMask,
        long thingId = 0,
        uint colorArgb = 0xFF25D2BE,
        IProgressMap? easing = null)
    {
        ArgGuard.ThrowIf(!id.IsValid, "A platform requires a valid entity ID.", nameof(id));
        Id = id;
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(size);
        _motion = new PingPongMotion2D(start, travel, speed, easing ?? Easing.Smooth);
        Size = size;
        ThingId = thingId;
        ColorArgb = colorArgb;
        _kinematic = new KinematicEntity2D(id, physics, AxisAlignedRectangle2D.FromSize(size),
            _motion, collisionLayer, collisionMask);
        Body = _kinematic.Body;
        WorldObject = _kinematic.WorldObject;
        Body.Restitution = 0f;
        Body.IsOneWayPlatform = true;
        Body.TransfersContactMotion = true;
    }

    public EntityId2D Id { get; }
    public long ThingId { get; }
    public Vector2 Size { get; }
    public uint ColorArgb { get; }
    public MovingPlatformDefinition2D CaptureDefinition() => new(Id, ThingId, Size, ColorArgb);
    public MovingPlatformState2D CaptureState() => new(Id, WorldObject.Transform.Position);
    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Vector2 Start => _motion.Start;
    public Vector2 End => _motion.End;

    public void Dispose()
    {
        _kinematic.Dispose();
    }

    public void Update(float deltaSeconds) => _kinematic.Update(deltaSeconds);
}
