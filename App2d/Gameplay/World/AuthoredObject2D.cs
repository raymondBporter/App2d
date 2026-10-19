using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Shared runtime binding for authored shapes, optional bodies and path motion.</summary>
public sealed class AuthoredObject2D : IDisposable
{
    private readonly PhysicsWorld2D _physics;
    private readonly KinematicEntity2D? _kinematic;
    private readonly CurveMotion2D? _visualMotion;
    private double _time;
    private bool _disposed;

    public AuthoredObject2D(EntityId2D id, ObjectPlacement2D placement, PhysicsWorld2D physics, uint layer, uint mask)
    {
        ArgGuard.ThrowIf(!id.IsValid, "An object needs a valid entity id.", nameof(id));
        ArgGuard.ThrowIfNull(placement);
        placement.Validate();
        _physics = ArgGuard.RequireNotNull(physics);
        Id = id;
        Placement = placement;
        var asset = placement.Asset;
        var motion = asset.Motion?.CreateMotion(placement.Position);
        if (asset.Physics is not null && motion is not null)
        {
            _kinematic = new(id, physics, asset.Geometry, motion, layer, mask);
            WorldObject = _kinematic.WorldObject;
            Body = _kinematic.Body;
            asset.Physics.ApplyTo(Body);
        }
        else
        {
            _visualMotion = motion;
            WorldObject = new(asset.Geometry);
            WorldObject.Transform.Position = motion?.Position(0) ?? placement.Position;
            Body = asset.Physics?.CreateBody(physics, WorldObject);
        }
        if (Body is not null)
        {
            Body.EntityId = id;
            Body.CollisionLayer = layer;
            Body.CollisionMask = mask;
            if (asset.Surface is { } surface)
            {
                var settings = PlatformCollisionRules2D.Attach(physics).Surface(Body);
                settings.OneWaySurfaceNormal = surface.OneWayNormal;
                settings.OneWaySlop = surface.OneWaySlop;
            }
        }
    }

    public EntityId2D Id { get; }
    public ObjectPlacement2D Placement { get; }
    public SpatialObject2D WorldObject { get; }
    public RigidBody2D? Body { get; }

    public void Update(float dt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.NonNegative(dt);
        if (_kinematic is not null) _kinematic.Update(dt);
        else if (_visualMotion is not null)
        {
            _time += dt;
            WorldObject.Transform.Position = _visualMotion.Position(_time);
        }
    }

    public ObjectDefinition2D CaptureDefinition() => new(Id, Placement.Id, Placement.Asset.Geometry, Placement.Asset.Material, Placement.Asset.TextureRoot);
    public ObjectState2D CaptureState() => new(Id, WorldObject.Transform.Position, WorldObject.Transform.Rotation);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_kinematic is not null) _kinematic.Dispose();
        else if (Body is not null) _physics.RemoveBody(Body);
    }
}
